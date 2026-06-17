using CS2M.API.Commands;
using MessagePack;

namespace CS2M.BaseGame.Commands
{
    /// <summary>
    ///     MessagePack-serializable command for real-time 3D map terrain pinging.
    /// </summary>
    [MessagePackObject]
    public class MapPingCommand : CommandBase
    {
        [Key(0)]
        public int TargetPlayerId { get; set; }

        [Key(1)]
        public string TargetUsername { get; set; } = "";

        [Key(2)]
        public float PositionX { get; set; }

        [Key(3)]
        public float PositionY { get; set; }

        [Key(4)]
        public float PositionZ { get; set; }

        [Key(5)]
        public int PingType { get; set; } // 0 = General, 1 = Danger, 2 = Build

        public override bool Validate()
        {
            if (TargetPlayerId < 0)
            {
                return false;
            }

            if (TargetUsername != null && TargetUsername.Length > 128)
            {
                return false;
            }

            const float MAX_COORD = 50000f;
            if (PositionX < -MAX_COORD || PositionX > MAX_COORD)
            {
                return false;
            }
            if (PositionY < -MAX_COORD || PositionY > MAX_COORD)
            {
                return false;
            }
            if (PositionZ < -MAX_COORD || PositionZ > MAX_COORD)
            {
                return false;
            }

            if (PingType < 0 || PingType > 2)
            {
                return false;
            }

            return true;
        }
    }
}
