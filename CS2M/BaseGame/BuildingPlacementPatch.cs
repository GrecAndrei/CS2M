using CS2M.API.Commands;
using CS2M.BaseGame.Commands;
using CS2M.Networking;
using Game.Prefabs;
using Game.Tools;
using HarmonyLib;
using Unity.Jobs;

namespace CS2M.BaseGame
{
    [HarmonyPatch(typeof(ObjectToolSystem), "Apply")]
    [HarmonyPatch(new[] { typeof(JobHandle), typeof(bool) })]
    public static class BuildingPlacementPatch
    {
        public static bool Prefix(
            ObjectToolSystem __instance,
            JobHandle inputDeps,
            bool singleFrameOnly,
            ref JobHandle __result,
            out BuildingCreateCommand __state)
        {
            __state = null;

            if (ReplayScope.IsReplayActive || !ShouldHandlePlacement(__instance))
            {
                return true;
            }

            if (Command.CurrentRole == MultiplayerRole.Client)
            {
                if (BuildingPlacementService.TryBuildPlacementCommand(__instance, requestOnly: true, out BuildingCreateCommand request))
                {
                    Command.SendToServer?.Invoke(request);
                    Log.Debug($"BuildingPlacementPatch: sent placement request '{request.PrefabName}'.");
                }

                __result = inputDeps;
                return false;
            }

            if (Command.CurrentRole == MultiplayerRole.Server)
            {
                BuildingPlacementService.TryBuildPlacementCommand(__instance, requestOnly: false, out __state);
            }

            return true;
        }

        public static void Postfix(BuildingCreateCommand __state)
        {
            if (ReplayScope.IsReplayActive || Command.CurrentRole != MultiplayerRole.Server || __state == null)
            {
                return;
            }

            Command.SendToClients?.Invoke(__state);
            Log.Debug($"BuildingPlacementPatch: replicated placement '{__state.PrefabName}' to clients.");
        }

        private static bool ShouldHandlePlacement(ObjectToolSystem toolSystem)
        {
            if (toolSystem == null)
            {
                return false;
            }

            if (NetworkInterface.Instance.LocalPlayer.PlayerStatus != CS2M.API.Networking.PlayerStatus.PLAYING)
            {
                return false;
            }

            if (toolSystem.state != ObjectToolSystem.State.Adding)
            {
                return false;
            }

            if (toolSystem.mode != ObjectToolSystem.Mode.Create)
            {
                return false;
            }

            return toolSystem.prefab is BuildingPrefab;
        }
    }
}
