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
            return true;
        }
    }
}
