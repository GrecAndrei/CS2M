using Game;
using Game.Simulation;
using Unity.Entities;
using UnityEngine;
using CS2M.API.Commands;
using CS2M.BaseGame.Commands;

namespace CS2M.BaseGame.Systems
{
    public partial class TimeSystem : GameSystemBase
    {
        private const int AuthorityBroadcastInterval = 120;

        private SimulationSystem _simulationSystem;
        private float _lastSpeed;
        private bool _isApplying;
        private int _authorityBroadcastCounter;

        protected override void OnCreate()
        {
            base.OnCreate();
            _simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
        }

        protected override void OnUpdate()
        {
            if (Command.CurrentRole != MultiplayerRole.Server)
                return;

            float currentSpeed = _simulationSystem.selectedSpeed;
            bool speedChanged = !_isApplying && currentSpeed != _lastSpeed;

            _authorityBroadcastCounter++;
            bool periodicBroadcast = _authorityBroadcastCounter >= AuthorityBroadcastInterval;

            if (speedChanged || periodicBroadcast)
            {
                _authorityBroadcastCounter = 0;
                _lastSpeed = currentSpeed;

                Command.SendToAll(new SpeedCommand
                {
                    Speed = currentSpeed,
                    Paused = currentSpeed == 0
                });
            }
            
            _isApplying = false;
        }

        public void SetSpeed(float speed, bool paused)
        {
            _isApplying = true;
            // If paused is true, we force speed to 0.
            // If paused is false and speed is 0, we default to 1 (normal speed).
            float targetSpeed = paused ? 0 : (speed == 0 ? 1 : speed);
            _simulationSystem.selectedSpeed = targetSpeed;
            _lastSpeed = targetSpeed;
        }
    }
}
