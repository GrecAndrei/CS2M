using CS2M.API.Commands;
using MessagePack;
using System;
using Game.Simulation;
using Unity.Entities;
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
        public new long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
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

            return true;
        }
    }
}
