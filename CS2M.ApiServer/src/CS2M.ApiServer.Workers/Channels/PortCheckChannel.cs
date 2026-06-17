// SPDX-License-Identifier: MIT
// Channel used by the UDP dispatcher to hand port-check work items
// to the PortReachableChecker BackgroundService. The channel is
// bounded so a malicious or misconfigured peer cannot enqueue an
// unbounded number of probes and OOM the server.

using System.Threading.Channels;
using CS2M.ApiServer.Core.Dispatch;

namespace CS2M.ApiServer.Workers.Channels;

public sealed record PortCheckWorkItem(
    string Token,
    string LocalIp,
    int LocalPort,
    int PortToProbe,
    DateTimeOffset EnqueuedAt) : IPortCheckWorkItem
{
    int IPortCheckWorkItem.Port => PortToProbe;
}

public sealed class PortCheckQueue : IPortCheckEnqueuer
{
    private readonly Channel<PortCheckWorkItem> _channel;

    public PortCheckQueue(Channel<PortCheckWorkItem> channel)
    {
        _channel = channel;
    }

    public ChannelReader<PortCheckWorkItem> Reader => _channel.Reader;
    public ChannelWriter<PortCheckWorkItem> Writer => _channel.Writer;

    bool IPortCheckEnqueuer.TryEnqueue(IPortCheckWorkItem work)
    {
        if (work is not PortCheckWorkItem item)
        {
            return false;
        }
        return _channel.Writer.TryWrite(item);
    }
}