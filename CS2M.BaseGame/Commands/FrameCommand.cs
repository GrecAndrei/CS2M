using CS2M.API.Commands;
using UnityEngine;
using Game.Simulation;
using Unity.Entities;
using CS2M.BaseGame.Systems;

namespace CS2M.BaseGame.Commands
{
    public class FrameCommand : CommandBase
    {
        public uint Frame { get; set; }
    }

    public class FrameCommandHandler : CommandHandler<FrameCommand>
    {
        private const int SummaryInterval = 20;
        private static int _correctionCount;
        private static long _totalCorrectionDelta;

        protected override void Handle(FrameCommand command)
        {
            if (Command.CurrentRole == MultiplayerRole.Client)
            {
                var simulationSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<SimulationSystem>();
                if (simulationSystem != null)
                {
                    uint localFrame = simulationSystem.frameIndex;
                    int delta = Mathf.Abs((int)localFrame - (int)command.Frame);
                    if (delta > 120)
                    {
                        _correctionCount++;
                        _totalCorrectionDelta += delta;

                        Debug.Log(
                            $"[CS2M Sync] Frame drift correction: local={localFrame}, authoritative={command.Frame}, delta={delta}");

                        if (_correctionCount % SummaryInterval == 0)
                        {
                            double avgDelta = (double)_totalCorrectionDelta / _correctionCount;
                            Debug.Log(
                                $"[CS2M Sync] Frame correction summary: count={_correctionCount}, avgDelta={avgDelta:0.##}");
                        }
                    }
                }

                var frameSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<FrameSyncSystem>();
                frameSystem?.ReceiveFrameUpdate(command);
            }
        }
    }
}
