// SPDX-License-Identifier: MIT
// Small helper that handlers can use to reply to the peer that sent
// an incoming command. Encapsulates the codec + listener coupling
// so handler code does not need to know the transport exists.
//
// The replier is created lazily via a factory because the listener
// is a BackgroundService that only knows how to send datagrams
// after ExecuteAsync has run. Using a Func<ApiUdpListener> breaks
// the singleton construction cycle between the listener, the
// dispatcher, and the handlers.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Protocol.Codec;
using CS2M.ApiServer.Protocol.Udp;

namespace CS2M.ApiServer.Protocol.Dispatch;

public interface IApiCommandReplier
{
    Task ReplyAsync(ApiCommandBase command, IPEndPoint remote, CancellationToken cancellationToken = default);
}

public sealed class ApiCommandReplier : IApiCommandReplier
{
    private readonly IApiCommandCodec _codec;
    private readonly Func<ApiUdpListener> _listener;

    public ApiCommandReplier(IApiCommandCodec codec, Func<ApiUdpListener> listener)
    {
        _codec = codec;
        _listener = listener;
    }

    public Task ReplyAsync(ApiCommandBase command, IPEndPoint remote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(remote);
        var bytes = _codec.Encode(command);
        return _listener().SendAsync(bytes, remote, cancellationToken);
    }
}