using CS2M.API.Commands;
using MessagePack;
using System;
using System.Collections.Generic;

namespace CS2M.Commands.Data.Internal
{
    /// <summary>
    ///     Send chat messages to other players in game
    /// </summary>
    [MessagePackObject]
    public class ChatMessageCommand : CommandBase
    {
        /// <summary>
        ///     The username for the message sender
        /// </summary>
        [Key(0)]
        public string Username { get; set; } = "";

        /// <summary>
        ///     The message sent by the user
        /// </summary>
        [Key(1)]
        public string Message { get; set; } = "";
        
        /// <summary>
        ///     Message type (0=normal, 1=system, 2=whisper)
        /// </summary>
        [Key(2)]
        public byte MessageType { get; set; }
        
        /// <summary>
        ///     Timestamp when message was created
        /// </summary>
        [Key(3)]
        public new long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        /// <summary>
        ///     Target player ID if whisper message
        /// </summary>
        [Key(4)]
        public int? TargetPlayerId { get; set; }
        
        /// <summary>
        ///     Validate message content before sending
        /// </summary>
        public override bool Validate()
        {
            // Basic length validation
            if (string.IsNullOrWhiteSpace(Message))
                return false;
            
            if (Message.Length > 500)
                return false;
            
            // Validate username
            if (string.IsNullOrWhiteSpace(Username) || Username.Length > 64)
                return false;
            
            // Check for invalid characters in message
            foreach (char c in Message)
            {
                if ((int)c < 32 && c != '\t' && c != '\n' && c != '\r')
                    return false;
            }
            
            // Check for spam patterns (repeated characters)
            if (IsSpamPattern(Message))
                return false;
            
            return true;
        }
        
        /// <summary>
        ///     Simple spam detection - checks for repeated character sequences
        /// </summary>
        private bool IsSpamPattern(string message)
        {
            // Count consecutive identical characters
            int consecutiveCount = 1;
            char lastChar = '\0';
            
            foreach (char c in message.ToLower())
            {
                if (c == lastChar)
                {
                    consecutiveCount++;
                    if (consecutiveCount >= 5)
                        return true;
                }
                else
                {
                    consecutiveCount = 1;
                    lastChar = c;
                }
            }
            
            return false;
        }
    }
    
    /// <summary>
    ///     Struct representing formatted chat message data for UI display
    /// </summary>
    [Serializable]
    public struct ChatMessageData
    {
        public string User;
        public string Text;
        public string Timestamp;
        public DateTime CreatedAt;
        public bool IsFromMe;
        public MessageTypeEnum Type;
        
        public static ChatMessageData FromCommand(ChatMessageCommand cmd)
        {
            var formattedTime = DateTimeOffset.FromUnixTimeMilliseconds(cmd.Timestamp).UtcDateTime;
            
            return new ChatMessageData
            {
                User = cmd.Username ?? "Unknown",
                Text = cmd.Message,
                Timestamp = formattedTime.ToString("HH:mm:ss"),
                CreatedAt = formattedTime,
                IsFromMe = false, // Will be determined by caller
                Type = GetMessageTypeEnum(cmd.MessageType)
            };
        }
        
        private static MessageTypeEnum GetMessageTypeEnum(byte typeByte)
        {
            switch (typeByte)
            {
                case 0: return MessageTypeEnum.Normal;
                case 1: return MessageTypeEnum.System;
                case 2: return MessageTypeEnum.Whisper;
                default: return MessageTypeEnum.Normal;
            }
        }
    }
    
    /// <summary>
    ///     Enum for different chat message types
    /// </summary>
    public enum MessageTypeEnum
    {
        Normal,
        System,
        Whisper,
        OOC // Out of Character
    }
}
