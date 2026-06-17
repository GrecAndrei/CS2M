// SPDX-License-Identifier: MIT
// Raw UDP listener for CS2M API traffic. We do not use LiteNetLib on
// the server side: the mod's SendUnconnectedMessage emits a plain UDP
// datagram containing one MessagePack payload, so a vanilla
// System.Net.Sockets.UdpClient is enough and removes a dependency that
// pulls in a full game-networking stack.
//
// The listener runs as an IHostedService, not as a background
// thread inside a request handler, because ASP.NET Core does not own
// a dedicated thread for this and we need a tight receive loop.

using System.Net;
using System.Net.Sockets;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Workers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CS2M.ApiServer.Protocol.Udp;

public sealed class UdpListenerOptions
{
    public int Port { get; set; } = 4242;
    public string BindAddress { get; set; } = "0.0.0.0";
    public int ReceiveBufferSize { get; set; } = 4096;
    public int MaxDatagramSize { get; set; } = 1500;
}

public sealed class UdpDatagram
{
    public required IPEndPoint RemoteEndPoint { get; init; }
    public required byte[] Payload { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
}

public interface IUdpDatagramSink
{
    void Handle(UdpDatagram datagram);
}

public sealed class ApiUdpListener : BackgroundService, IProbeReplyChannel
{
    private readonly ILogger<ApiUdpListener> _logger;
    private readonly UdpListenerOptions _options;
    private readonly IUdpDatagramSink _sink;
    private UdpClient? _client;

    public ApiUdpListener(
        IOptions<UdpListenerOptions> options,
        IUdpDatagramSink sink,
        ILogger<ApiUdpListener> logger)
    {
        _options = options.Value;
        _sink = sink;
        _logger = logger;
    }

    public IPEndPoint? LocalEndpoint => _client?.Client.LocalEndPoint as IPEndPoint;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var local = ResolveBindEndpoint();
        _client = new UdpClient(local)
        {
            Client =
            {
                ReceiveBufferSize = _options.ReceiveBufferSize
            }
        };
        _logger.LogInformation(
            "UDP API listener bound to {LocalEndpoint}", _client.Client.LocalEndPoint);

        // Fire-and-forget receive loop. Exceptions are caught and
        // logged so a single malformed peer cannot take the listener
        // down.
        _ = Task.Run(() => ReceiveLoopAsync(stoppingToken), stoppingToken);
        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken stoppingToken)
    {
        if (_client is null)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _client.ReceiveAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "UDP receive failed; pausing briefly");
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (result.Buffer.Length > _options.MaxDatagramSize)
            {
                _logger.LogWarning(
                    "Dropping oversized datagram of {Size} bytes from {Peer}",
                    result.Buffer.Length, result.RemoteEndPoint);
                continue;
            }

            try
            {
                _sink.Handle(new UdpDatagram
                {
                    RemoteEndPoint = result.RemoteEndPoint,
                    Payload = result.Buffer,
                    ReceivedAt = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Sink threw while handling datagram from {Peer}",
                    result.RemoteEndPoint);
            }
        }
    }

    /// <summary>
    ///     Send a one-shot datagram. Used by the API server to reply to
    ///     PortCheckRequestCommand with a PortCheckResultCommand, and by
    ///     background workers to ping stale game servers.
    /// </summary>
    /// <summary>
    ///     Send a one-shot datagram. Used by the API server to reply to
    ///     PortCheckRequestCommand with a PortCheckResultCommand, and by
    ///     background workers to ping stale game servers.
    /// </summary>
    public async Task SendAsync(byte[] payload, IPEndPoint target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(target);
        if (_client is null)
        {
            throw new InvalidOperationException("UDP listener is not running.");
        }
        await _client
            .SendAsync(payload, payload.Length, target)
            .ConfigureAwait(false);
    }

    async Task IProbeReplyChannel.SendAsync(ApiCommandBase command, IPEndPoint remote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(remote);
        var bytes = _codec is null
            ? System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(command)
            : _codec.Encode(command);
        await SendAsync(bytes, remote, cancellationToken).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        if (_client is not null)
        {
            try
            {
                _client.Close();
            }
            catch
            {
                // ignored; shutdown best-effort
            }
            _client = null;
        }
    }

    private IPEndPoint ResolveBindEndpoint()
    {
        if (IPAddress.TryParse(_options.BindAddress, out var ip))
        {
            return new IPEndPoint(ip, _options.Port);
        }
        // Treat as DNS name; resolve first IPv4 entry.
        var addresses = Dns.GetHostAddresses(_options.BindAddress);
        var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                   ?? addresses.FirstOrDefault()
                   ?? IPAddress.Any;
        return new IPEndPoint(ipv4, _options.Port);
    }
}
