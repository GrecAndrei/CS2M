using CS2M.API.Commands;
using MessagePack;
using System;
using Game.Simulation;
using Unity.Entities;
using CS2M.BaseGame.Systems;
using UnityEngine;

namespace CS2M.BaseGame.Commands
{
    /// <summary>
    ///     Command for synchronizing money values across multiplayer clients
    /// </summary>
    [MessagePackObject]
    public class MoneyCommand : CommandBase
    {
        /// <summary>
        ///     Current money amount
        /// </summary>
        [Key(0)]
        public long Money { get; set; }
        
        /// <summary>
        ///     Authority epoch for versioning
        /// </summary>
        [Key(1)]
        public uint AuthorityEpoch { get; set; }
        
        /// <summary>
        ///     Timestamp when this value was recorded
        /// </summary>
        [Key(2)]
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        /// <summary>
        ///     Validate that money value is reasonable
        /// </summary>
        public override bool Validate()
        {
            // Cannot be negative
            if (Money < 0)
                return false;
            
            // Maximum reasonable money value (1 trillion cap)
            const long MAX_MONEY = 1_000_000_000_000L;
            if (Money > MAX_MONEY)
                return false;
            
            // Timestamp must be recent (within last 60 seconds)
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Math.Abs(now - Timestamp) > 60000)
                return false;
            
            return true;
        }
    }
}
