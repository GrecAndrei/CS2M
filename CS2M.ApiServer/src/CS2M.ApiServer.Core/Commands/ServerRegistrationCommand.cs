// SPDX-License-Identifier: MIT
// Protocol mirror — see ApiCommandBase.cs for sync instructions.

namespace CS2M.ApiServer.Core.Commands;

/// <summary>
///     Sent by the game server (host) every 5 minutes as a keepalive.
///     Semantics: "I, a game server, am reachable on LocalIp:LocalPort
///     and I want clients to be able to find me by Token."
/// </summary>
public sealed class ServerRegistrationCommand : ApiCommandBase
{
    /// <summary>
    ///     The server token to register.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    ///     The server's IP address in the local network.
    /// </summary>
    public string LocalIp { get; set; } = string.Empty;

    /// <summary>
    ///     The configured local port.
    /// </summary>
    public int LocalPort { get; set; }
}
