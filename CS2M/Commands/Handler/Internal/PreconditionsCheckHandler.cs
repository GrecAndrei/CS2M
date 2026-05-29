using System.Linq;
using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Commands.Data.Internal;
using CS2M.Mods;
using CS2M.Networking;
using CS2M.Util;
using LiteNetLib;
using System.Collections.Concurrent;
using System.Threading;

namespace CS2M.Commands.Handler.Internal
{
    /// <summary>
    ///     Enhanced preconditions check handler with improved error handling and validation
    /// </summary>
    public class PreconditionsCheckHandler : CommandHandler<PreconditionsCheckCommand>
    {
        // Thread-safe counter for concurrent connections
        private static readonly ConcurrentBag<string> _usernameLog = new();
        
        public PreconditionsCheckHandler()
        {
            TransactionCmd = false;
        }

        protected override void Handle(PreconditionsCheckCommand command)
        {
            // This should only be called server-side
        }

        public void HandleOnServer(PreconditionsCheckCommand command, NetPeer peer)
        {
            try
            {
                Log.Debug($"Received Preconditions Check [PeerId: {peer.Id}]");

                // Validate basic fields before processing
                if (string.IsNullOrWhiteSpace(command.Username))
                {
                    Log.Warn("Preconditions check failed: empty username");
                    RejectWithValidation(peer);
                    return;
                }

                // Basic username length check
                if (command.Username.Length > 64)
                {
                    Log.Warn($"Preconditions check failed: username too long ({command.Username.Length} chars)");
                    RejectWithValidation(peer);
                    return;
                }

                PreconditionsUtil.Result result = PreconditionsUtil.CheckPreconditions(command);

                // Check duplicate usernames on connection list
                lock (_usernameLog)
                {
                    foreach (var connectedPlayer in NetworkInterface.Instance.PlayerListConnected)
                    {
                        if (connectedPlayer.Username.Equals(command.Username, System.StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Debug($"[Preconditions Check] Username '{command.Username}' is already connected.");
                            result.Errors |= PreconditionsUtil.Errors.USERNAME_NOT_AVAILABLE;
                        }
                    }
                }

                // Validate server password if set
                string serverPassword = NetworkInterface.Instance.LocalPlayer.GetConnectionPassword();
                if (!string.IsNullOrEmpty(serverPassword))
                {
                    if (string.IsNullOrEmpty(command.Password) || !serverPassword.Equals(command.Password))
                    {
                        Log.Debug("[Preconditions Check] Password is not correct.");
                        result.Errors |= PreconditionsUtil.Errors.PASSWORD_INCORRECT;
                    }
                }

                if (result.Errors == PreconditionsUtil.Errors.NONE)
                {
                    Log.Info($"Preconditions check passed for user '{command.Username}' from peer {peer.Id}");
                    
                    // Send success response
                    NetworkInterface.Instance.LocalPlayer.SendToClient(peer, new PreconditionsSuccessCommand());

                    // Register player as connected
                    var remotePlayer = new RemotePlayer(peer, command.Username, PlayerType.CLIENT);
                    NetworkInterface.Instance.PlayerConnected(remotePlayer);
                    
                    _usernameLog.Add(command.Username.ToLower());
                }
                else
                {
                    Log.Warn($"Preconditions check failed for peer {peer.Id}: {result.Errors}");
                    
                    // Send detailed error information
                    NetworkInterface.Instance.LocalPlayer.SendToClient(peer, new PreconditionsErrorCommand()
                    {
                        Errors = result.Errors,
                        ModVersion = VersionUtil.GetModVersion(),
                        GameVersion = VersionUtil.GetGameVersion(),
                        Mods = ModSupport.Instance.RequiredModsForSync,
                        DlcIds = DlcCompat.RequiredDLCsForSync,
                    });
                    
                    // Disconnect on critical errors
                    if (result.Errors.HasFlag(PreconditionsUtil.Errors.PASSWORD_INCORRECT))
                    {
                        Log.Info($"Disconnecting peer {peer.Id} due to incorrect password");
                        peer.Disconnect();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Preconditions check failed with exception: {ex.Message}", ex);
                RejectWithValidation(peer);
            }
        }

        private void RejectWithValidation(NetPeer peer)
        {
            Log.Warn($"Rejecting connection for peer {peer.Id} due to validation failure");
            
            NetworkInterface.Instance.LocalPlayer.SendToClient(peer, new PreconditionsErrorCommand
            {
                Errors = PreconditionsUtil.Errors.INVALID_USERNAME | PreconditionsUtil.Errors.INVALID_FORMAT
            });
            
            // Allow a moment for error message before disconnect
            ThreadPool.QueueUserWorkItem(_ => 
            {
                System.Threading.Thread.Sleep(500);
                peer.Disconnect();
            });
        }
    }
}
