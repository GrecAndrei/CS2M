using CS2M.API;
using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.Networking;
using CS2M.Commands;

namespace CS2M.Commands.Handler.Internal
{
    public class ChatMessageHandler : CommandHandler<ChatMessageCommand>
    {
        public ChatMessageHandler()
        {
        }

        protected override void Handle(ChatMessageCommand command)
        {
            // Don't show our own messages again (echo)
            if (command.SenderId == NetworkInterface.Instance.LocalPlayer.PlayerId)
            {
                return;
            }

            Chat.Instance.PrintChatMessage(command.Username, command.Message);

            // Relay to all other clients if we are the server
            if (NetworkInterface.Instance.LocalPlayer.PlayerType ==  CS2M.API.Networking.PlayerType.SERVER)
            {
                CommandInternal.Instance.SendToClients(command);
            }
        }
    }
}
