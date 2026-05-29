using CS2M.API.Commands;
using CS2M.BaseGame.Commands;
using CS2M.Networking;
using Game.Tools;
using HarmonyLib;
using Unity.Jobs;

namespace CS2M.BaseGame
{
    [HarmonyPatch(typeof(ZoneToolSystem), "Apply")]
    [HarmonyPatch(new[] { typeof(JobHandle), typeof(bool) })]
    public static class ZoneSyncPatch
    {
        public static bool Prefix(
            ZoneToolSystem __instance,
            JobHandle inputDeps,
            bool singleFrameOnly,
            ref JobHandle __result,
            out ZoneApplyCommand __state)
        {
            __state = null;
            if (!ShouldHandle(__instance))
            {
                return true;
            }

            if (!ZoneSyncService.IsSupportedOperation(__instance, out string reason))
            {
                Log.Warn($"ZoneSyncPatch: unsupported zone action blocked ({reason}).");
                __result = inputDeps;
                return false;
            }

            if (Command.CurrentRole == MultiplayerRole.Client)
            {
                bool built = ZoneSyncService.TryBuildApplyCommand(
                    __instance,
                    singleFrameOnly,
                    requestOnly: true,
                    out ZoneApplyCommand request);
                if (!built)
                {
                    Log.Warn("ZoneSyncPatch: failed to build zoning request command.");
                    __result = inputDeps;
                    return false;
                }

                Command.SendToServer?.Invoke(request);
                Log.Debug($"ZoneSyncPatch: sent zoning request nonce {request.ApplyNonce}.");
                __result = inputDeps;
                return false;
            }

            if (Command.CurrentRole == MultiplayerRole.Server)
            {
                ZoneSyncService.TryBuildApplyCommand(
                    __instance,
                    singleFrameOnly,
                    requestOnly: false,
                    out __state);
            }

            return true;
        }

        public static void Postfix(ZoneApplyCommand __state)
        {
            if (Command.CurrentRole != MultiplayerRole.Server || __state == null)
            {
                return;
            }

            Command.SendToClients?.Invoke(__state);
            Log.Debug($"ZoneSyncPatch: replicated zoning nonce {__state.ApplyNonce}.");
        }

        private static bool ShouldHandle(ZoneToolSystem toolSystem)
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
