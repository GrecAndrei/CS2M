using CS2M.API.Commands;
using MessagePack;

namespace CS2M.BaseGame.Commands
{
    /// <summary>
    ///     Command to synchronize Development Tree point purchases and tech unlocks
    /// </summary>
    [MessagePackObject]
    public class TechUnlockCommand : CommandBase
    {
        [Key(0)]
        public string NodePrefabName { get; set; } = "";

        [Key(1)]
        public int UnlockNonce { get; set; }

        [Key(2)]
        public bool RequestOnly { get; set; }

        public override bool Validate() => true;
    }
}
