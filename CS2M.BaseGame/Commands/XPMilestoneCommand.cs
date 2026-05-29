using CS2M.API.Commands;
using CS2M.BaseGame.Systems;
using Unity.Entities;

namespace CS2M.BaseGame.Commands
{
    public class XPMilestoneCommand : CommandBase
    {
        public int XP { get; set; }
        public int NextMilestone { get; set; }
    }

    public class XPMilestoneCommandHandler : CommandHandler<XPMilestoneCommand>
    {
        protected override void Handle(XPMilestoneCommand command)
        {
            if (Command.CurrentRole != MultiplayerRole.Client || command == null)
            {
                return;
            }

            var xpMilestoneSyncSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<XPMilestoneSyncSystem>();
            if (xpMilestoneSyncSystem != null)
            {
                xpMilestoneSyncSystem.SetValues(command.XP, command.NextMilestone);
            }
        }
    }
}
