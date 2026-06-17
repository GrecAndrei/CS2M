// SPDX-License-Identifier: MIT
// M1 placeholder handler. Replies to a PortCheckRequestCommand with a
// hard-coded PortCheckResultCommand. Replaced in M3 by the real
// background probe pipeline.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using Microsoft.Extensions.Logging;

namespace CS2M.ApiServer.Protocol.Dispatch.Handlers;

public sealed class PortCheckRequestHandler : ApiCommandHandler<PortCheckRequestCommand>
{
    private readonly IApiCommandReplier _replier;
    private readonly ILogger<PortCheckRequestHandler> _logger;

    public PortCheckRequestHandler(IApiCommandReplier replier, ILogger<PortCheckRequestHandler> logger)
    {
        _replier = replier;
        _logger = logger;
    }

    protected override async Task HandleAsync(PortCheckRequestCommand command, IPEndPoint remote, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received PortCheckRequestCommand for port {Port} from {Peer}",
            command.Port, remote);

        var reply = new PortCheckResultCommand
        {
            State = PortCheckResult.Error,
            Message = "probe queued (M1 stub)"
        };
        await _replier.ReplyAsync(reply, remote, cancellationToken).ConfigureAwait(false);
    }
}
