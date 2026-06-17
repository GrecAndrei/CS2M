using CS2M.API.Commands;

namespace CS2M.BaseGame.Commands
{
    public class RoadControlPointSnapshot
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

    public class RoadApplyCommand : CommandBase
    {
        public string PrefabName { get; set; }
        public int Mode { get; set; }
        public float Elevation { get; set; }
        public int ParallelCount { get; set; }
        public float ParallelOffset { get; set; }
        public bool Underground { get; set; }
        public int SelectedSnap { get; set; }
        public bool UpgradeOnly { get; set; }
        public bool ServiceUpgrade { get; set; }
        public int PreApplyState { get; set; }
        public bool SingleFrameOnly { get; set; }
        public int ApplyNonce { get; set; }
        public int RandomSeed { get; set; }
        public bool RequestOnly { get; set; }
        public RoadControlPointSnapshot ApplyStartPoint { get; set; }
        public RoadControlPointSnapshot LastRaycastPoint { get; set; }
        public RoadControlPointSnapshot[] ControlPoints { get; set; }

        public override bool Validate() => true;
    }
}
