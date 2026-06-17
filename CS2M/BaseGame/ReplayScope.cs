using System;
using System.Threading;

namespace CS2M.BaseGame
{
    internal static class ReplayScope
    {
        private static int _replayDepth;

        internal static IDisposable BeginReplayScope()
        {
            Interlocked.Increment(ref _replayDepth);
            return new Scope();
        }

        internal static bool IsReplayActive => Volatile.Read(ref _replayDepth) > 0;

        private sealed class Scope : IDisposable
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
