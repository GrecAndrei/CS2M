// SPDX-License-Identifier: MIT
// Protocol mirror — see ApiCommandBase.cs for sync instructions.

namespace CS2M.ApiServer.Core.Commands;

/// <summary>
///     Sent by the API server back to the game server.
///     Semantics: "Here's the answer to your last PortCheckRequest."
/// </summary>
public sealed class PortCheckResultCommand : ApiCommandBase
{
    /// <summary>
    ///     The determined state of the checked port.
    /// </summary>
    public PortCheckResult State { get; set; }

    /// <summary>
    ///     The error message in case the port check failed.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

public enum PortCheckResult
{
    Reachable = 0,
    Unreachable = 1,
    Error = 2
}
