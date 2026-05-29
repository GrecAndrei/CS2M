using System;
using System.Threading;
using CS2M.API.Commands;
using CS2M.BaseGame.Commands;
using CS2M.Networking;
using Game.Tools;
using HarmonyLib;
using Unity.Jobs;

namespace CS2M.BaseGame
{
    [HarmonyPatch(typeof(AreaToolSystem), "Apply")]
    [HarmonyPatch(new[] { typeof(JobHandle), typeof(bool) })]
    public static class AreaSyncPatch
    {
        private static int _replayDepth;

        internal static IDisposable BeginReplayScope()
        {
            Interlocked.Increment(ref _replayDepth);
            return new ReplayScope();
        }

        internal static bool IsReplayActive => Volatile.Read(ref _replayDepth) > 0;

        public static bool Prefix(
            AreaToolSystem __instance,
            JobHandle inputDeps,
            bool singleFrameOnly,
            ref JobHandle __result,
            out AreaApplyCommand __state)
        {
            __state = null;

            if (IsReplayActive || !ShouldHandle(__instance))
            {
                return true;
            }

            if (Command.CurrentRole == MultiplayerRole.Client)
            {
                bool built = AreaSyncService.TryBuildApplyCommand(
                    __instance,
                    singleFrameOnly,
                    requestOnly: true,
                    out AreaApplyCommand request);
                if (!built)
                {
                    Log.Warn("AreaSyncPatch: failed to build area request command.");
                    __result = inputDeps;
                    return false;
                }

                Command.SendToServer?.Invoke(request);
                Log.Debug($"AreaSyncPatch: sent area request nonce {request.ApplyNonce}.");
                __result = inputDeps;
                return false;
            }

            if (Command.CurrentRole == MultiplayerRole.Server)
            {
                bool built = AreaSyncService.TryBuildApplyCommand(
                    __instance,
                    singleFrameOnly,
                    requestOnly: false,
                    out __state);
                if (!built)
                {
                    Log.Warn("AreaSyncPatch: failed to build local area replication command.");
                    __result = inputDeps;
                    return false;
                }
            }

            return true;
        }

        public static void Postfix(AreaApplyCommand __state)
        {
            if (IsReplayActive || Command.CurrentRole != MultiplayerRole.Server || __state == null)
            {
                return;
            }

            Command.SendToClients?.Invoke(__state);
            Log.Debug($"AreaSyncPatch: replicated area nonce {__state.ApplyNonce}.");
        }

        private static bool ShouldHandle(AreaToolSystem toolSystem)
        {
            if (toolSystem == null)
            {
                return false;
            }

            if (NetworkInterface.Instance?.LocalPlayer?.PlayerStatus != CS2M.API.Networking.PlayerStatus.PLAYING)
            {
                return false;
            }

            return Command.CurrentRole == MultiplayerRole.Client || Command.CurrentRole == MultiplayerRole.Server;
        }

        private sealed class ReplayScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Interlocked.Decrement(ref _replayDepth);
            }
        }
    }
}
