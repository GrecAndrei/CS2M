// SPDX-License-Identifier: MIT
// Consumes PortCheckWorkItem from the queue, opens a TCP connection
// to the work item's target endpoint, and reports back over UDP to
// the game server that originally asked for the probe.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Core.Dispatch;
using CS2M.ApiServer.Workers.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CS2M.ApiServer.Workers;

public sealed class PortReachableChecker : BackgroundService
{
    private readonly PortCheckQueue _queue;
    private readonly IApiCommandCodec _codec;
    private readonly IProbeReplyChannel _replyChannel;
    private readonly ILogger<PortReachableChecker> _logger;
    private readonly PortCheckQueueOptions _options;
    private readonly SemaphoreSlim _gate;

    public PortReachableChecker(
        PortCheckQueue queue,
        IApiCommandCodec codec,
        IProbeReplyChannel replyChannel,
        IOptions<PortCheckQueueOptions> options,
        ILogger<PortReachableChecker> logger)
    {
        _queue = queue;
        _codec = codec;
        _replyChannel = replyChannel;
        _options = options.Value;
        _logger = logger;
        _gate = new SemaphoreSlim(Math.Max(1, _options.MaxParallelProbes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PortReachableChecker started (max parallel={MaxParallel}, timeout={TimeoutMs}ms)",
            _options.MaxParallelProbes, _options.ProbeTimeoutMs);

        var tasks = new List<Task>();
        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            tasks.Add(RunProbeAsync(item, stoppingToken));
            if (tasks.Count >= 64)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
                tasks.Clear();
            }
        }
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private async Task RunProbeAsync(IPortCheckWorkItem item, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            var (state, message) = await ProbeOnceAsync(item).ConfigureAwait(false);
            sw.Stop();

            _logger.LogInformation(
                "Probe complete {Target}:{Port} state={State} latency={LatencyMs}ms msg={Message}",
                item.LocalIp, item.Port, state, sw.ElapsedMilliseconds, message);

            var replyTarget = new IPEndPoint(IPAddress.Parse(item.LocalIp), item.LocalPort);
            try
            {
                await _replyChannel.SendAsync(
                    new PortCheckResultCommand { State = state, Message = message },
                    replyTarget,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send PortCheckResultCommand to {Target}", replyTarget);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(PortCheckResult State, string Message)> ProbeOnceAsync(IPortCheckWorkItem item)
    {
        if (!IPAddress.TryParse(item.LocalIp, out var ip))
        {
            return (PortCheckResult.Error, $"invalid IP address: {item.LocalIp}");
        }
        if (item.Port is < 1 or > 65535)
        {
            return (PortCheckResult.Error, $"invalid port: {item.Port}");
        }

        using var client = new TcpClient();
        try
        {
            using var cts = new CancellationTokenSource(_options.ProbeTimeoutMs);
            await client.ConnectAsync(ip, item.Port, cts.Token).ConfigureAwait(false);
            return (PortCheckResult.Reachable, $"connected in {_options.ProbeTimeoutMs}ms budget");
        }
        catch (OperationCanceledException)
        {
            return (PortCheckResult.Unreachable,
                $"TCP connect timed out after {_options.ProbeTimeoutMs}ms");
        }
        catch (SocketException ex)
        {
            return ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => (PortCheckResult.Unreachable, "connection refused"),
                SocketError.TimedOut => (PortCheckResult.Unreachable, "connection timed out"),
                SocketError.HostUnreachable => (PortCheckResult.Unreachable, "host unreachable"),
                SocketError.NetworkUnreachable => (PortCheckResult.Unreachable, "network unreachable"),
                _ => (PortCheckResult.Error, $"socket error: {ex.SocketErrorCode} ({ex.Message})")
            };
        }
        catch (Exception ex)
        {
            return (PortCheckResult.Error, $"probe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}