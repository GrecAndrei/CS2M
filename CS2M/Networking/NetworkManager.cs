using CS2M.API;
using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Commands;
using CS2M.Commands.ApiServer;
using CS2M.Util;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CS2M.Commands.Data.Internal;
using CS2M.Commands.Handler.Internal;
using Timer = System.Timers.Timer;

namespace CS2M.Networking
{
    /// <summary>
    ///     Enhanced network manager with improved error handling, thread safety, and state management
    /// </summary>
    public class NetworkManager
    {
        private const string ConnectionKey = "CSM";
        
        // Thread-safe collections for peer tracking
        private readonly ConcurrentDictionary<int, NetPeer> _activePeers = new();
        private readonly object _lock = new object();
        
        private readonly NetManager _netManager;
        public NetManager NetManager => _netManager;
        private readonly ApiServer _apiServer;
        private ConnectionConfig _connectionConfig;
        private IPEndPoint _connectEndpoint;
        private Timer _timeout;
        private Timer _natTimeoutTimer;
        private Timer _connectTimeoutTimer;
        private readonly ConcurrentDictionary<int, Timer> _peerRegistrationTimers = new();
        private bool _pollNatEvent = false;
        private bool _isStarted = false;
        private bool _isShuttingDown = false;

        public event OnNatHolePunchSuccessful NatHolePunchSuccessfulEvent;
        public event OnNatHolePunchFailed NatHolePunchFailedEvent;
        public event OnClientConnectSuccessful ClientConnectSuccessfulEvent;
        public event OnClientConnectFailed ClientConnectFailedEvent;
        public event OnClientDisconnect ClientDisconnectEvent;

        /// <summary>
        ///     Indicates if the network manager is currently running
        /// </summary>
        public bool IsRunning => _isStarted && !_isShuttingDown;

        /// <summary>
        ///     Current connection state for debugging and recovery
        /// </summary>
        public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;

        public NetworkManager()
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            _netManager = new NetManager(listener)
            {
                NatPunchEnabled = true,
                UnconnectedMessagesEnabled = true,
                MtuDiscovery = true,
            };
            _apiServer = new ApiServer(_netManager);

            listener.NetworkReceiveEvent += ListenerOnNetworkReceiveEvent;
            listener.NetworkErrorEvent += ListenerOnNetworkErrorEvent;
            listener.PeerConnectedEvent += ListenerOnPeerConnectedEvent;
            listener.PeerDisconnectedEvent += ListenerOnPeerDisconnectedEvent;
            listener.NetworkLatencyUpdateEvent += ListenerOnNetworkLatencyUpdateEvent;
            listener.ConnectionRequestEvent += ListenerOnConnectionRequestEvent;
        }

        public bool InitConnect(ConnectionConfig connectionConfig)
        {
            lock (_lock)
            {
                if (IsRunning)
                {
                    Log.Warn("Cannot initialize connect while already running");
                    return false;
                }
            }

            try
            {
                Log.Trace("NetworkManager: InitConnect");

                if (connectionConfig.IsTokenBased())
                {
                    Log.Info($"Initializing connect to server via token...");
                }
                else
                {
                    Log.Info($"Initializing connect to server at {connectionConfig.HostAddress}:{connectionConfig.Port}...");
                }

                _connectionConfig = connectionConfig;
                ConnectionState = ConnectionState.Initializing;

                bool result = _netManager.Start();
                if (!result)
                {
                    Log.Error("Failed to start NetManager");
                    ConnectionState = ConnectionState.Failed;
                    return false;
                }

                _isStarted = true;
                ConnectionState = ConnectionState.Initialized;
                Log.Debug("NetManager started successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"InitConnect failed: {ex.Message}", ex);
                ConnectionState = ConnectionState.Failed;
                return false;
            }
        }

        public bool SetupNatConnect()
        {
            lock (_lock)
            {
                if (_connectionConfig == null)
                {
                    Log.Error("No connection config set before NAT setup");
                    return false;
                }
            }

            Log.Trace("NetworkManager: Setting up NAT connect");

            IPEndPoint directEndpoint = null;
            if (!_connectionConfig.IsTokenBased())
            {
                try
                {
                    directEndpoint = IPUtil.CreateIPEndPoint(_connectionConfig.HostAddress, _connectionConfig.Port);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to resolve host address: {ex.Message}");
                    return false;
                }
            }

            _pollNatEvent = true;
            ConnectionState = ConnectionState.NatHolePunching;

            EventBasedNatPunchListener natPunchListener = new EventBasedNatPunchListener();

            StopAndDisposeTimer(ref _natTimeoutTimer);
            _natTimeoutTimer = new Timer
            {
                Interval = 10000, // Increased timeout for NAT
                AutoReset = false
            };

            _natTimeoutTimer.Elapsed += (sender, args) =>
            {
                if (_isShuttingDown) return;
                Log.Debug("NAT hole punch timed out, attempting direct connection");
                lock (_lock)
                {
                    _pollNatEvent = false;
                    _connectEndpoint = directEndpoint;
                }
                NatHolePunchFailedEvent?.Invoke();
            };

            natPunchListener.NatIntroductionSuccess += (point, type, token) =>
            {
                Log.Debug($"NAT hole punch successful ({point.Address}:{point.Port})");
                lock (_lock)
                {
                    _pollNatEvent = false;
                    _connectEndpoint = point;
                    ConnectionState = ConnectionState.Connected;
                }

                bool? eventResult = NatHolePunchSuccessfulEvent?.Invoke();
                if (eventResult != null && eventResult.Value)
                {
                    StopAndDisposeTimer(ref _natTimeoutTimer);
                }
            };

            try
            {
                _netManager.NatPunchModule.Init(natPunchListener);

                var apiEndpoint = IPUtil.CreateIP4EndPoint(Mod.Instance.Settings.ApiServer, Mod.Instance.Settings.GetApiServerPort());
                _netManager.NatPunchModule.SendNatIntroduceRequest(
                    apiEndpoint,
                    _connectionConfig.IsTokenBased() ? $"token:{_connectionConfig.Token}" : $"ip:{(directEndpoint?.Address != null ? directEndpoint.Address.ToString() : "unknown")}");

                _natTimeoutTimer.Start();
                Log.Debug("NAT hole punch initiated");
            }
            catch (Exception e)
            {
                StopAndDisposeTimer(ref _natTimeoutTimer);
                Log.Error($"NAT hole punch failed: {e.Message}");
                ConnectionState = ConnectionState.Failed;
                return false;
            }

            return true;
        }

        public bool Connect()
        {
            lock (_lock)
            {
                if (_connectEndpoint == null)
                {
                    Log.Error("No valid endpoint available for connection");
                    ConnectionState = ConnectionState.Failed;
                    return false;
                }

                if (_isShuttingDown)
                {
                    Log.Warn("Connection attempt aborted: shutting down");
                    return false;
                }
            }

            Log.Debug($"Connecting to {_connectEndpoint.Address}:{_connectEndpoint.Port}");
            ConnectionState = ConnectionState.Connecting;

            try
            {
                StopAndDisposeTimer(ref _connectTimeoutTimer);
                _connectTimeoutTimer = new Timer
                {
                    Interval = 10000,
                    AutoReset = false
                };

                _connectTimeoutTimer.Elapsed += (sender, args) =>
                {
                    if (_isShuttingDown) return;
                    Log.Debug($"Connection to client ({_connectEndpoint.Address}:{_connectEndpoint.Port}) timed out");
                    ConnectionState = ConnectionState.Failed;
                    ClientConnectFailedEvent?.Invoke();
                };

                _netManager.Connect(_connectEndpoint, ConnectionKey);
                _connectTimeoutTimer.Start();
                return true;
            }
            catch (Exception ex)
            {
                StopAndDisposeTimer(ref _connectTimeoutTimer);
                Log.Error($"Failed to establish connection: {ex.Message}", ex);
                ConnectionState = ConnectionState.Failed;
                return false;
            }
        }

        public void ProcessEvents()
        {
            if (_isShuttingDown || !IsRunning) return;

            try
            {
                if (_pollNatEvent)
                {
                    _netManager.NatPunchModule.PollEvents();
                }

                _netManager.PollEvents();
                _apiServer.KeepAlive(_connectionConfig);
            }
            catch (Exception ex)
            {
                Log.Warn($"Event processing error: {ex.Message}");
            }
        }

        public void SendToAllClients(CommandBase message)
        {
            try
            {
                byte[] data = CommandInternal.Instance.Serialize(message);
                _netManager.SendToAll(data, DeliveryMethod.ReliableOrdered);
                Log.Debug($"Sent {message.GetType().Name} to all clients");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to send to all clients: {ex.Message}");
            }
        }

        public void SendToClient(NetPeer peer, CommandBase message)
        {
            if (peer == null || peer.ConnectionState != LiteNetLib.ConnectionState.Connected)
            {
                Log.Warn($"Attempted to send to disconnected or null peer");
                return;
            }

            try
            {
                byte[] data = CommandInternal.Instance.Serialize(message);
                peer.Send(data, DeliveryMethod.ReliableOrdered);
                
                string logMsg = message is WorldTransferCommand wt && !wt.NewTransfer 
                    ? $"[TRACE] Sent {message.GetType().Name} to peer {peer.Id}"
                    : $"Sent {message.GetType().Name} to peer {peer.Id}";
                    
                Log.Debug(logMsg);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to send to peer {peer.Id}: {ex.Message}");
            }
        }

        public void SendToServer(CommandBase message)
        {
            try
            {
                lock (_lock)
                {
                    var connectedPeers = _netManager.ConnectedPeerList.ToArray();
                    
                    if (connectedPeers.Length == 0)
                    {
                        Log.Warn($"Cannot send {message.GetType().Name}: no server connected");
                        return;
                    }

                    NetPeer server = connectedPeers[0];
                    byte[] data = CommandInternal.Instance.Serialize(message);
                    server.Send(data, DeliveryMethod.ReliableOrdered);
                    Log.Debug($"Sent {message.GetType().Name} to server");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to send to server: {ex.Message}");
            }
        }

        public void SendToApiServer(ApiCommandBase message)
        {
            try
            {
                _apiServer.SendCommand(message);
                Log.Debug($"Sent {message.GetType().Name} to API server");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to send to API server: {ex.Message}");
            }
        }

        public bool StartServer(ConnectionConfig connectionConfig)
        {
            lock (_lock)
            {
                if (IsRunning)
                {
                    Log.Warn("Cannot start server while already running");
                    return false;
                }
            }

            try
            {
                _connectionConfig = connectionConfig;
                ConnectionState = ConnectionState.ServerStarting;

                Log.Info($"Attempting to start server on port {_connectionConfig.Port}...");

                bool result = _netManager.Start(_connectionConfig.Port);
                
                if (!result)
                {
                    Log.Error("Failed to start server");
                    ConnectionState = ConnectionState.Failed;
                    Stop();
                    return false;
                }

                _isStarted = true;
                ConnectionState = ConnectionState.ServerRunning;
                
                Log.Info("Server started successfully");
                Chat.Instance.PrintGameMessage("CS2M.NetworkManager.ServerStarted".Translate());
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"StartServer failed: {ex.Message}", ex);
                ConnectionState = ConnectionState.Failed;
                return false;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isStarted)
                {
                    return;
                }

                _isShuttingDown = true;
            }

            try
            {
                Log.Info("Stopping NetworkManager...");

                // Clear active peers
                _activePeers.Clear();

                // Stop timers
                _timeout?.Stop();
                _timeout?.Dispose();
                _timeout = null;

                StopAndDisposeTimer(ref _natTimeoutTimer);
                StopAndDisposeTimer(ref _connectTimeoutTimer);

                foreach (var pair in _peerRegistrationTimers)
                {
                    pair.Value.Stop();
                    pair.Value.Dispose();
                }
                _peerRegistrationTimers.Clear();

                // Stop NAT punching
                _pollNatEvent = false;

                // Stop the network manager
                _netManager.Stop();

                // Reset state
                _isStarted = false;
                _isShuttingDown = false;
                _connectEndpoint = null;
                _connectionConfig = null;
                ConnectionState = ConnectionState.Disconnected;

                Log.Info("NetworkManager stopped successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"Error during stop: {ex.Message}", ex);
            }
        }

        private void StopAndDisposeTimer(ref Timer timer)
        {
            if (timer == null) return;
            try
            {
                timer.Stop();
                timer.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
            timer = null;
        }

        private void ListenerOnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            try
            {
                // Track peer activity
                lock (_lock)
                {
                    _activePeers.AddOrUpdate(peer.Id, peer, (id, old) => peer);
                }

                CommandBase command = CommandInternal.Instance.Deserialize(reader.GetRemainingBytes());
                if (command == null)
                {
                    Log.Warn($"Received null command from peer {peer.Id}");
                    return;
                }

                CommandHandler handler = CommandInternal.Instance.GetCommandHandler(command.GetType());
                if (handler == null)
                {
                    Log.Warn($"No handler found for {command.GetType()}");
                    return;
                }

                Log.Trace($"Processing {command.GetType().Name} from peer {peer.Id}");

                // Route special commands
                switch (command)
                {
                    case PreconditionsCheckCommand preconds:
                        if (handler is PreconditionsCheckHandler h)
                            h.HandleOnServer(preconds, peer);
                        break;
                    case JoinRequestCommand joinReq:
                        if (handler is JoinRequestHandler jrh)
                            jrh.HandleOnServer(joinReq, peer);
                        break;
                    case JoinReadyCommand readyCmd:
                        if (handler is JoinReadyHandler jrh2)
                            jrh2.HandleOnServer(readyCmd, peer);
                        break;
                    default:
                        // Validate sender for server
                        if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.SERVER)
                        {
                            if (!NetworkInterface.Instance.IsPeerConnected(peer))
                            {
                                Log.Warn($"Dropping command from unauthorized peer {peer.Id}");
                                return;
                            }
                            
                            if (!NetworkInterface.Instance.IsPeerJoined(peer))
                            {
                                Log.Warn($"Dropping command from non-joined peer {peer.Id}");
                                return;
                            }
                        }
                        
                        handler.Parse(command);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to process packet from peer {peer?.Id}: {ex.Message}");
                Log.Trace(ex.ToString());
            }
        }

        private void ListenerOnPeerConnectedEvent(NetPeer peer)
        {
            Log.Debug($"Peer connected: {peer.Id}");

            lock (_lock)
            {
                _activePeers.TryAdd(peer.Id, peer);
            }

            if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.CLIENT)
            {
                StopAndDisposeTimer(ref _connectTimeoutTimer);
                ConnectionState = ConnectionState.Connected;
                ClientConnectSuccessfulEvent?.Invoke();
            }
            else if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.SERVER)
            {
                var registrationTimer = new Timer
                {
                    Interval = 10000,
                    AutoReset = false
                };

                _peerRegistrationTimers[peer.Id] = registrationTimer;

                registrationTimer.Elapsed += (sender, args) =>
                {
                    if (_isShuttingDown) return;
                    if (NetworkInterface.Instance.GetPlayerByPeer(peer) == null)
                    {
                        Log.Warn($"Peer {peer.Id} did not register within 10 seconds, disconnecting");
                        peer.Disconnect();
                    }
                    RemovePeerRegistrationTimer(peer.Id);
                };

                registrationTimer.Start();
            }
        }

        private void RemovePeerRegistrationTimer(int peerId)
        {
            if (_peerRegistrationTimers.TryRemove(peerId, out Timer timer))
            {
                timer.Stop();
                timer.Dispose();
            }
        }

        private void ListenerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Log.Debug($"Peer disconnected: {peer.Id}, Info: {disconnectInfo.Reason}");

            lock (_lock)
            {
                _activePeers.TryRemove(peer.Id, out _);
            }

            RemovePeerRegistrationTimer(peer.Id);

            if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.CLIENT)
            {
                ConnectionState = ConnectionState.Disconnected;
                ClientDisconnectEvent?.Invoke();
            }
            else if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.SERVER)
            {
                NetworkInterface.Instance.GetPlayerByPeer(peer)?.HandleDisconnect();
                NetworkInterface.Instance.PlayerDisconnected(peer);
            }
        }

        private void ListenerOnNetworkErrorEvent(IPEndPoint endpoint, SocketError socketError)
        {
            string source = endpoint != null ? $"{endpoint.Address}:{endpoint.Port}" : "<Unconnected>";
            Log.Error($"Network error from {source}: {socketError}");
            ConnectionState = ConnectionState.Failed;
        }

        private void ListenerOnNetworkLatencyUpdateEvent(NetPeer peer, int latency)
        {
            Log.Trace($"Latency update for peer {peer.Id}: {latency}ms");
        }

        public string GetConnectionPassword()
        {
            return _connectionConfig?.Password;
        }

        private void ListenerOnConnectionRequestEvent(ConnectionRequest request)
        {
            Log.Debug("Incoming connection request");
            request.AcceptIfKey(ConnectionKey);
        }

        public delegate bool OnNatHolePunchSuccessful();
        public delegate bool OnNatHolePunchFailed();
        public delegate bool OnClientConnectSuccessful();
        public delegate bool OnClientConnectFailed();
        public delegate bool OnClientDisconnect();
    }

    /// <summary>
    ///     Represents the current state of network connection
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Initializing,
        Initialized,
        Connecting,
        Connected,
        NatHolePunching,
        ServerStarting,
        ServerRunning,
        Failed
    }
}
