using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Commands.Data.Internal;
using CS2M.Networking;
using LiteNetLib;
using System.Collections.Concurrent;
using System.Threading;
using System;
using System.Linq;
using System.Security;

namespace CS2M.Commands.Handler.Internal
{
    /// <summary>
    ///     Enhanced join request handler with security features: rate limiting, anti-DDoS, and validation
    /// </summary>
    public class JoinRequestHandler : CommandHandler<JoinRequestCommand>
    {
        // Anti-spam protection
        private static readonly ConcurrentDictionary<int, JoinRequestThrottle> _throttling = new();
        private const int MAX_REQUESTS_PER_SECOND = 3;
        private const int REQUEST_COOLDOWN_MS = 1000;
        
        // Track last seen peer IPs for additional protection
        private static readonly ConcurrentDictionary<int, long> _lastRequestTime = new();
        
        public JoinRequestHandler()
        {
            TransactionCmd = false;
            RelayOnServer = false;
        }

        protected override void Handle(JoinRequestCommand command)
        {
            // This should only be called server-side
        }

        public void HandleOnServer(JoinRequestCommand command, NetPeer peer)
        {
            try
            {
                ValidatePlayerState();
                ValidatePeer(peer);
                CheckRateLimit(peer);
                ValidateCommand(command);
                
                Log.Debug($"Processing join request from peer {peer.Id}");
                
                AcceptJoinRequest(peer);
            }
            catch (SecurityException ex)
            {
                Log.Warn($"Rejecting join request due to security violation: {ex.Message}");
                RejectJoinRequest(peer, "Security violation");
            }
            catch (System.Exception ex)
            {
                Log.Error($"Join request failed: {ex.Message}", ex);
            }
        }

        private void ValidatePlayerState()
        {
            if (NetworkInterface.Instance.LocalPlayer.PlayerType != PlayerType.SERVER)
            {
                Log.Warn("Received join request on non-server player");
                return;
            }
        }

        private void ValidatePeer(NetPeer peer)
        {
            if (!NetworkInterface.Instance.IsPeerConnected(peer))
            {
                throw new SecurityException($"Unknown peer attempting to join: {peer?.Id}");
            }

            if (NetworkInterface.Instance.IsPeerJoined(peer))
            {
                throw new SecurityException($"Already joined peer attempting to rejoin: {peer?.Id}");
            }
        }

        private void CheckRateLimit(NetPeer peer)
        {
            int peerId = peer.Id;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            
            // Get or create throttle state
            var throttleState = _throttling.GetOrAdd(peerId, id => 
                new JoinRequestThrottle { Requests = 0, StartTime = now });
            
            lock (throttleState)
            {
                // Check if cooldown period has passed
                if (now - throttleState.StartTime > TimeSpan.FromMilliseconds(REQUEST_COOLDOWN_MS).Ticks)
                {
                    // Reset counters
                    throttleState.Requests = 0;
                    throttleState.StartTime = now;
                }
                
                // Check if limit exceeded
                if (throttleState.Requests >= MAX_REQUESTS_PER_SECOND)
                {
                    throw new SecurityException(
                        $"Rate limit exceeded for peer {peerId}: {throttleState.Requests} requests/sec");
                }
                
                throttleState.Requests++;
                
                // Add extra layer of protection based on absolute time
                long absLastTime = GetAndCheckAbsoluteTime(peerId);
                if (absLastTime > 0 && (now - absLastTime) < TimeSpan.FromMilliseconds(500).Ticks)
                {
                    throw new SecurityException(
                        $"Too frequent join attempts detected for peer {peerId}");
                }
            }
        }

        private long GetAndCheckAbsoluteTime(int peerId)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            _lastRequestTime.AddOrUpdate(peerId, now, (id, old) =>
            {
                long result = now;
                return result;
            });
            return _lastRequestTime.TryGetValue(peerId, out var lastTime) ? lastTime : 0;
        }

        private void ValidateCommand(JoinRequestCommand command)
        {
            // Validate command structure
            if (string.IsNullOrWhiteSpace(command.Username))
            {
                throw new SecurityException("Empty username in join request");
            }
            
            // Basic username length check
            if (command.Username.Length > 64)
            {
                throw new SecurityException($"Username too long: {command.Username.Length} characters");
            }
            
            // Username sanitization (basic checks)
            foreach (char c in command.Username)
            {
                if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && c != '_')
                {
                    throw new SecurityException($"Invalid character in username: '{c}'");
                }
            }
            
            // Limit special characters
            int specialCharCount = command.Username.Count(c => !char.IsLetterOrDigit(c));
            if (specialCharCount > 3)
            {
                throw new SecurityException($"Too many special characters in username");
            }
        }

        private void AcceptJoinRequest(NetPeer peer)
        {
            Log.Info($"Accepting join request from peer {peer.Id}");
            
            bool transferStarted = NetworkInterface.Instance.BeginWorldTransfer(peer);
            if (!transferStarted)
            {
                Log.Warn($"Failed to start world transfer for peer {peer.Id}");
                RejectJoinRequest(peer, "Failed to initialize world transfer");
            }
            else
            {
                Log.Debug($"World transfer initiated successfully for peer {peer.Id}");
            }
        }

        private void RejectJoinRequest(NetPeer peer, string reason)
        {
            Log.Warn($"Rejecting join request for peer {peer.Id}: {reason}");
            try
            {
                peer.Disconnect();
            }
            catch
            {
                // Ignore disconnect errors
            }
        }
    }

    /// <summary>
    ///     Per-peer rate limiting state
    /// </summary>
    public sealed class JoinRequestThrottle
    {
        public int Requests;
        public long StartTime;
    }
}
