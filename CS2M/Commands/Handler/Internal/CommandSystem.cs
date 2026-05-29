using CS2M.API.Commands;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using CS2M.Commands.Handler.BaseGame;

namespace CS2M.Commands.Handler.Internal
{
    /// <summary>
    ///     Central command execution system with registration and routing
    /// </summary>
    public class CommandSystem
    {
        private static readonly ConcurrentDictionary<Type, object> _handlers = new();
        private static ConcurrentBag<Type> _handlerTypes = new();
        private static bool _isInitialized;

        /// <summary>
        ///     Check if command system is initialized
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        ///     Initialize all registered handlers
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                Log.Debug("Command system already initialized");
                return;
            }

            Log.Info("Initializing command system...");

            // Register all built-in internal handlers
            RegisterInternalHandlers();

            // Register base game handlers
            CommandHandlerInitializer.InitializeAll();

            _isInitialized = true;
            Log.Info($"Command system initialized with {_handlers.Count} handlers");
            CommandRegistry.LogRegistrations();
        }

        /// <summary>
        ///     Register a handler type
        /// </summary>
        public static void RegisterHandler<T>() where T : CommandHandler
        {
            var handler = Activator.CreateInstance<T>();
            
            Type commandType = typeof(T).GetProperty("CommandType")?.PropertyType ?? 
                              GetCommandTypeFromBase(typeof(T));

            if (commandType != null)
            {
                _handlers.TryAdd(commandType, handler);
                _handlerTypes.Add(commandType);
                
                Log.Debug($"Registered handler: {typeof(T).Name} for {commandType.Name}");
            }
            else
            {
                Log.Error($"Could not determine command type for handler {typeof(T).Name}");
            }
        }

        /// <summary>
        ///     Get handler for a specific command type
        /// </summary>
        public static CommandHandler GetHandler(Type commandType)
        {
            return _handlers.TryGetValue(commandType, out var handler) ? handler as CommandHandler : null;
        }

        /// <summary>
        ///     Check if handler exists for command type
        /// </summary>
        public static bool HasHandler(Type commandType)
        {
            return _handlers.ContainsKey(commandType);
        }

        /// <summary>
        ///     Execute command with appropriate handler
        /// </summary>
        public static void ExecuteCommand(BaseCommand command, NetPeer peer = null)
        {
            if (command == null)
            {
                Log.Warn("Attempted to execute null command");
                return;
            }

            Type commandType = command.GetType();
            
            if (!HasHandler(commandType))
            {
                Log.Warn($"No handler found for command: {commandType.Name}");
                return;
            }

            try
            {
                var handler = GetHandler(commandType);
                
                if (command is ServerCommand serverCmd)
                {
                    handler.HandleOnServer(serverCmd, peer);
                }
                else if (command is ClientCommand clientCmd)
                {
                    handler.Handle(clientCmd, peer);
                }
                else
                {
                    handler.Parse(command);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to execute command {commandType.Name}: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Reset all handlers (for testing or restart)
        /// </summary>
        public static void Reset()
        {
            _handlers.Clear();
            _handlerTypes = new ConcurrentBag<Type>();
            _isInitialized = false;
            Log.Info("Command system reset");
        }

        /// <summary>
        ///     List all registered handlers
        /// </summary>
        public static void ListHandlers()
        {
            Log.Info("Registered Handlers:");
            foreach (var kvp in _handlers)
            {
                Log.Trace($"  - {kvp.Key.Name}: {kvp.Value.GetType().Name}");
            }
        }

        /// <summary>
        ///     Register all internal handlers automatically
        /// </summary>
        private static void RegisterInternalHandlers()
        {
            // Internal connection handlers
            RegisterHandler<PreconditionsCheckHandler>();
            RegisterHandler<JoinRequestHandler>();
            RegisterHandler<JoinReadyHandler>();
            RegisterHandler<JoinAcceptedHandler>();
            RegisterHandler<WorldTransferHandler>();
            RegisterHandler<ChatMessageHandler>();

            // Error handlers
            RegisterHandler<PreconditionsErrorHandler>();
            RegisterHandler<PreconditionsSuccessHandler>();
        }

        /// <summary>
        ///     Infer command type from handler base class
        /// </summary>
        private static Type GetCommandTypeFromBase(Type handlerType)
        {
            var baseTypes = handlerType.BaseType;
            
            while (baseTypes != null)
            {
                if (baseTypes.IsGenericType)
                {
                    var genericArgs = baseTypes.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        var arg = genericArgs[0];
                        if (arg.IsSubclassOf(typeof(BaseCommand)))
                        {
                            return arg;
                        }
                    }
                }
                baseTypes = baseTypes.BaseType;
            }

            return null;
        }
    }
}
