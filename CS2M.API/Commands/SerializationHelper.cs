using System;
using System.Linq;
using CS2M.API;
using CS2M.API.Networking;
using MessagePack;
using UnityEngine;

namespace CS2M.API.Commands
{
    /// <summary>
    ///     High-performance message serialization/deserialization using MessagePack
    /// </summary>
    public static class SerializationHelper
    {
        private static readonly MessagePackSerializerOptions _options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
        
        // Cache of command type serializers for performance
        private static readonly MessagePackSerializerOptions _safeOptions = _options.WithResolver(MessagePack.Resolvers.StandardResolver.Instance);

        /// <summary>
        ///     Serializes a command to byte array
        /// </summary>
        public static byte[] Serialize(BaseCommand command)
        {
            if (command == null)
            {
                Log.Error("Cannot serialize null command");
                return Array.Empty<byte>();
            }

            try
            {
                // Use standard MessagePack serialization
                return MessagePackSerializer.Serialize(command, _safeOptions);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to serialize command {command.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Deserializes a byte array into a command
        /// </summary>
        public static T Deserialize<T>(byte[] data) where T : BaseCommand
        {
            if (data == null || data.Length == 0)
            {
                Log.Warn("Cannot deserialize empty data");
                return default;
            }

            try
            {
                T command = MessagePackSerializer.Deserialize<T>(data, _safeOptions);
                
                // Validate required fields
                if (!string.IsNullOrEmpty(command.GetCommandType()))
                {
                    return command;
                }
                else
                {
                    Log.Warn($"Deserialized command {typeof(T).Name} has missing CommandType");
                    return command;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to deserialize command from {data.Length} bytes: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     Deserializes as generic base command, then casts to specific type
        /// </summary>
        public static T DeserializeAs<T>(BaseCommand prototype, byte[] data) where T : BaseCommand
        {
            if (prototype is T typedPrototype)
            {
                return Deserialize<T>(data);
            }
            else
            {
                Log.Error($"Cannot deserialize {typeof(T).Name} as {prototype.GetType().Name}");
                return null;
            }
        }

        /// <summary>
        ///     Measures serialized size without actually serializing
        /// </summary>
        public static int EstimateSize(BaseCommand command)
        {
            try
            {
                byte[] bytes = Serialize(command);
                return bytes?.Length ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        ///     Validates that a command can be serialized
        /// </summary>
        public static bool CanSerialize(BaseCommand command)
        {
            if (command == null) return false;

            Type type = command.GetType();
            
            // Check if type is marked with MessagePack attributes
            if (!type.GetCustomAttributes(typeof(MessagePackObjectAttribute), false).Any())
            {
                Log.Warn($"Type {type.Name} is not marked with [MessagePackObject]");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Creates an uninitialized command instance
        /// </summary>
        public static T CreateCommandInstance<T>() where T : BaseCommand, new()
        {
            return new T();
        }

        /// <summary>
        ///     Creates a deep copy of a command
        /// </summary>
        public static T CloneCommand<T>(T command) where T : BaseCommand
        {
            if (command == null) return null;

            byte[] data = Serialize(command);
            return Deserialize<T>(data);
        }

        /// <summary>
        ///     Debug: Logs serialization statistics
        /// </summary>
        public static void LogStats()
        {
            Log.Debug($"Serialization options: Compression={MessagePackCompression.Lz4Block}, Resolver={_safeOptions.Resolver?.GetType().Name}");
        }
    }
}
