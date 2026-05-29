using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Linq;
using System.Threading.Tasks;
using Colossal;
using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Commands;
using CS2M.Commands.ApiServer;
using CS2M.Commands.Data.Internal;
using CS2M.Helpers;
using LiteNetLib;
using Unity.Entities;

namespace CS2M.Networking
{
    public class NetworkInterface
    {
        private const int PacketOverheadBytes = 25;
        private const int MaxWorldTransferAttempts = 3;
        private const int WorldTransferRetryDelayMs = 1000;

        public delegate void OnPlayerConnected(Player player);

        public delegate void OnPlayerDisconnected(Player player);

        public delegate void OnPlayerJoined(Player player);

        public delegate void OnPlayerLeft(Player player);

        private static NetworkInterface _instance;
        private readonly HashSet<int> _worldTransfersInProgress = new();
        private readonly Dictionary<int, int> _worldTransferIds = new();

        public readonly LocalPlayer LocalPlayer = new();

        /// <summary>
        ///     List of all players, which are connected on network level
        /// </summary>
        public List<Player> PlayerListConnected = new();

        /// <summary>
        ///     List of all players, which are connected on game level
        /// </summary>
        public List<Player> PlayerListJoined = new();

        public NetworkInterface()
        {
            PlayerListConnected.Add(LocalPlayer);
            PlayerListJoined.Add(LocalPlayer);
        }

        public static NetworkInterface Instance => _instance ??= new NetworkInterface();

        /// <summary>
        ///     Event is triggered, when a player is connected on the network level
        /// </summary>
        public event OnPlayerConnected PlayerConnectedEvent;

        /// <summary>
        ///     Event is triggered, when a player disconnects on the network level
        /// </summary>
        public event OnPlayerDisconnected PlayerDisconnectedEvent;

        /// <summary>
        ///     Event is triggered, when a player joins on the game level
        /// </summary>
        public event OnPlayerJoined PlayerJoinedEvent;

        /// <summary>
        ///     Event is triggered, when a player leaves on the game level
        /// </summary>
        public event OnPlayerLeft PlayerLeftEvent;

        public void OnUpdate()
        {
            LocalPlayer.OnUpdate();
        }

        public void Connect(ConnectionConfig connectionConfig)
        {
            LocalPlayer.GetServerInfo(connectionConfig);
        }

        public void UpdateLocalPlayerUsername(string username)
        {
            LocalPlayer.UpdateUsername(username);
        }

        public void StartServer(ConnectionConfig connectionConfig)
        {
            LocalPlayer.Playing(connectionConfig);
        }

        public void StopServer()
        {
            LocalPlayer.Inactive();
        }

        public void SendToAll(CommandBase message)
        {
            LocalPlayer.SendToAll(message);
        }

        public void SendToClient(Player player, CommandBase message)
        {
            if (player is RemotePlayer remotePlayer)
            {
                LocalPlayer.SendToClient(remotePlayer.NetPeer, message);
            }
            else
            {
                Log.Warn("Trying to send packet to non-csm player, ignoring.");
            }
        }

        public void SendToServer(CommandBase message)
        {
            LocalPlayer.SendToServer(message);
        }

        public void SendToApiServer(ApiCommandBase message)
        {
            LocalPlayer.SendToApiServer(message);
        }

        public void SendToClients(CommandBase message)
        {
            LocalPlayer.SendToClients(message);
        }

        public RemotePlayer GetPlayerByPeer(NetPeer peer)
        {
            if (peer == null)
            {
                return null;
            }

            return PlayerListConnected
                .Where(p => p is RemotePlayer)
                .Cast<RemotePlayer>()
                .FirstOrDefault(p => p.NetPeer != null && p.NetPeer.Id == peer.Id);
        }

        public bool IsPeerConnected(NetPeer peer)
        {
            if (peer == null)
            {
                return false;
            }

            return PlayerListConnected
                .Where(p => p is RemotePlayer)
                .Cast<RemotePlayer>()
                .Any(p => p.NetPeer != null && p.NetPeer.Id == peer.Id);
        }

        public bool IsPeerJoined(NetPeer peer)
        {
            if (peer == null)
            {
                return false;
            }

            return PlayerListJoined
                .Where(p => p is RemotePlayer)
                .Cast<RemotePlayer>()
                .Any(p => p.NetPeer != null && p.NetPeer.Id == peer.Id);
        }

        public bool PlayerJoined(NetPeer peer)
        {
            RemotePlayer player = GetPlayerByPeer(peer);
            if (player == null)
            {
                Log.Warn($"Cannot mark peer {peer?.Id} as joined: no connected player found.");
                return false;
            }

            if (IsPeerJoined(peer))
            {
                return true;
            }

            EndWorldTransfer(peer.Id);
            PlayerListJoined.Add(player);
            PlayerJoinedEvent?.Invoke(player);
            Log.Debug($"RemotePlayer '{player.Username}' joined gameplay.");
            return true;
        }

        public bool PlayerDisconnected(NetPeer peer)
        {
            RemotePlayer player = GetPlayerByPeer(peer);
            if (player == null)
            {
                return false;
            }

            bool wasJoined = PlayerListJoined.Remove(player);
            bool wasConnected = PlayerListConnected.Remove(player);

            if (wasJoined)
            {
                PlayerLeftEvent?.Invoke(player);
            }

            if (wasConnected)
            {
                EndWorldTransfer(peer.Id);
                _worldTransferIds.Remove(peer.Id);
                PlayerDisconnectedEvent?.Invoke(player);
                Log.Debug($"RemotePlayer '{player.Username}' disconnected.");
            }

            return wasConnected || wasJoined;
        }

        public void ResetRemotePlayers()
        {
            PlayerListConnected.RemoveAll(player => player is RemotePlayer);
            PlayerListJoined.RemoveAll(player => player is RemotePlayer);
            _worldTransfersInProgress.Clear();
            _worldTransferIds.Clear();

            if (!PlayerListConnected.Contains(LocalPlayer))
            {
                PlayerListConnected.Add(LocalPlayer);
            }

            if (!PlayerListJoined.Contains(LocalPlayer))
            {
                PlayerListJoined.Add(LocalPlayer);
            }
        }

        public void PlayerConnected(RemotePlayer player)
        {
            if (player == null || player.NetPeer == null)
            {
                Log.Warn("Cannot register connected player: peer is null.");
                return;
            }

            if (IsPeerConnected(player.NetPeer))
            {
                Log.Warn($"Peer {player.NetPeer.Id} is already tracked as connected.");
                return;
            }

            Log.Debug($"RemotePlayer '{player.Username}' connected.");
            PlayerListConnected.Add(player);
            PlayerConnectedEvent?.Invoke(player);
        }

        public bool BeginWorldTransfer(NetPeer peer)
        {
            RemotePlayer player = GetPlayerByPeer(peer);
            if (player == null)
            {
                Log.Warn($"Cannot start world transfer for peer {peer?.Id}: peer not connected.");
                return false;
            }

            return BeginWorldTransfer(player);
        }

        private bool BeginWorldTransfer(RemotePlayer player)
        {
            int peerId = player.NetPeer.Id;
            if (!TryStartWorldTransfer(peerId))
            {
                Log.Debug($"World transfer already in progress for peer {peerId}, ignoring duplicate request.");
                return false;
            }

            // Get max packet size from MTU discovery
            int maxPacketSize = player.NetPeer.GetMaxSinglePacketSize(DeliveryMethod.ReliableOrdered);
            maxPacketSize -= PacketOverheadBytes;
            if (maxPacketSize <= 0)
            {
                Log.Warn($"Cannot start world transfer for peer {peerId}: invalid packet size {maxPacketSize}.");
                EndWorldTransfer(peerId);
                return false;
            }

            // Send world
            TaskManager.instance.EnqueueTask("WorldTransfer", async () =>
            {
                try
                {
                    for (int attempt = 1; attempt <= MaxWorldTransferAttempts; attempt++)
                    {
                        if (!IsPeerConnected(player.NetPeer))
                        {
                            Log.Debug($"Stopped world transfer to peer {player.NetPeer.Id}: peer disconnected.");
                            return;
                        }

                        bool success = await SendWorldTransferAttempt(player, maxPacketSize, attempt);
                        if (success)
                        {
                            return;
                        }

                        if (attempt < MaxWorldTransferAttempts)
                        {
                            Log.Warn(
                                $"World transfer attempt {attempt}/{MaxWorldTransferAttempts} failed for peer {peerId}, retrying.");
                            await Task.Delay(WorldTransferRetryDelayMs);
                        }
                    }

                    Log.Warn($"World transfer failed after {MaxWorldTransferAttempts} attempts for peer {peerId}.");
                    if (IsPeerConnected(player.NetPeer))
                    {
                        player.NetPeer.Disconnect();
                    }
                }
                finally
                {
                    EndWorldTransfer(peerId);
                }
            });

            return true;
        }

        private async Task<bool> SendWorldTransferAttempt(RemotePlayer player, int maxPacketSize, int attempt)
        {
            try
            {
                SaveLoadHelper saveLoadHelper =
                    World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SaveLoadHelper>();
                SlicedPacketStream stream = await saveLoadHelper.SaveGame(maxPacketSize);
                if (stream == null)
                {
                    Log.Warn($"World transfer packaging failed for peer {player.NetPeer.Id}.");
                    return false;
                }

                if (!IsPeerConnected(player.NetPeer))
                {
                    return false;
                }

                int transferId = NextWorldTransferId(player.NetPeer.Id);
                int remainingBytes = (int)stream.Length;
                int sliceIndex = 0;
                bool newTransfer = true;

                var watch = Stopwatch.StartNew();
                CommandInternal.Instance.SendToClient(player, new JoinAcceptedCommand());
                Log.Debug(
                    $"Sending world attempt {attempt}/{MaxWorldTransferAttempts} to peer {player.NetPeer.Id} with size {stream.Length} bytes and transfer id {transferId}.");

                foreach (byte[] slice in stream.GetSlices())
                {
                    if (!IsPeerConnected(player.NetPeer))
                    {
                        Log.Debug($"Stopped world transfer to peer {player.NetPeer.Id}: peer disconnected.");
                        return false;
                    }

                    remainingBytes -= slice.Length;
                    if (remainingBytes < 0)
                    {
                        remainingBytes = 0;
                    }

                    var cmd = new WorldTransferCommand
                    {
                        TransferId = transferId,
                        SliceIndex = sliceIndex,
                        WorldSlice = slice,
                        RemainingBytes = remainingBytes,
                        NewTransfer = newTransfer,
                    };

                    CommandInternal.Instance.SendToClient(player, cmd);
                    newTransfer = false;
                    sliceIndex++;
                }

                Log.Debug(
                    $"World transfer attempt {attempt}/{MaxWorldTransferAttempts} completed for peer {player.NetPeer.Id} in {watch.ElapsedMilliseconds}ms.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"World transfer attempt {attempt}/{MaxWorldTransferAttempts} failed: {ex}");
                return false;
            }
        }

        private int NextWorldTransferId(int peerId)
        {
            if (!_worldTransferIds.TryGetValue(peerId, out int transferId))
            {
                transferId = 0;
            }

            transferId++;
            _worldTransferIds[peerId] = transferId;
            return transferId;
        }

        private bool TryStartWorldTransfer(int peerId)
        {
            return _worldTransfersInProgress.Add(peerId);
        }

        private void EndWorldTransfer(int peerId)
        {
            _worldTransfersInProgress.Remove(peerId);
        }
    }
}
