using CS2M.API.Commands;
using MessagePack;
using System;

namespace CS2M.Commands.Data.Internal
{
    /// <summary>
    ///     Sent by client after preconditions pass, requesting gameplay join
    /// </summary>
    [MessagePackObject]
    public class JoinRequestCommand : CommandBase
    {
        /// <summary>
        ///     Client's username
        /// </summary>
        [Key(0)]
        public string Username { get; set; } = "";
        
        /// <summary>
        ///     Client's mod version
        /// </summary>
        [Key(1)]
        public string ModVersion { get; set; } = "";
        
        /// <summary>
        ///     Client's game version
        /// </summary>
        [Key(2)]
        public string GameVersion { get; set; } = "";
        
        /// <summary>
        ///     List of installed required mods
        /// </summary>
        [Key(3)]
        public string[] RequiredMods { get; set; } = Array.Empty<string>();
        
        /// <summary>
        ///     List of installed DLCs
        /// </summary>
        [Key(4)]
        public int[] InstalledDLCs { get; set; } = Array.Empty<int>();
        
        /// <summary>
        ///     Client token for session management
        /// </summary>
        [Key(5)]
        public ulong ClientToken { get; set; }
        
        /// <summary>
        ///     Connection timestamp (for debugging)
        /// </summary>
        [Key(6)]
        public long ConnectionTimestamp { get; set; }
        
        public override bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Username) || Username.Length > 64)
                return false;
            
            if (string.IsNullOrWhiteSpace(ModVersion))
                return false;
            
            if (ConnectionTimestamp <= 0)
                return false;
            
            // Basic security check - no null bytes or control characters
            foreach (char c in Username)
            {
                if ((int)c < 32 && c != '\t' && c != '\n' && c != '\r')
                    return false;
            }
            
            return true;
        }
    }
}
