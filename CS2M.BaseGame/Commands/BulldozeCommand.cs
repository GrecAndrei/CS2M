using CS2M.API.Commands;

namespace CS2M.BaseGame.Commands
{
    public class BulldozeCommand : CommandBase
    {
        public int TargetEntityIndex { get; set; }
        public int TargetEntityVersion { get; set; }
        public int BulldozeNonce { get; set; }
        public bool RequestOnly { get; set; }
        public bool IsMultiSelect { get; set; }
        public System.Collections.Generic.List<int> MultiTargetIndices { get; set; }
        public System.Collections.Generic.List<int> MultiTargetVersions { get; set; }

        public override bool Validate() => true;
    }
}
