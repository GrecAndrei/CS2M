using CS2M.API.Commands;
using CS2M.BaseGame.Commands;
using CS2M.Systems;

namespace CS2M.Commands.Handler.BaseGame
{
    public class MapPingCommandHandler : CommandHandler<MapPingCommand>
    {
        public MapPingCommandHandler()
        {
        }

        protected override void Handle(MapPingCommand command)
        {
            if (command == null)
            {
                return;
            }

            switch (Command.CurrentRole)
            {
                case MultiplayerRole.Server:
                    // Server: Relay the map ping to all clients
                    Command.SendToClients?.Invoke(command);
                    break;

                case MultiplayerRole.Client:
                    // Client: Trigger the visual beacon and sound in CooperativeSyncSystem
                    CooperativeSyncSystem.TriggerRemotePing(command);
                    break;
            }
        }
    }
}
