using System;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using CS2M; // For Log

namespace CS2M.Systems
{
    public partial class EconomyInspectorSystem : SystemBase
    {
        private bool _hasRun = false;

        protected override void OnCreate()
        {
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (_hasRun) return;

            // wait a bit for assemblies to be fully loaded if needed
            if (UnityEngine.Time.frameCount < 600) return;

            Log.Info("[EconomyInspector] Starting Type Discovery...");

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var gameAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "Game");

                if (gameAssembly == null)
                {
                    Log.Error("[EconomyInspector] Could not find 'Game' assembly!");
                    _hasRun = true;
                    return;
                }

                Log.Info($"[EconomyInspector] Found Game assembly: {gameAssembly.FullName}");

                var types = gameAssembly.GetTypes();
                
                // Search for Money
                var moneyTypes = types.Where(t => t.Name.IndexOf("Money", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                Log.Info($"[EconomyInspector] Found {moneyTypes.Count} types with 'Money':");
                foreach (var t in moneyTypes)
                {
                    Log.Info($" - {t.FullName}");
                }

                // Search for Economy namespace types (limit to 50)
                var economyTypes = types.Where(t => t.Namespace != null && t.Namespace.Contains("Game.Economy")).ToList();
                 Log.Info($"[EconomyInspector] Found {economyTypes.Count} types in Game.Economy:");
                foreach (var t in economyTypes.Take(50))
                {
                    Log.Info($" - {t.FullName}");
                }

                // Specifically check for Game.Economy.Resources
                var resourcesType = types.FirstOrDefault(t => t.FullName == "Game.Economy.Resources");
                if (resourcesType != null)
                {
                    Log.Info($"[EconomyInspector] FOUND Game.Economy.Resources! It is a {resourcesType.BaseType}");
                    foreach(var f in resourcesType.GetFields())
                        Log.Info($"   Field: {f.Name} ({f.FieldType})");
                }
                else
                {
                    Log.Info("[EconomyInspector] Game.Economy.Resources NOT found by exact name.");
                }

            }
            catch (Exception e)
            {
                Log.Error($"[EconomyInspector] Error during discovery: {e}");
            }

            _hasRun = true;
        }
    }
}
