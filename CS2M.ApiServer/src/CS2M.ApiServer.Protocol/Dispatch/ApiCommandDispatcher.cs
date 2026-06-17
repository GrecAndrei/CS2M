// SPDX-License-Identifier: MIT
// Decodes incoming UDP datagrams and routes them to a registered
// handler. The dispatcher also implements IUdpDatagramSink so the
// listener can hand raw datagrams to it without coupling to the
// transport.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Protocol.Codec;
using CS2M.ApiServer.Protocol.Udp;
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
    private readonly IReadOnlyDictionary<Type, IApiCommandHandler> _handlers;
    private readonly ILogger<ApiCommandDispatcher> _logger;

    public ApiCommandDispatcher(
        IApiCommandCodec codec,
        IEnumerable<IApiCommandHandler> handlers,
        ILogger<ApiCommandDispatcher> logger)
    {
        _codec = codec;
        _logger = logger;
        _handlers = handlers.ToDictionary(h => h.CommandType);
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

        var type = command.GetType();
        if (!_handlers.TryGetValue(type, out var handler))
        {
            _logger.LogWarning(
                "No handler registered for command {CommandType} from {Peer}",
                type.Name, datagram.RemoteEndPoint);
            return;
        }

        _ = Task.Run(() => handler.HandleAsync(command, datagram.RemoteEndPoint, CancellationToken.None));
    }
}
