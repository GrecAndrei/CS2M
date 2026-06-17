// SPDX-License-Identifier: MIT
// Protocol mirror — see ApiCommandBase.cs for sync instructions.

namespace CS2M.ApiServer.Core.Commands;

/// <summary>
///     Sent by the game server (host).
///     Semantics: "Please TCP-probe this port on the IP I last
///     registered with, and tell me whether it's reachable from the
///     public internet."
/// </summary>
public sealed class PortCheckRequestCommand : ApiCommandBase
{
    /// <summary>
    ///     The port to check.
    /// </summary>
    public int Port { get; set; }
}
