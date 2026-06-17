using CS2M.API.Commands;
using CS2M.BaseGame;
using CS2M.BaseGame.Systems;
using Unity.Entities;
using UnityEngine;

namespace CS2M.BaseGame.Commands
{
    public class SpeedCommand : CommandBase
    {
        public float Speed { get; set; }
        public bool Paused { get; set; }

        public override bool Validate() => true;
    }

    public class SpeedCommandHandler : CommandHandler<SpeedCommand>
    {
        private const int SummaryInterval = 20;
        private static int _correctionCount;
        private static float _totalCorrectionDelta;

        protected override void Handle(SpeedCommand command)
        {
            if (Command.CurrentRole != MultiplayerRole.Client)
            {
                return;
            }

            var simulationSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
            if (simulationSystem != null)
            {
                float targetSpeed = command.Paused ? 0f : (command.Speed == 0f ? 1f : command.Speed);
                float delta = Mathf.Abs(simulationSystem.selectedSpeed - targetSpeed);
                if (delta >= 0.01f)
                {
                    _correctionCount++;
                    _totalCorrectionDelta += delta;

                    Debug.Log(
                        $"[CS2M Sync] Speed drift correction: local={simulationSystem.selectedSpeed}, authoritative={targetSpeed}");

                    if (_correctionCount % SummaryInterval == 0)
                    {
                        float avgDelta = _totalCorrectionDelta / _correctionCount;
                        Debug.Log(
                            $"[CS2M Sync] Speed correction summary: count={_correctionCount}, avgDelta={avgDelta:0.###}");
                    }
                }
            }

            var timeSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TimeSystem>();
            if (timeSystem != null)
            {
                timeSystem.SetSpeed(command.Speed, command.Paused);
            }
        }
    }
}
