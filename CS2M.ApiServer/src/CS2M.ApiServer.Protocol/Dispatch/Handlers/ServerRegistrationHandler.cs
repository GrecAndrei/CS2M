// SPDX-License-Identifier: MIT
// Handles ServerRegistrationCommand by upserting the game server
// record in PostgreSQL and replying with a PortCheckResultCommand
// so the mod has positive confirmation that the registration was
// accepted.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Storage.Repositories;
using Microsoft.Extensions.Logging;

namespace CS2M.ApiServer.Protocol.Dispatch.Handlers;

public sealed class ServerRegistrationHandler : ApiCommandHandler<ServerRegistrationCommand>
{
    private readonly IApiCommandReplier _replier;
    private readonly IServerRepository _servers;
    private readonly ILogger<ServerRegistrationHandler> _logger;

    public ServerRegistrationHandler(
        IApiCommandReplier replier,
        IServerRepository servers,
        ILogger<ServerRegistrationHandler> logger)
    {
        _replier = replier;
        _servers = servers;
        _logger = logger;
    }

    protected override async Task HandleAsync(ServerRegistrationCommand command, IPEndPoint remote, CancellationToken cancellationToken)
    {
        if (!Core.Validation.TokenValidator.IsWellFormed(command.Token, out var tokenError))
        {
            _logger.LogWarning(
                "Rejecting ServerRegistrationCommand from {Peer}: {Error}",
                remote, tokenError);
            await _replier.ReplyAsync(new PortCheckResultCommand
            {
                State = PortCheckResult.Error,
                Message = $"invalid token: {tokenError}"
            }, remote, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!System.Net.IPAddress.TryParse(command.LocalIp, out _))
        {
            _logger.LogWarning(
                "Rejecting ServerRegistrationCommand from {Peer}: LocalIp {LocalIp} is not a valid IP",
                remote, command.LocalIp);
            await _replier.ReplyAsync(new PortCheckResultCommand
            {
                State = PortCheckResult.Error,
                Message = "invalid LocalIp"
            }, remote, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command.LocalPort is < 1 or > 65535)
        {
            _logger.LogWarning(
                "Rejecting ServerRegistrationCommand from {Peer}: LocalPort {Port} is out of range",
                remote, command.LocalPort);
            await _replier.ReplyAsync(new PortCheckResultCommand
            {
                State = PortCheckResult.Error,
                Message = "invalid LocalPort"
            }, remote, cancellationToken).ConfigureAwait(false);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var created = await _servers
            .UpsertFromRegistrationAsync(command.Token, command.LocalIp, command.LocalPort, now, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Registered {Action} {Token} {LocalIp}:{LocalPort} from {Peer}",
            created ? "new" : "refresh",
            command.Token, command.LocalIp, command.LocalPort, remote);

        await _replier.ReplyAsync(new PortCheckResultCommand
        {
            State = PortCheckResult.Reachable,
            Message = "registration accepted"
        }, remote, cancellationToken).ConfigureAwait(false);
    }
}