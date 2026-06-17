using CS2M.API.Commands;
using CS2M.BaseGame.Commands;
using CS2M.Systems;

namespace CS2M.Commands.Handler.BaseGame
{
    public class PlayerCursorCommandHandler : CommandHandler<PlayerCursorCommand>
    {
        public PlayerCursorCommandHandler()
        {
        }

        protected override void Handle(PlayerCursorCommand command)
        {
            if (command == null)
            {
                return;
            }

            switch (Command.CurrentRole)
            {
                case MultiplayerRole.Server:
                    // Server: Relay the cursor packet to all other clients
                    Command.SendToClients?.Invoke(command);
                    break;

                case MultiplayerRole.Client:
                    // Client: Register cursor position in CooperativeSyncSystem
                    CooperativeSyncSystem.UpdateRemoteCursor(command);
                    break;
            }
        }
    }
}
