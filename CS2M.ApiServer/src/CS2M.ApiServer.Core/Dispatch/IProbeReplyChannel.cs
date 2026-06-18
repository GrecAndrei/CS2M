using System.Net;
using CS2M.ApiServer.Core.Commands;

namespace CS2M.ApiServer.Core.Dispatch;

/// <summary>
///     Abstraction over the UDP listener so the PortReachableChecker
///     worker can reply to a game server without taking a direct
///     dependency on the listener type.
/// </summary>
public interface IProbeReplyChannel
{
    Task SendAsync(ApiCommandBase command, IPEndPoint remote, CancellationToken cancellationToken);
}
