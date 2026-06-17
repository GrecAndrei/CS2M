using CS2M.API.Commands;

namespace CS2M.BaseGame.Commands
{
    public class AreaControlPointSnapshot
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float HitPositionX { get; set; }
        public float HitPositionY { get; set; }
        public float HitPositionZ { get; set; }
        public float DirectionX { get; set; }
        public float DirectionY { get; set; }
        public float HitDirectionX { get; set; }
        public float HitDirectionY { get; set; }
        public float HitDirectionZ { get; set; }
        public float RotationX { get; set; }
        public float RotationY { get; set; }
        public float RotationZ { get; set; }
        public float RotationW { get; set; }
        public float CurvePosition { get; set; }
        public float Elevation { get; set; }
        public int ElementIndexX { get; set; }
        public int ElementIndexY { get; set; }
        public float SnapPriorityX { get; set; }
        public float SnapPriorityY { get; set; }
        public int OriginalEntityIndex { get; set; }
        public int OriginalEntityVersion { get; set; }
    }

    public class AreaApplyCommand : CommandBase
    {
        public string PrefabName { get; set; }
        public int Mode { get; set; }
        public bool Underground { get; set; }
        public bool AllowGenerate { get; set; }
        public int PreApplyState { get; set; }
        public bool SingleFrameOnly { get; set; }
        public int ApplyNonce { get; set; }
        public bool RequestOnly { get; set; }
        public AreaControlPointSnapshot[] ControlPoints { get; set; }

        public override bool Validate()
        {
            if (string.IsNullOrWhiteSpace(PrefabName) || PrefabName.Length > 128)
            {
                return false;
            }

            if (Mode < 0 || Mode > 4)
            {
                return false;
            }

            if (ControlPoints == null || ControlPoints.Length == 0 || ControlPoints.Length > 1024)
            {
                return false;
            }

            if (ApplyNonce == 0)
            {
                return false;
            }

            return true;
        }
    }
}
