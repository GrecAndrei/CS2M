// SPDX-License-Identifier: MIT
// Abstraction the UDP dispatcher uses to enqueue port-check work
// without taking a hard dependency on the Workers project. The
// implementation lives in CS2M.ApiServer.Workers.

namespace CS2M.ApiServer.Core.Dispatch;

public interface IPortCheckEnqueuer
{
    /// <summary>
    ///     Try to enqueue a port check for the given remote endpoint
    ///     and port. Returns false if the queue is full and the work
    ///     item was dropped (under <see cref="Dispatch.BoundedChannelFullMode.DropOldest"/>).
    /// </summary>
    bool TryEnqueue(IPortCheckWorkItem work);
}

public interface IPortCheckWorkItem
{
    string Token { get; }
    string LocalIp { get; }
    int LocalPort { get; }
    int Port { get; }
    DateTimeOffset EnqueuedAt { get; }
}

public sealed record PortCheckWorkItem(
    string Token,
    string LocalIp,
    int LocalPort,
    int Port,
    DateTimeOffset EnqueuedAt) : IPortCheckWorkItem;