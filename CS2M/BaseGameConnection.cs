using CS2M.API;
using CS2M.BaseGame.Commands;

namespace CS2M
{
    public class BaseGameConnection : ModConnection
    {
        public BaseGameConnection()
        {
            Name = "Cities: Skylines II";
            Enabled = true;
            ModClass = null;
            CommandAssemblies.Add(typeof(MoneyCommand).Assembly);
        }

        public override void RegisterHandlers()
        {
            
        }

        public override void UnregisterHandlers()
        {
            
        }
    }
}
