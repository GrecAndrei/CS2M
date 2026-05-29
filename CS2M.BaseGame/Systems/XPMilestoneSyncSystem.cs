using Game;
using Game.Simulation;
using Unity.Entities;
using CS2M.API.Commands;
using CS2M.BaseGame.Commands;

namespace CS2M.BaseGame.Systems
{
    public partial class XPMilestoneSyncSystem : GameSystemBase
    {
        private const int AuthorityBroadcastInterval = 60; // Sample/Broadcast every 60 frames (~1 sec)

        private CitySystem _citySystem;
        private MilestoneSystem _milestoneSystem;
        private int _lastXP;
        private int _lastMilestone;
        private bool _isApplying;
        private int _authorityBroadcastCounter;

        protected override void OnCreate()
        {
            base.OnCreate();
            _citySystem = World.GetOrCreateSystemManaged<CitySystem>();
            _milestoneSystem = World.GetOrCreateSystemManaged<MilestoneSystem>();
        }

        protected override void OnUpdate()
        {
            if (Command.CurrentRole != MultiplayerRole.Server)
                return;

            int currentXP = _citySystem.XP;
            int currentMilestone = _milestoneSystem.nextMilestone;

            bool xpChanged = !_isApplying && currentXP != _lastXP;
            bool milestoneChanged = !_isApplying && currentMilestone != _lastMilestone;

            _authorityBroadcastCounter++;
            bool periodicBroadcast = _authorityBroadcastCounter >= AuthorityBroadcastInterval;

            if (xpChanged || milestoneChanged || periodicBroadcast)
            {
                if (milestoneChanged && currentMilestone > _lastMilestone && _lastMilestone > 0)
                {
                    CS2M.API.CooperativeActivityRegistry.RegisterActivity(
                        "City",
                        $"Unlocked Milestone {currentMilestone - 1}!",
                        0f, 0f, 0f
                    );
                }

                _authorityBroadcastCounter = 0;
                _lastXP = currentXP;
                _lastMilestone = currentMilestone;

                Command.SendToAll(new XPMilestoneCommand
                {
                    XP = currentXP,
                    NextMilestone = currentMilestone
                });
            }

            _isApplying = false;
        }

        public void SetValues(int xp, int nextMilestone)
        {
            if (nextMilestone > _lastMilestone && _lastMilestone > 0)
            {
                CS2M.API.CooperativeActivityRegistry.RegisterActivity(
                    "City",
                    $"Unlocked Milestone {nextMilestone - 1}!",
                    0f, 0f, 0f
                );
            }

            _isApplying = true;
            _lastXP = xp;
            _lastMilestone = nextMilestone;

            _citySystem.SetPrivateField("m_XP", xp);
            _milestoneSystem.SetPrivateField("m_NextMilestone", nextMilestone);
        }
    }
}
