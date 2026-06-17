using CS2M.API.Commands;
using MessagePack;

namespace CS2M.BaseGame.Commands
{
    /// <summary>
    ///     MessagePack-serializable command for real-time 3D cursor, active tool, and camera focus tracking.
    /// </summary>
    [MessagePackObject]
    public class PlayerCursorCommand : CommandBase
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
        public string ActiveTool { get; set; } = "";

        [Key(6)]
        public string ActivePrefab { get; set; } = "";

        [Key(7)]
        public float CameraFocusX { get; set; }

        [Key(8)]
        public float CameraFocusY { get; set; }

        [Key(9)]
        public float CameraFocusZ { get; set; }

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
            if (ActiveTool != null && ActiveTool.Length > 128)
            {
                return false;
            }
            if (ActivePrefab != null && ActivePrefab.Length > 128)
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

            if (CameraFocusX < -MAX_COORD || CameraFocusX > MAX_COORD)
            {
                return false;
            }
            if (CameraFocusY < -MAX_COORD || CameraFocusY > MAX_COORD)
            {
                return false;
            }
            if (CameraFocusZ < -MAX_COORD || CameraFocusZ > MAX_COORD)
            {
                return false;
            }

            return true;
        }
    }
}
