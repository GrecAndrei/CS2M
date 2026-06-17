// SPDX-License-Identifier: MIT
// M1 echo handler. Logs ServerRegistrationCommand and replies with a
// canned PortCheckResultCommand so the test client can prove the
// full codec/listener/replier pipeline works.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using Microsoft.Extensions.Logging;

namespace CS2M.ApiServer.Protocol.Dispatch.Handlers;

public sealed class ServerRegistrationHandler : ApiCommandHandler<ServerRegistrationCommand>
{
    private readonly IApiCommandReplier _replier;
    private readonly ILogger<ServerRegistrationHandler> _logger;

    public ServerRegistrationHandler(IApiCommandReplier replier, ILogger<ServerRegistrationHandler> logger)
    {
        _replier = replier;
        _logger = logger;
    }

    protected override Task HandleAsync(ServerRegistrationCommand command, IPEndPoint remote, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Echo: received ServerRegistrationCommand token={Token} {LocalIp}:{LocalPort} from {Peer}",
            command.Token, command.LocalIp, command.LocalPort, remote);

        var reply = new PortCheckResultCommand
        {
            State = PortCheckResult.Reachable,
            Message = "echo from M1 (registration accepted)"
        };
        return _replier.ReplyAsync(reply, remote, cancellationToken);
    }
}
