// SPDX-License-Identifier: MIT
// Lightweight helper that simulates a CS2M game server for use in
// integration tests. Sends a single MessagePack command to the API
// server and waits for a matching reply (or times out).

using System.Net;
using System.Net.Sockets;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Protocol.Codec;
using MessagePack;

namespace CS2M.ApiServer.Tests.Helpers;

public sealed class TestUdpClient : IDisposable
{
    private readonly UdpClient _socket;
    private readonly IApiCommandCodec _codec;

    public TestUdpClient(IApiCommandCodec? codec = null)
    {
        _socket = new UdpClient(0); // bind to ephemeral port
        _codec = codec ?? new ApiCommandCodec();
    }

    public IPEndPoint LocalEndpoint => (IPEndPoint)_socket.Client.LocalEndPoint!;

    public async Task SendAsync(ApiCommandBase command, IPEndPoint target, CancellationToken cancellationToken = default)
    {
        var payload = _codec.Encode(command);
        await _socket.SendAsync(payload, payload.Length, target).ConfigureAwait(false);
    }

    public async Task<ApiCommandBase> SendAndReceiveAsync(
        ApiCommandBase command,
        IPEndPoint target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(command, target, cancellationToken).ConfigureAwait(false);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            var result = await _socket.ReceiveAsync(cts.Token).ConfigureAwait(false);
            if (!_codec.TryDecode(result.Buffer, out var reply) || reply is null)
            {
                throw new InvalidOperationException("Failed to decode reply datagram.");
            }
            return reply;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"No reply from API server within {timeout.TotalSeconds}s.");
        }
    }

    public void Dispose()
    {
        try
        {
            _socket.Close();
        }
        catch
        {
            // best-effort
        }
        _socket.Dispose();
    }
}