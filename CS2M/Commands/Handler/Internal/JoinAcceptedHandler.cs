using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.Networking;

namespace CS2M.Commands.Handler.Internal
{
    public class JoinAcceptedHandler : CommandHandler<JoinAcceptedCommand>
    {
        public JoinAcceptedHandler()
        {
        }

        protected override void Handle(JoinAcceptedCommand command)
        {
            bool transitioned = NetworkInterface.Instance.LocalPlayer.DownloadingMap();
            if (!transitioned)
            {
                Log.Warn("Received JoinAcceptedCommand in unexpected local state.");
            }
        }
    }
}
