using System;

namespace CS2M.API.Commands
{
    /// <summary>
    ///     Base class for all multiplayer commands
    /// </summary>
    public abstract class BaseCommand
    {
        /// <summary>
        ///     Command type identifier for handler lookup
        /// </summary>
        public virtual string CommandType => GetType().Name;

        /// <summary>
        ///     Unique command ID (if applicable)
        /// </summary>
        public uint CommandId { get; set; }

        /// <summary>
        ///     Timestamp when command was created (for debugging and ordering)
        /// </summary>
        public long Timestamp { get; set; } = DateTime.UtcNow.Ticks;

        /// <summary>
        ///     Sender ID (-1 for server, >= 0 for clients)
        /// </summary>
        public int SenderId { get; set; } = -1;

        /// <summary>
        ///     Direction of the command routing
        /// </summary>
        public virtual CommandDirection Direction => CommandDirection.None;

        /// <summary>
        ///     Validates that this command can be processed
        /// </summary>
        public virtual bool Validate() => false;

        /// <summary>
        ///     Creates a shallow copy of this command
        /// </summary>
        public BaseCommand Clone() => (BaseCommand)MemberwiseClone();

        /// <summary>
        ///     Gets the canonical type name for serialization
        /// </summary>
        public string GetCommandType() => GetType().Name;
    }

    /// <summary>
    ///     Direction indicator for command routing
    /// </summary>
    public enum CommandDirection
    {
        None,
        ClientToServer,
        ServerToClient,
        Broadcast
    }

    /// <summary>
    ///     Base class for all server commands
    /// </summary>
    public abstract class ServerCommand : BaseCommand
    {
        public override CommandDirection Direction => CommandDirection.ClientToServer;
    }

    /// <summary>
    ///     Base class for all client commands
    /// </summary>
    public abstract class ClientCommand : BaseCommand
    {
        public override CommandDirection Direction => CommandDirection.ServerToClient;
    }

    /// <summary>
    ///     Base class for broadcast commands (sent to multiple clients)
    /// </summary>
    public abstract class BroadcastCommand : BaseCommand
    {
        public override CommandDirection Direction => CommandDirection.Broadcast;
    }
}
