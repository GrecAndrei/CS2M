// SPDX-License-Identifier: MIT
// Decodes incoming UDP datagrams and routes them to a registered
// handler. The dispatcher also implements IUdpDatagramSink so the
// listener can hand raw datagrams to it without coupling to the
// transport.
//
// Handlers may take scoped dependencies (DB contexts, etc.), so the
// dispatcher creates a DI scope per datagram and resolves handlers
// from that scope. The scope is disposed when the handler completes.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Protocol.Codec;
using CS2M.ApiServer.Protocol.Udp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CS2M.ApiServer.Protocol.Dispatch;

public interface IApiCommandHandler
{
    Type CommandType { get; }
    Task HandleAsync(ApiCommandBase command, IPEndPoint remote, CancellationToken cancellationToken);
}

public abstract class ApiCommandHandler<T> : IApiCommandHandler where T : ApiCommandBase
{
    public Type CommandType => typeof(T);
    public Task HandleAsync(ApiCommandBase command, IPEndPoint remote, CancellationToken cancellationToken)
        => HandleAsync((T)command, remote, cancellationToken);
    protected abstract Task HandleAsync(T command, IPEndPoint remote, CancellationToken cancellationToken);
}

public sealed class ApiCommandDispatcher : IUdpDatagramSink
{
    private readonly IApiCommandCodec _codec;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiCommandDispatcher> _logger;

    public ApiCommandDispatcher(
        IApiCommandCodec codec,
        IServiceScopeFactory scopeFactory,
        ILogger<ApiCommandDispatcher> logger)
    {
        _codec = codec;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Handle(UdpDatagram datagram)
    {
        if (!_codec.TryDecode(datagram.Payload, out var command) || command is null)
        {
            _logger.LogWarning(
                "Dropped undecodable datagram of {Size} bytes from {Peer}",
                datagram.Payload.Length, datagram.RemoteEndPoint);
            return;
        }

        _ = Task.Run(async () =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handlers = scope.ServiceProvider.GetServices<IApiCommandHandler>();
            var handler = handlers.FirstOrDefault(h => h.CommandType == command.GetType());
            if (handler is null)
            {
                _logger.LogWarning(
                    "No handler registered for command {CommandType} from {Peer}",
                    command.GetType().Name, datagram.RemoteEndPoint);
                return;
            }
            try
            {
                await handler.HandleAsync(command, datagram.RemoteEndPoint, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Handler {HandlerType} threw while processing {CommandType} from {Peer}",
                    handler.GetType().Name, command.GetType().Name, datagram.RemoteEndPoint);
            }
        });
    }
}