using System;
#pragma warning disable MsgPack005
using LiteNetLib;
using MessagePack;
using UnityEngine;

namespace CS2M.API.Networking
{
    /// <summary>
    ///     Base player class with comprehensive state tracking
    /// </summary>
    [MessagePackObject]
    public abstract class Player
    {
        private PlayerStatus _playerStatus = PlayerStatus.INACTIVE;
        
        private PlayerType _playerType = PlayerType.NONE;
        
        /// <summary>
        ///     Unique player identifier (-1 for server)
        /// </summary>
        [Key(0)]
        public int PlayerId { get; set; }
        
        /// <summary>
        ///     Display name/username
        /// </summary>
        [Key(1)]
        public string Username { get; protected set; } = "";
        
        /// <summary>
        ///     Last known latency in milliseconds
        /// </summary>
        [Key(2)]
        public long Latency { get; set; }
        
        /// <summary>
        ///     Current connection status
        /// </summary>
        [IgnoreMember]
        public PlayerStatus PlayerStatus
        {
            get => _playerStatus;
            protected set
            {
                if (_playerStatus != value)
                {
                    var oldStatus = _playerStatus;
                    _playerStatus = value;
                    PlayerStatusChangedEvent?.Invoke(oldStatus, value);
                }
            }
        }
        
        /// <summary>
        ///     Current role type (client/server)
        /// </summary>
        [IgnoreMember]
        public PlayerType PlayerType
        {
            get => _playerType;
            protected set
            {
                if (_playerType != value)
                {
                    var oldType = _playerType;
                    _playerType = value;
                    PlayerTypeChangedEvent?.Invoke(oldType, value);
                }
            }
        }
        
        /// <summary>
        ///     Session token for identification
        /// </summary>
        [Key(3)]
        public ulong SessionToken { get; set; } = (ulong)(uint)Guid.NewGuid().GetHashCode();
        
        /// <summary>
        ///     Timestamp of last activity
        /// </summary>
        [Key(4)]
        public long LastActivityTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        /// <summary>
        ///     Client version info
        /// </summary>
        [Key(5)]
        public string ClientVersion { get; set; } = "";
        
        /// <summary>
        ///     Number of bytes sent total
        /// </summary>
        [Key(6)]
        public long TotalBytesSent { get; set; }
        
        /// <summary>
        ///     Number of bytes received total
        /// </summary>
        [Key(7)]
        public long TotalBytesReceived { get; set; }
        
        #region Events
        
        /// <summary>
        ///     Fired when player status changes
        /// </summary>
        public event OnPlayerStatusChanged PlayerStatusChangedEvent;
        
        /// <summary>
        ///     Fired when player type changes
        /// </summary>
        public event OnPlayerTypeChanged PlayerTypeChangedEvent;
        
        /// <summary>
        ///     Fired on any state change
        /// </summary>
        public event Action<Player> StateChangedEvent;
        
        #endregion
        
        #region Type Definitions
        
        public delegate void OnPlayerStatusChanged(PlayerStatus oldStatus, PlayerStatus newStatus);
        public delegate void OnPlayerTypeChanged(PlayerType oldType, PlayerType newType);
        
        #endregion
        
        /// <summary>
        ///     Initialize player with basic info
        /// </summary>
        public Player()
        {
            LastActivityTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        
        /// <summary>
        ///     Update timestamp of last activity
        /// </summary>
        public void UpdateActivity()
        {
            LastActivityTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        
        /// <summary>
        ///     Check if player is active (recently active within last minute)
        /// </summary>
        public bool IsActive(int timeoutMs = 60000)
        {
            return (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - LastActivityTime) < timeoutMs;
        }
        
        /// <summary>
        ///     Get formatted uptime string
        /// </summary>
        public string GetUptimeString()
        {
            var uptime = DateTime.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(LastActivityTime).UtcDateTime;
            return uptime.ToString(@"d\days h\:mm\:ss");
        }
        
        /// <summary>
        ///     Convert to summary data structure
        /// </summary>
        public PlayerSummary ToSummary()
        {
            return new PlayerSummary
            {
                PlayerId = PlayerId,
                Username = Username,
                PlayerType = PlayerType,
                PlayerStatus = PlayerStatus,
                Latency = (int)Math.Min(Latency, int.MaxValue),
                IsActive = IsActive(),
                SessionToken = SessionToken
            };
        }
    }
    
    /// <summary>
    ///     Summary data for UI display
    /// </summary>
    [Serializable]
    public struct PlayerSummary
    {
        public int PlayerId;
        public string Username;
        public PlayerType PlayerType;
        public PlayerStatus PlayerStatus;
        public int Latency;
        public bool IsActive;
        public ulong SessionToken;
    }
}
