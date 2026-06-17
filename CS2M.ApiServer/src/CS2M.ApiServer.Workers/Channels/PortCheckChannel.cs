// SPDX-License-Identifier: MIT
// Channel used by the UDP dispatcher to hand port-check work items
// to the PortReachableChecker BackgroundService. The channel is
// bounded so a malicious or misconfigured peer cannot enqueue an
// unbounded number of probes and OOM the server.
//
// Items flow through the channel as the Core abstraction
// IPortCheckWorkItem so the dispatcher doesn't need to know about
// the Workers project's concrete record type. The queue itself is
// typed on the abstract interface for maximum decoupling.

using System.Threading.Channels;
using CS2M.ApiServer.Core.Dispatch;

namespace CS2M.ApiServer.Workers.Channels;

public sealed class PortCheckQueue : IPortCheckEnqueuer
{
    private readonly Channel<IPortCheckWorkItem> _channel;

    public PortCheckQueue(Channel<IPortCheckWorkItem> channel)
    {
        _channel = channel;
    }

    public ChannelReader<IPortCheckWorkItem> Reader => _channel.Reader;

    bool IPortCheckEnqueuer.TryEnqueue(IPortCheckWorkItem work) =>
        _channel.Writer.TryWrite(work);
}