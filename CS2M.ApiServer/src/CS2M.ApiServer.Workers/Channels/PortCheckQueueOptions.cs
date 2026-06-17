// SPDX-License-Identifier: MIT
// Options for the port-check queue: capacity and worker fan-out.

namespace CS2M.ApiServer.Workers.Channels;

public sealed class PortCheckQueueOptions
{
    public int Capacity { get; set; } = 4096;
    public int MaxParallelProbes { get; set; } = 32;
    public int ProbeTimeoutMs { get; set; } = 3000;
}