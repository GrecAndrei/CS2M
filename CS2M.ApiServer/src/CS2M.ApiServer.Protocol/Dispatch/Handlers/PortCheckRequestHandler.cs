// SPDX-License-Identifier: MIT
// Receives PortCheckRequestCommand, enqueues a probe work item for
// the PortReachableChecker worker, and acknowledges the mod
// immediately with a queued reply. The worker sends the real
// PortCheckResultCommand once the TCP probe completes.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Core.Dispatch;
using Microsoft.Extensions.Logging;

namespace CS2M.ApiServer.Protocol.Dispatch.Handlers;

public sealed class PortCheckRequestHandler : ApiCommandHandler<PortCheckRequestCommand>
{
    private readonly IApiCommandReplier _replier;
    private readonly IPortCheckEnqueuer _enqueuer;
    private readonly ILogger<PortCheckRequestHandler> _logger;

    public PortCheckRequestHandler(
        IApiCommandReplier replier,
        IPortCheckEnqueuer enqueuer,
        ILogger<PortCheckRequestHandler> logger)
    {
        _replier = replier;
        _enqueuer = enqueuer;
        _logger = logger;
    }

    protected override async Task HandleAsync(PortCheckRequestCommand command, IPEndPoint remote, CancellationToken cancellationToken)
    {
        if (command.Port is < 1 or > 65535)
        {
            _logger.LogWarning(
                "Rejecting PortCheckRequestCommand from {Peer}: Port {Port} is out of range",
                remote, command.Port);
            await _replier.ReplyAsync(new PortCheckResultCommand
            {
                State = PortCheckResult.Error,
                Message = "invalid Port"
            }, remote, cancellationToken).ConfigureAwait(false);
            return;
        }

        // The mod's PortCheckRequestCommand does not currently include
        // a token; the probe runs against the requester's remote
        // endpoint on the requested port. Once the mod adds an optional
        // Token field the handler will cross-reference it against the
        // servers table for richer responses.
        var work = new PortCheckWorkItem(
            Token: string.Empty,
            LocalIp: remote.Address.ToString(),
            LocalPort: remote.Port,
            Port: command.Port,
            EnqueuedAt: DateTimeOffset.UtcNow);

        if (!_enqueuer.TryEnqueue(work))
        {
            _logger.LogWarning(
                "PortCheckQueue is full; rejecting probe for {Peer}:{Port}",
                remote, command.Port);
            await _replier.ReplyAsync(new PortCheckResultCommand
            {
                State = PortCheckResult.Error,
                Message = "probe queue full; try again later"
            }, remote, cancellationToken).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation(
            "Enqueued port check for {Peer}:{Port}",
            remote, command.Port);

        await _replier.ReplyAsync(new PortCheckResultCommand
        {
            State = PortCheckResult.Error,
            Message = "probe queued"
        }, remote, cancellationToken).ConfigureAwait(false);
    }
}