using CS2M.API.Commands;
using CS2M.BaseGame.Commands;
using CS2M.Helpers;
using CS2M.Networking;
using Game.Tools;
using HarmonyLib;
using Unity.Jobs;

namespace CS2M.BaseGame
{
    [HarmonyPatch(typeof(BulldozeToolSystem), "Apply")]
    [HarmonyPatch(new[] { typeof(JobHandle) })]
    public static class BulldozePatch
    {
        public static bool Prefix(
            BulldozeToolSystem __instance,
            JobHandle inputDeps,
            ref JobHandle __result,
            out BulldozeCommand __state)
        {
            __state = null;

            if (!ShouldHandleBulldoze(__instance))
            {
                return true;
            }



            if (Command.CurrentRole == MultiplayerRole.Client)
            {
                if (BulldozeService.TryBuildBulldozeCommand(__instance, requestOnly: true, out BulldozeCommand request))
                {
                    Command.SendToServer?.Invoke(request);
                    Log.Debug(
                        $"BulldozePatch: sent bulldoze request for {request.TargetEntityIndex}:{request.TargetEntityVersion}.");
                }
                else
                {
                    Log.Warn("BulldozePatch: failed to build bulldoze request command.");
                }

                __result = inputDeps;
                return false;
            }

            if (Command.CurrentRole == MultiplayerRole.Server)
            {
                if (!BulldozeService.TryBuildBulldozeCommand(__instance, requestOnly: false, out __state))
                {
                    Log.Warn("BulldozePatch: failed to build server-side bulldoze replication command.");
                    __result = inputDeps;
                    return false;
                }
            }

            return true;
        }

        public static void Postfix(BulldozeCommand __state)
        {
            if (Command.CurrentRole != MultiplayerRole.Server || __state == null)
            {
                return;
            }

            Command.SendToClients?.Invoke(__state);
            Log.Debug(
                $"BulldozePatch: replicated bulldoze {__state.TargetEntityIndex}:{__state.TargetEntityVersion} to clients.");
        }

        private static bool ShouldHandleBulldoze(BulldozeToolSystem toolSystem)
        {
            if (toolSystem == null)
            {
                return false;
            }

            if (NetworkInterface.Instance.LocalPlayer.PlayerStatus != CS2M.API.Networking.PlayerStatus.PLAYING)
            {
                return false;
            }

            return Command.CurrentRole == MultiplayerRole.Client || Command.CurrentRole == MultiplayerRole.Server;
        }
    }
}
