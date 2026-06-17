using CS2M.API.Commands;
using MessagePack;

namespace CS2M.BaseGame.Commands
{
    /// <summary>
    ///     Command for placing modular service upgrades on existing buildings
    /// </summary>
    [MessagePackObject]
    public class ModularUpgradeCommand : CommandBase
    {
        [Key(0)]
        public int ParentEntityIndex { get; set; }

        [Key(1)]
        public int ParentEntityVersion { get; set; }

        [Key(2)]
        public string UpgradePrefabName { get; set; } = "";

        [Key(3)]
        public float PositionX { get; set; }

        [Key(4)]
        public float PositionY { get; set; }

        [Key(5)]
        public float PositionZ { get; set; }

        [Key(6)]
        public float RotationX { get; set; }

        [Key(7)]
        public float RotationY { get; set; }

        [Key(8)]
        public float RotationZ { get; set; }

        [Key(9)]
        public float RotationW { get; set; }

        [Key(10)]
        public int UpgradeNonce { get; set; }

        [Key(11)]
        public bool RequestOnly { get; set; }

        public override bool Validate() => true;
    }
}
