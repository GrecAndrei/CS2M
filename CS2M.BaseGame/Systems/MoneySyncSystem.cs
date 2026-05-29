using Game;
using Game.Simulation;
using Unity.Entities;
using UnityEngine;
using CS2M.API.Commands;
using CS2M.BaseGame.Commands;
using System.Collections.Generic;
using System;

namespace CS2M.BaseGame.Systems
{
    /// <summary>
    ///     Enhanced money synchronization system with validation, anti-cheat, and smoothing
    /// </summary>
    public partial class MoneySyncSystem : GameSystemBase
    {
        private const int SAMPLE_INTERVAL = 30; // Sample every 30 frames (~0.5 seconds)
        private const int BROADCAST_INTERVAL = 20; // Broadcast authority every 20 samples
        private const float MAX_REASONABLE_RATE_OF_CHANGE = 100000.0f; // Max money change per second
        
        private CitySystem _citySystem;
        
        // State tracking
        private long _lastMoney = 0;
        private bool _isApplying = false;
        private int _sampleCounter = 0;
        private int _authorityBroadcastCounter = 0;
        
        // Interpolation and smoothing for clients
        private readonly Queue<MoneySample> _moneyHistory = new();
        private const int MAX_HISTORY_SIZE = 10;
        private double _smoothedMoney = 0;
        private double _smoothingAlpha = 0.1;
        
        // Anti-cheat: track recent values for rate checking
        private readonly List<MoneyMeasurement> _recentMeasurements = new();
        private const int MAX_MEASUREMENTS = 60;
        
        // Server authority state
        private bool _serverAuthoritative = true;
        private uint _authorityEpoch = 0;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            _citySystem = World.GetOrCreateSystemManaged<CitySystem>();
            
            Log.Debug($"MoneySyncSystem created for role {Command.CurrentRole}");
            
            // Initialize last money from current state
            _lastMoney = _citySystem.moneyAmount;
            _smoothedMoney = _citySystem.moneyAmount;
        }

        protected override void OnUpdate()
        {
            switch (Command.CurrentRole)
            {
                case MultiplayerRole.Server:
                    HandleServerLogic();
                    break;
                    
                case MultiplayerRole.Client:
                    HandleClientLogic();
                    break;
                    
                default:
                    return;
            }
        }

        private void HandleServerLogic()
        {
            _sampleCounter++;
            
            if (_sampleCounter < SAMPLE_INTERVAL)
                return;

            _sampleCounter = 0;

            long currentMoney = GetActualMoney();
            bool moneyChanged = !ShouldSmoothApply(currentMoney);

            _authorityBroadcastCounter++;
            bool shouldBroadcast = _authorityBroadcastCounter >= BROADCAST_INTERVAL || moneyChanged;

            if (shouldBroadcast)
            {
                _authorityBroadcastCounter = 0;
                
                // Validate the value before broadcasting
                if (ValidateMoneyValue(currentMoney))
                {
                    ApplyAntiCheatChecks(currentMoney);
                    SendMoneyAuthority(currentMoney);
                }

                _lastMoney = currentMoney;
                _isApplying = false;
            }
        }

        private void HandleClientLogic()
        {
            ProcessMoneyHistory();
            UpdateSmoothedMoney();
        }

        private long GetActualMoney()
        {
            return _citySystem.moneyAmount;
        }

        private bool ShouldSmoothApply(long actualMoney)
        {
            // Only apply smoothly if we're receiving authoritative values
            return _isApplying && Math.Abs(_smoothedMoney - actualMoney) < 1000;
        }

        private void ProcessMoneyHistory()
        {
            lock (_moneyHistory)
            {
                while (_moneyHistory.Count > 0)
                {
                    var sample = _moneyHistory.Peek();
                    
                    // Check if this sample is too old
                    if (DateTime.UtcNow.ToUnixTimeMilliseconds() - sample.Timestamp > 2000)
                    {
                        _moneyHistory.Dequeue();
                        continue;
                    }
                    
                    // Take interpolated value
                    _smoothedMoney = sample.Value;
                    break;
                }
            }
        }

        private void UpdateSmoothedMoney()
        {
            // Smoothly interpolate towards the actual value
            double target = _citySystem.moneyAmount;
            _smoothedMoney = LinearInterpolate(_smoothedMoney, target, _smoothingAlpha);
            
            // Occasionally correct based on server authority (if lag detected)
            if (Math.Abs(_smoothedMoney - target) > 10000)
            {
                _smoothedMoney = target; // Snap if too far off
            }
        }

        private double LinearInterpolate(double a, double b, double alpha)
        {
            return a + (b - a) * alpha;
        }

        private void SendMoneyAuthority(long money)
        {
            Command.SendToAll(new MoneyCommand
            {
                Money = money,
                AuthorityEpoch = _authorityEpoch,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            Log.Trace($"Broadcasting money authority: {money}, epoch {_authorityEpoch}");
        }

        private void ApplyAntiCheatChecks(long currentMoney)
        {
            long delta = currentMoney - _lastMoney;
            long absDelta = Math.Abs(delta);
            
            // Check rate of change
            if (absDelta > MAX_REASONABLE_RATE_OF_CHANGE)
            {
                Log.Warn($"Money changed too rapidly: {delta} in one frame!");
                Log.Warn($"Validating source...");
                
                // Revert to previous valid value
                SetMoney(_lastMoney);
                return;
            }
            
            // Add measurement for further analysis
            AddMeasurement(currentMoney, delta);
            
            // Check for suspicious patterns (only log once per session)
            if (HasSuspiciousPattern())
            {
                Log.Warn("Suspicious money activity detected!");
                // Could trigger additional validation here
            }
        }

        private void AddMeasurement(long currentMoney, long delta)
        {
            lock (_recentMeasurements)
            {
                _recentMeasurements.Add(new MoneyMeasurement
                {
                    Value = currentMoney,
                    Delta = delta,
                    Timestamp = DateTime.UtcNow
                });

                // Keep only recent measurements
                while (_recentMeasurements.Count > MAX_MEASUREMENTS)
                {
                    _recentMeasurements.RemoveAt(0);
                }
            }
        }

        private bool HasSuspiciousPattern()
        {
            lock (_recentMeasurements)
            {
                if (_recentMeasurements.Count < 10)
                    return false;

                // Look for consistent large increases that don't match game mechanics
                int largeIncreases = 0;
                foreach (var m in _recentMeasurements)
                {
                    if (m.Delta > MAX_REASONABLE_RATE_OF_CHANGE / 4) // Quarter of max threshold
                        largeIncreases++;
                }

                // More than 5 large increases in 10 measurements is suspicious
                return largeIncreases > 5;
            }
        }

        private bool ValidateMoneyValue(long value)
        {
            // Cannot be negative
            if (value < 0)
            {
                Log.Error($"Invalid money value: {value} is negative");
                return false;
            }

            // Cannot exceed maximum (adjust based on game balance)
            const long MAX_MONEY = 1_000_000_000_000L; // 1 trillion
            if (value > MAX_MONEY)
            {
                Log.Error($"Money value exceeded maximum: {value}");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Handles incoming money update from server
        /// </summary>
        public void ReceiveMoneyUpdate(MoneyCommand command)
        {
            if (Command.CurrentRole != MultiplayerRole.Client)
                return;

            // Validate authority epoch
            if (command.AuthorityEpoch <= _authorityEpoch)
            {
                Log.Trace($"Ignoring outdated money update, epoch: {command.AuthorityEpoch} <= {_authorityEpoch}");
                return;
            }

            // Validate money value
            if (!ValidateMoneyValue(command.Money))
            {
                Log.Error($"Received invalid money value from server: {command.Money}");
                return;
            }

            // Check reasonableness compared to our local value
            long localDiff = Math.Abs(command.Money - _citySystem.moneyAmount);
            if (localDiff > MAX_REASONABLE_RATE_OF_CHANGE * 2)
            {
                Log.Warn($"Large money discrepancy detected: local={_citySystem.moneyAmount}, server={command.Money}");
                // Don't panic-reject, but note it for monitoring
            }

            // Update state
            _authorityEpoch = command.AuthorityEpoch;
            _isApplying = true;
            
            // Add to history for interpolation
            lock (_moneyHistory)
            {
                if (_moneyHistory.Count >= MAX_HISTORY_SIZE * 2)
                {
                    _moneyHistory.Dequeue();
                }
                
                _moneyHistory.Enqueue(new MoneySample
                {
                    Value = command.Money,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }

            // Apply directly (server is authoritative)
            SetMoney(command.Money);
            _lastMoney = command.Money;
            
            Log.Trace($"Received money authority: {command.Money}");
        }

        /// <summary>
        ///     Sets money value (called by server and client)
        /// </summary>
        public void SetMoney(long money)
        {
            try
            {
                _citySystem.SetPrivateProperty("moneyAmount", money);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to set money: {ex.Message}");
            }
        }

        /// <summary>
        ///     Forces immediate money update (for testing or critical corrections)
        /// </summary>
        public void ForceUpdateMoney(long money)
        {
            if (!ValidateMoneyValue(money))
            {
                Log.Error($"Force update rejected: invalid value {money}");
                return;
            }

            _isApplying = false;
            SetMoney(money);
            _lastMoney = money;
            
            _authorityEpoch++;
            Log.Info($"Forced money update to {money}");
        }

        /// <summary>
        ///     Resets all money synchronization state
        /// </summary>
        public void Reset()
        {
            lock (_moneyHistory)
            {
                while (_moneyHistory.Count > 0)
                    _moneyHistory.Dequeue();
            }

            _lastMoney = _citySystem.moneyAmount;
            _smoothedMoney = _citySystem.moneyAmount;
            _recentMeasurements.Clear();
            _authorityEpoch = 0;
            _isApplying = false;
            
            Log.Debug("MoneySyncSystem reset");
        }
    }

    /// <summary>
    ///     Represents a money observation point
    /// </summary>
    public struct MoneySample
    {
        public double Value;
        public long Timestamp;
    }

    /// <summary>
    ///     Measurement for anti-cheat analysis
    /// </summary>
    public struct MoneyMeasurement
    {
        public long Value;
        public long Delta;
        public DateTime Timestamp;
    }

    /// <summary>
    ///     Utility for Unix timestamp conversion
    /// </summary>
    public static class DateTimeExtensions
    {
        public static long ToUnixTimeMilliseconds(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
        }
    }
}
