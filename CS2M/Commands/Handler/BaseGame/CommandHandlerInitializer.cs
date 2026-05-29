using System;
using CS2M.API.Commands;
using CS2M.BaseGame.Commands;
using System.Reflection;
using System.Collections.Generic;
using LiteNetLib;

namespace CS2M.Commands.Handler.BaseGame
{
    /// <summary>
    ///     Initializes and registers all base game command handlers with the registry
    /// </summary>
    public static class CommandHandlerInitializer
    {
        private static readonly HashSet<string> _initializedHandlers = new();
        private static readonly List<System.Action> _initializationCallbacks = new();

        /// <summary>
        ///     Initialize all command handlers
        /// </summary>
        public static void InitializeAll()
        {
            if (_initializedHandlers.Count > 0)
            {
                Log.Debug("Command handlers already initialized");
                return;
            }

            Log.Info("Initializing command handlers...");

            // Register frame sync handler
            RegisterHandler<FrameCommandHandler>();

            // Register money sync handler
            RegisterHandler<MoneyCommandHandler>();

            // Register area sync handler
            RegisterHandler<AreaApplyCommandHandler>();

            // Register XP and Milestone sync handler
            RegisterHandler<XPMilestoneCommandHandler>();

            // Register player cursor sync handler
            RegisterHandler<PlayerCursorCommandHandler>();

            // Register map ping handler
            RegisterHandler<MapPingCommandHandler>();

            // Register building creation handler (if exists)
            RegisterAssemblyHandlers();

            Log.Info($"Registered {_initializationCallbacks.Count} handler callbacks");
            CommandRegistry.LogRegistrations();
            
            _initializedHandlers.Add("BaseGame");
        }

        /// <summary>
        ///     Manually register a specific handler type
        /// </summary>
        public static T RegisterHandler<T>() where T : CommandHandler, new()
        {
            var handler = CommandRegistry.GetOrRegisterHandler<T>(() => new T());
            
            _initializationCallbacks.Add(() =>
            {
                Log.Trace($"Handler callback for: {typeof(T).Name}");
            });

            return handler as T;
        }

        /// <summary>
        ///     Auto-discover and register handlers from assemblies
        /// </summary>
        private static void RegisterAssemblyHandlers()
        {
            try
            {
                // Get all assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // Find all handlers in this assembly
                        foreach (var type in assembly.GetTypes())
                        {
                            if (!type.IsAbstract && typeof(CommandHandler).IsAssignableFrom(type))
                            {
                                // Create instance dynamically
                                var handler = Activator.CreateInstance(type);
                                
                                if (handler is CommandHandler cmdHandler)
                                {
                                    Type commandType = GetCommandTypeFromHandler(type);
                                    if (commandType != null)
                                    {
                                        CommandRegistry.RegisterHandler(cmdHandler);
                                        Log.Debug($"Auto-registered handler: {type.Name} for {commandType.Name}");
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warn($"Failed to scan assembly {assembly.GetName().Name}: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Failed to auto-register handlers: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Gets the command type associated with a handler
        /// </summary>
        private static Type GetCommandTypeFromHandler(Type handlerType)
        {
            // Try to get CommandType property first
            var prop = handlerType.GetProperty("CommandType");
            if (prop != null && prop.PropertyType != null)
            {
                return prop.PropertyType;
            }

            // Try to infer from generic base class
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

        /// <summary>
        ///     Reset all handler registrations
        /// </summary>
        public static void Reset()
        {
            CommandRegistry.Clear();
            _initializedHandlers.Clear();
            _initializationCallbacks.Clear();
            Log.Debug("Command handlers reset");
        }
    }
}
