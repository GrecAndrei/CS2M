// SPDX-License-Identifier: MIT
// Stub port-reachability worker for M2. Consumes PortCheckWorkItem
// from the channel and logs them; the real TCP probe lands in M3.
//
// Even at the M2 stub stage the queue MUST be drained so
// `BoundedChannelFullMode.DropOldest` doesn't silently throw away
// real requests while we're busy answering other things.

using CS2M.ApiServer.Workers.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CS2M.ApiServer.Workers;

public sealed class PortReachableChecker : BackgroundService
{
    private readonly PortCheckQueue _queue;
    private readonly ILogger<PortReachableChecker> _logger;

    public PortReachableChecker(PortCheckQueue queue, ILogger<PortReachableChecker> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PortReachableChecker started (M2 stub, real probe in M3)");
        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "[stub] would probe {Token} on port {Port} (enqueued at {EnqueuedAt:o})",
                string.IsNullOrEmpty(item.Token) ? "(unknown)" : item.Token,
                item.PortToProbe,
                item.EnqueuedAt);
        }
    }
}