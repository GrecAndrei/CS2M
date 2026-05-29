using System;
using System.Collections.Generic;
using LiteNetLib;
using CS2M.API;

namespace CS2M.API.Commands
{
    /// <summary>
    ///     Base class for all command handlers with proper error handling and logging
    /// </summary>
    public abstract class CommandHandler
    {
        /// <summary>
        ///     Type of command this handler processes
        /// </summary>
        public abstract Type CommandType { get; }

        /// <summary>
        ///     Indicates if this command represents a transaction that needs tracking
        /// </summary>
        public bool TransactionCmd { get; set; } = true;

        /// <summary>
        ///     Indicates if this command should be automatically relayed by the server to other clients
        /// </summary>
        public bool RelayOnServer { get; set; } = true;

        /// <summary>
        ///     Generic parse/execution entry point for compatability
        /// </summary>
        public virtual void Parse(BaseCommand command, NetPeer peer = null)
        {
            try
            {
                InternalHandle(command);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to parse/handle command {command.GetType().Name}: {ex.Message}");
                Log.Trace(ex.ToString());
            }
        }

        /// <summary>
        ///     Handles the command on the client side
        /// </summary>
        public virtual void Handle(ClientCommand command, NetPeer peer = null)
        {
            try
            {
                InternalHandle(command);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to handle command {command.GetType().Name}: {ex.Message}");
                Log.Trace(ex.ToString());
            }
        }

        /// <summary>
        ///     Handles the command on the server side
        /// </summary>
        public virtual void HandleOnServer(ServerCommand command, NetPeer peer = null)
        {
            try
            {
                // Validate sender
                if (!ValidateSender(command, peer))
                {
                    Log.Warn($"Invalid sender for command {command.GetType().Name} from peer {peer?.Id}");
                    return;
                }

                InternalHandle(command);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to handle command {command.GetType().Name} on server: {ex.Message}");
                Log.Trace(ex.ToString());
            }
        }

        /// <summary>
        ///     Core handler implementation - override this in derived classes
        /// </summary>
        protected abstract void InternalHandle(BaseCommand command);

        /// <summary>
        ///     Validates the sender is authorized to execute this command
        /// </summary>
        protected virtual bool ValidateSender(ServerCommand command, NetPeer peer)
        {
            return true;
        }

        /// <summary>
        ///     Logs debug information about the command being handled
        /// </summary>
        protected void LogDebug(BaseCommand command, NetPeer peer = null)
        {
            string peerInfo = peer != null ? $"from peer {peer.Id}" : "";
            Log.Debug($"Handling {command.CommandType} {peerInfo}");
        }

        /// <summary>
        ///     Gets the command type name for debugging
        /// </summary>
        protected string GetCommandTypeName(BaseCommand command)
        {
            return command.CommandType ?? command.GetType().Name;
        }
    }

    /// <summary>
    ///     Generic handler for type-specific commands
    /// </summary>
    public abstract class CommandHandler<TCommand> : CommandHandler where TCommand : BaseCommand
    {
        public override Type CommandType => typeof(TCommand);

        protected sealed override void InternalHandle(BaseCommand command)
        {
            if (command is TCommand typedCommand)
            {
                Handle(typedCommand);
            }
            else
            {
                Log.Error($"Command type mismatch: expected {typeof(TCommand).Name}, got {command.GetType().Name}");
            }
        }

        /// <summary>
        ///     Override this to handle your specific command type
        /// </summary>
        protected abstract void Handle(TCommand command);
    }

    /// <summary>
    ///     Client-side handler
    /// </summary>
    public abstract class ClientCommandHandler<TCommand> : CommandHandler<TCommand> where TCommand : BaseCommand
    {
        protected sealed override void Handle(TCommand command)
        {
            if (!command.Validate())
            {
                Log.Warn($"Client command validation failed: {command.CommandType}");
                return;
            }

            OnValidatedCommand(command);
        }

        /// <summary>
        ///     Called after validation passes
        /// </summary>
        protected abstract void OnValidatedCommand(TCommand command);
    }

    /// <summary>
    ///     Server-side handler
    /// </summary>
    public abstract class ServerCommandHandler<TCommand> : CommandHandler<TCommand> where TCommand : BaseCommand
    {
        protected override bool ValidateSender(ServerCommand command, NetPeer peer)
        {
            return peer != null && IsAuthorized(peer);
        }

        /// <summary>
        ///     Check if a peer is authorized to send this command
        /// </summary>
        protected abstract bool IsAuthorized(NetPeer peer);

        protected sealed override void Handle(TCommand command)
        {
            if (!command.Validate())
            {
                Log.Warn($"Server command validation failed: {command.CommandType}");
                return;
            }

            OnValidatedCommand(command);
        }

        /// <summary>
        ///     Called after validation passes
        /// </summary>
        protected abstract void OnValidatedCommand(TCommand command);
    }
}
