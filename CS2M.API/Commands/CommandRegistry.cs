using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using CS2M.API;

namespace CS2M.API.Commands
{
    /// <summary>
    ///     Centralized command registry for type-safe command routing and handler management
    /// </summary>
    public static class CommandRegistry
    {
        private static readonly ConcurrentDictionary<Type, CommandHandler> _handlers = new();
        private static readonly ConcurrentDictionary<string, Type> _commandTypes = new();
        private static readonly object _lock = new();

        /// <summary>
        ///     Registers all commands from an assembly
        /// </summary>
        public static void RegisterAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsAbstract && typeof(BaseCommand).IsAssignableFrom(type))
                {
                    RegisterCommandType(type);
                }
            }
        }

        /// <summary>
        ///     Registers a specific command type
        /// </summary>
        public static void RegisterCommandType(Type commandType)
        {
            if (commandType == null || !typeof(BaseCommand).IsAssignableFrom(commandType))
            {
                throw new ArgumentException($"Type {commandType?.Name} must inherit from BaseCommand", nameof(commandType));
            }

            string typeName = commandType.Name;
            if (!_commandTypes.ContainsKey(typeName))
            {
                lock (_lock)
                {
                    // Double-check locking
                    if (!_commandTypes.ContainsKey(typeName))
                    {
                        _commandTypes.TryAdd(typeName, commandType);
                        Log.Debug($"Registered command type: {typeName}");
                    }
                }
            }
        }

        /// <summary>
        ///     Registers a specific command handler instance
        /// </summary>
        public static void RegisterHandler(CommandHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Type commandType = handler.CommandType;
            if (commandType == null)
            {
                throw new InvalidOperationException($"Handler {handler.GetType().Name} must have a valid CommandType");
            }

            _handlers[commandType] = handler;
            Log.Debug($"Registered handler: {handler.GetType().Name} for {commandType.Name}");
        }

        /// <summary>
        ///     Gets or creates a handler for a command type
        /// </summary>
        public static CommandHandler GetOrRegisterHandler<T>(Func<T> factory = null) where T : CommandHandler
        {
            Type commandType = typeof(T).GetProperty("CommandType")?.PropertyType;
            if (commandType == null)
            {
                throw new InvalidOperationException($"Handler {typeof(T).Name} must have CommandType property");
            }

            return _handlers.GetOrAdd(commandType, type =>
            {
                var handler = factory?.Invoke() ?? Activator.CreateInstance<T>();
                Log.Trace($"Registered handler: {handler.GetType().Name} for {commandType.Name}");
                return handler;
            });
        }

        /// <summary>
        ///     Checks if a handler exists for a command type
        /// </summary>
        public static bool HasHandler(Type commandType)
        {
            return _handlers.ContainsKey(commandType);
        }

        /// <summary>
        ///     Gets handler for a command type
        /// </summary>
        public static CommandHandler GetHandler(Type commandType)
        {
            return _handlers.TryGetValue(commandType, out var handler) ? handler : null;
        }

        /// <summary>
        ///     Resolves command type from command instance
        /// </summary>
        public static Type ResolveCommandType(BaseCommand command)
        {
            return command?.GetType() ?? throw new ArgumentNullException(nameof(command));
        }

        /// <summary>
        ///     Gets all registered command types
        /// </summary>
        public static IReadOnlyDictionary<string, Type> GetAllCommandTypes()
        {
            return new ReadOnlyDictionary<string, Type>(_commandTypes.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value));
        }

        /// <summary>
        ///     Clears all registered handlers (for testing/cleanup)
        /// </summary>
        public static void Clear()
        {
            _handlers.Clear();
            _commandTypes.Clear();
            Log.Info("Command registry cleared");
        }

        /// <summary>
        ///     Logs current registration state (for debugging)
        /// </summary>
        public static void LogRegistrations()
        {
            int handlerCount = _handlers.Count;
            int typeCount = _commandTypes.Count;
            
            Log.Info($"Command Registry Status: {handlerCount} handlers, {typeCount} command types");
            
            foreach (var kvp in _commandTypes)
            {
                Log.Trace($"  - {kvp.Key}: {kvp.Value.FullName}");
            }
        }
    }
}
