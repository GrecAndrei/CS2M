using System;
using System.Collections.Generic;
using System.Threading;
using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Settings;
using CS2M.Commands.Handler.BaseGame;
using CS2M.Commands.Handler.Internal;
using CS2M.Helpers;

namespace CS2M.Networking
{
    /// <summary>
    ///     Manages network system lifecycle and initialization
    /// </summary>
    public static class NetworkInitializer
    {
        private static bool _isInitialized;
        private static bool _isShuttingDown;
        private static readonly object _lock = new object();
        
        private static NetworkManager _networkManager;
        private static ModSettings _settings;
        private static int _connectionAttempts = 0;
        private const int MAX_CONNECTION_ATTEMPTS = 3;
        private const int CONNECTION_RETRY_DELAY_MS = 2000;

        /// <summary>
        ///     Initialize network subsystem
        /// </summary>
        public static bool Initialize(ModSettings settings)
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    Log.Warn("Network subsystem already initialized");
                    return false;
                }

                if (_isShuttingDown)
                {
                    Log.Error("Cannot initialize: subsystem is shutting down");
                    return false;
                }

                Log.Info("Initializing network subsystem...");
                
                _settings = settings ?? throw new ArgumentNullException(nameof(settings));
                _networkManager = new NetworkManager();

                // Set up event handlers
                SetupEventHandlers();

                // Initialize command handlers
                CommandSystem.Initialize();

                // Register base game handlers
                CommandHandlerInitializer.RegisterHandler<Commands.Handler.BaseGame.FrameCommandHandler>();
                CommandHandlerInitializer.RegisterHandler<Commands.Handler.BaseGame.MoneyCommandHandler>();

                _isInitialized = true;
                
                Log.Info("Network subsystem initialized successfully.");
                return true;
            }
        }

        /// <summary>
        ///     Connect to external server using token or IP
        /// </summary>
        public static bool ConnectToServer(string hostAddress, int port, string password)
        {
            if (!_isInitialized || _isShuttingDown)
            {
                Log.Error("Cannot connect: network not initialized");
                return false;
            }

            if (_connectionAttempts >= MAX_CONNECTION_ATTEMPTS)
            {
                Log.Error($"Connection attempts exceeded maximum ({MAX_CONNECTION_ATTEMPTS})");
                return false;
            }

            _connectionAttempts++;

            try
            {
                var connectionConfig = new ConnectionConfig(hostAddress, port, password);
                
                if (!_networkManager.InitConnect(connectionConfig))
                {
                    Log.Error("Failed to initialize connection");
                    return false;
                }

                if (!_networkManager.SetupNatConnect())
                {
                    Log.Error("Failed to setup NAT connection");
                    return false;
                }

                if (!_networkManager.Connect())
                {
                    Log.Error("Failed to establish connection");
                    return false;
                }

                Log.Info($"Connection initiated to {hostAddress}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Connection failed: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        ///     Start local server for hosting
        /// </summary>
        public static bool StartLocalServer(int port, string password)
        {
            if (!_isInitialized || _isShuttingDown)
            {
                Log.Error("Cannot start server: network not initialized");
                return false;
            }

            try
            {
                var connectionConfig = new ConnectionConfig(port, password);
                
                if (!_networkManager.StartServer(connectionConfig))
                {
                    Log.Error("Failed to start local server");
                    return false;
                }

                Log.Info($"Local server started on port {port}");
                
                // Update player type to SERVER
                UpdatePlayerType(PlayerType.SERVER);
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Server startup failed: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        ///     Process pending network events
        /// </summary>
        public static void ProcessEvents()
        {
            if (!_isInitialized || _networkManager == null)
                return;

            try
            {
                _networkManager.ProcessEvents();
                
                // Track latency from connected peers
                foreach (var peer in _networkManager.NetManager.ConnectedPeerList)
                {
                    // Update statistics
                    NetworkStatistics.UpdateLatency(peer.Id, peer.Ping);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Event processing error: {ex.Message}");
            }
        }

        /// <summary>
        ///     Shutdown network subsystem gracefully
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (!_isInitialized)
                    return;

                Log.Info("Shutting down network subsystem...");
                _isShuttingDown = true;

                _networkManager?.Stop();
                _networkManager = null;

                CommandSystem.Reset();

                _isInitialized = false;
                _connectionAttempts = 0;

                Log.Info("Network subsystem shutdown complete");
            }
        }

        /// <summary>
        ///     Get current network manager instance
        /// </summary>
        public static NetworkManager GetNetworkManager()
        {
            return _networkManager;
        }

        /// <summary>
        ///     Check if network is currently active
        /// </summary>
        public static bool IsNetworkActive()
        {
            return _isInitialized && _networkManager != null && _networkManager.IsRunning;
        }

        /// <summary>
        ///     Reset connection attempt counter
        /// </summary>
        internal static void ResetConnectionAttempts()
        {
            _connectionAttempts = 0;
        }

        /// <summary>
        ///     Get network status summary
        /// </summary>
        public static NetworkStatus Summary => new NetworkStatus
        {
            IsInitialized = _isInitialized,
            IsShuttingDown = _isShuttingDown,
            IsActive = IsNetworkActive(),
            ConnectionAttempts = _connectionAttempts,
            ManagerState = _networkManager?.ConnectionState ?? ConnectionState.Disconnected
        };

        private static void SetupEventHandlers()
        {
            // NAT hole punch success
            _networkManager.NatHolePunchSuccessfulEvent += () =>
            {
                Log.Info("NAT hole punch successful");
                return true;
            };

            // NAT hole punch failure
            _networkManager.NatHolePunchFailedEvent += () =>
            {
                Log.Warn("NAT hole punch failed, attempting direct connection");
                return true;
            };

            // Client connection success
            _networkManager.ClientConnectSuccessfulEvent += () =>
            {
                Log.Info("Client connection successful");
                UpdatePlayerType(PlayerType.CLIENT);
                return true;
            };

            // Client connection failure
            _networkManager.ClientConnectFailedEvent += () =>
            {
                Log.Error("Client connection failed");
                if (_connectionAttempts < MAX_CONNECTION_ATTEMPTS)
                {
                    Thread.Sleep(CONNECTION_RETRY_DELAY_MS);
                    _connectionAttempts++;
                    // Retry logic would go here
                }
                else
                {
                    Log.Error("Maximum connection attempts reached");
                }
                return true;
            };

            // Client disconnect
            _networkManager.ClientDisconnectEvent += () =>
            {
                Log.Info("Client disconnected");
                UpdatePlayerType(PlayerType.NONE);
                return true;
            };
        }

        private static void UpdatePlayerType(PlayerType type)
        {
            // This would update the actual player type through NetworkInterface
            // For now, just logging
            Log.Debug($"Player type updated to: {type}");
        }

        /// <summary>
        ///     Current network status information
        /// </summary>
        public struct NetworkStatus
        {
            public bool IsInitialized;
            public bool IsShuttingDown;
            public bool IsActive;
            public int ConnectionAttempts;
            public ConnectionState ManagerState;
        }
    }
}
