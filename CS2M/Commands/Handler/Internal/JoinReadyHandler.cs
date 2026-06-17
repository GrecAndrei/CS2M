using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.Networking;
using LiteNetLib;

namespace CS2M.Commands.Handler.Internal
{
    public class JoinReadyHandler : CommandHandler<JoinReadyCommand>
    {
        public JoinReadyHandler()
        {
        }

        protected override void Handle(JoinReadyCommand command)
        {
            // JoinReadyCommand is only handled with peer context via HandleOnServer.
        }

        public void HandleOnServer(JoinReadyCommand command, NetPeer peer)
        {
            if (NetworkInterface.Instance.LocalPlayer.PlayerType != CS2M.API.Networking.PlayerType.SERVER)
            {
                return;
            }

            bool marked = NetworkInterface.Instance.PlayerJoined(peer);
            if (!marked)
            {
                Log.Warn($"Failed to mark peer {peer?.Id} as joined.");
            }
        }
    }
}
