using CS2M.Networking;
using CS2M.Settings;
using System;
using Unity.Entities;
using CS2M.Commands.Handler.Internal;
using CS2M.Systems;
using CS2M.Helpers;

namespace CS2M
{
    /// <summary>
    ///     Manages mod lifecycle and initialization sequence
    /// </summary>
    public static class ModInitializer
    {
        private static bool _modInitialized;
        private static readonly object _lock = new object();

        /// <summary>
        ///     Initialize all mod subsystems in proper order
        /// </summary>
        public static bool Initialize(ModSettings settings)
        {
            lock (_lock)
            {
                if (_modInitialized)
                {
                    Log.Warn("Mod already initialized");
                    return false;
                }

                Log.Info("Starting CS2M initialization sequence...");

                try
                {
                    // 1. Configure logging first (so we can log everything)
                    ConfigurateLogging(settings.LoggingLevel);
                    
                    // 2. Initialize networking subsystem
                    if (!NetworkInitializer.Initialize(settings))
                    {
                        Log.Error("Failed to initialize network subsystem");
                        return false;
                    }

                    // 3. Initialize command system
                    CommandSystem.Initialize();

                    // 4. Initialize game systems
                    InitializeGameSystems();

                    // 5. Register update systems
                    RegisterUpdateSystems();

                    _modInitialized = true;
                    Log.Info("CS2M initialization complete!");
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error($"Initialization failed: {ex.Message}", ex);
                    Shutdown();
                    return false;
                }
            }
        }

        /// <summary>
        ///     Configure logging level
        /// </summary>
        private static void ConfigurateLogging(int level)
        {
            try
            {
                Log.LogLevelThreshold = level;
                Log.Info($"Logging configured for level: {level}");
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not configure logging: {ex.Message}");
            }
        }

        /// <summary>
        ///     Initialize game integration subsystems
        /// </summary>
        private static void InitializeGameSystems()
        {
            Log.Debug("Initializing game systems...");

            try
            {
                // Initialize economy inspector
                World.DefaultGameObjectInjectionWorld?.GetOrCreateSystemManaged<EconomyInspectorSystem>();
                
                Log.Debug("Game systems initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"Game system initialization failed: {ex.Message}", ex);
            }
        }

        /// <sync>
        ///     Register all ECS systems with update phase
        /// </sync>
        private static void RegisterUpdateSystems()
        {
            try
            {
                // This would be called from Mod.cs OnLoad method
                // For now, just documenting what needs to be registered
                
                Log.Debug("Update system registration would occur here via Mod.cs");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to register update systems: {ex.Message}");
            }
        }

        /// <summary>
        ///     Perform cleanup on shutdown
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (!_modInitialized)
                {
                    Log.Debug("Mod not initialized, nothing to shutdown");
                    return;
                }

                Log.Info("Shutting down CS2M...");

                // 1. Unregister and stop systems
                StopAllSystems();

                // 2. Shutdown networking
                NetworkInitializer.Shutdown();

                // 3. Reset command registry
                CommandSystem.Reset();

                _modInitialized = false;
                
                Log.Info("CS2M shutdown complete");
            }
        }

        /// <summary>
        ///     Stop all running systems
        /// </summary>
        private static void StopAllSystems()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null)
                {
                    // Dispose systems gracefully
                    world.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Error stopping systems: {ex.Message}");
            }
        }

        /// <summary>
        ///     Execute periodic maintenance tasks
        /// </summary>
        public static void PerformMaintenance()
        {
            // Update network statistics
            NetworkStatistics.LogCurrentStats();

            // Clean up stale data
            CleanupStaleData();

            // Log maintenance status
            Log.Trace("Maintenance cycle completed");
        }

        /// <summary>
        ///     Remove stale data that's no longer needed
        /// </summary>
        private static void CleanupStaleData()
        {
            try
            {
                // Clear old network connections after timeout
                var networkManager = NetworkInitializer.GetNetworkManager();
                if (networkManager != null && networkManager.IsRunning)
                {
                    // Logic to detect and remove stale peers would go here
                }

                // Clear command cache if too large
                // Clear other temporary data structures
            }
            catch (Exception ex)
            {
                Log.Warn($"Cleanup failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     Get current mod state
        /// </summary>
        public static ModState State => new ModState
        {
            IsInitialized = _modInitialized,
            NetworkActive = NetworkInitializer.IsNetworkActive(),
            HasCommandHandlers = CommandSystem.IsInitialized
        };

        /// <summary>
        ///     Current mod state information
        /// </summary>
        public struct ModState
        {
            public bool IsInitialized;
            public bool NetworkActive;
            public bool HasCommandHandlers;
        }
    }
}
