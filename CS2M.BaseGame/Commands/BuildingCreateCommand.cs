using CS2M.API.Commands;
using MessagePack;
using UnityEngine;

namespace CS2M.BaseGame.Commands
{
    /// <summary>
    ///     Command for creating buildings in the game world
    /// </summary>
    [MessagePackObject]
    public class BuildingCreateCommand : CommandBase
    {
        /// <summary>
        ///     Name of the building prefab to create
        /// </summary>
        [Key(0)]
        public string PrefabName { get; set; } = "";
        
        /// <summary>
        ///     X position coordinate
        /// </summary>
        [Key(1)]
        public float PositionX { get; set; }
        
        /// <summary>
        ///     Y position coordinate
        /// </summary>
        [Key(2)]
        public float PositionY { get; set; }
        
        /// <summary>
        ///     Z position coordinate (height)
        /// </summary>
        [Key(3)]
        public float PositionZ { get; set; }
        
        /// <summary>
        ///     Rotation quaternion components
        /// </summary>
        [Key(4)]
        public float RotationX { get; set; }
        
        [Key(5)]
        public float RotationY { get; set; }
        
        [Key(6)]
        public float RotationZ { get; set; }
        
        [Key(7)]
        public float RotationW { get; set; }
        
        /// <summary>
        ///     Random seed for procedural variation
        /// </summary>
        [Key(8)]
        public int RandomSeed { get; set; }
        
        /// <summary>
        ///     Unique placement nonce for deduplication
        /// </summary>
        [Key(9)]
        public int PlacementNonce { get; set; }
        
        /// <summary>
        ///     If true, only request permission without executing
        /// </summary>
        [Key(10)]
        public bool RequestOnly { get; set; }
        
        /// <summary>
        ///     Client token for session tracking
        /// </summary>
        [Key(11)]
        public ulong ClientToken { get; set; }
        
        /// <summary>
        ///     Validate command data before execution
        /// </summary>
        public override bool Validate()
        {
            // Validate prefab name
            if (string.IsNullOrWhiteSpace(PrefabName) || PrefabName.Length > 128)
                return false;
            
            // Validate coordinates are within reasonable range
            const float MAX_COORD = 5000f;
            if (PositionX < -MAX_COORD || PositionX > MAX_COORD)
                return false;
            
            if (PositionY < -MAX_COORD || PositionY > MAX_COORD)
                return false;
            
            // Validate rotation is valid quaternion
            float magnitude = Mathf.Sqrt(RotationX * RotationX + 
                                         RotationY * RotationY + 
                                         RotationZ * RotationZ + 
                                         RotationW * RotationW);
            if (magnitude < 0.9f || magnitude > 1.1f)
                return false;
            
            return true;
        }
    }
    
    /// <summary>
    ///     Utility extension for Vector3/Quaternion conversions
    /// </summary>
    internal static class TransformExtensions
    {
        public static Unity.Mathematics.float3 ToFloat3(this BuildingCreateCommand cmd)
        {
            return new Unity.Mathematics.float3(cmd.PositionX, cmd.PositionY, cmd.PositionZ);
        }
        
        public static Unity.Mathematics.quaternion ToQuaternion(this BuildingCreateCommand cmd)
        {
            return new Unity.Mathematics.quaternion(cmd.RotationX, cmd.RotationY, cmd.RotationZ, cmd.RotationW);
        }
    }
}
