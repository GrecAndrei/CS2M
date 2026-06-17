// SPDX-License-Identifier: MIT
// Public REST surface for the CS2M API server. JSON, not MessagePack.

using CS2M.ApiServer.Core.Validation;
using CS2M.ApiServer.Storage.Entities;
using CS2M.ApiServer.Storage.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CS2M.ApiServer.Host.Controllers;

[ApiController]
[Route("api/v1/servers")]
[Produces("application/json")]
public class ServersController : ControllerBase
{
    private readonly IServerRepository _servers;
    private readonly ILogger<ServersController> _logger;

    public ServersController(IServerRepository servers, ILogger<ServersController> logger)
    {
        _servers = servers;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ServerListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] bool publicOnly = false,
        [FromQuery] DateTimeOffset? since = null,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        var rows = await _servers.ListAsync(publicOnly, since, take, cancellationToken).ConfigureAwait(false);
        var projection = rows.Select(MapListItem).ToList();
        return Ok(projection);
    }

    [HttpGet("{token}")]
    [ProducesResponseType(typeof(ServerDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(string token, CancellationToken cancellationToken)
    {
        if (!TokenValidator.IsWellFormed(token, out var error))
        {
            return Problem(statusCode: 400, title: "invalid_token", detail: error);
        }
        var row = await _servers.FindAsync(token, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return Problem(statusCode: 404, title: "not_found", detail: "no such server token");
        }
        return Ok(MapDetail(row));
    }

    private static ServerListItem MapListItem(ServerRecord r) => new()
    {
        Token = r.Token,
        PublicEndpoint = r.PublicEndpoint,
        LastHeartbeatAt = r.LastHeartbeatAt,
        Version = r.Version,
        DisplayName = r.DisplayName
    };

    private static ServerDetail MapDetail(ServerRecord r) => new()
    {
        Token = r.Token,
        LocalIp = r.LocalIp,
        LocalPort = r.LocalPort,
        PublicEndpoint = r.PublicEndpoint,
        LastHeartbeatAt = r.LastHeartbeatAt,
        RegisteredAt = r.RegisteredAt,
        IsPublic = r.IsPublic,
        Version = r.Version,
        DisplayName = r.DisplayName
    };
}

public sealed class ServerListItem
{
    public string Token { get; set; } = string.Empty;
    public string? PublicEndpoint { get; set; }
    public DateTimeOffset LastHeartbeatAt { get; set; }
    public string? Version { get; set; }
    public string? DisplayName { get; set; }
}

public sealed class ServerDetail
{
    public string Token { get; set; } = string.Empty;
    public string LocalIp { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string? PublicEndpoint { get; set; }
    public DateTimeOffset LastHeartbeatAt { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public bool IsPublic { get; set; }
    public string? Version { get; set; }
    public string? DisplayName { get; set; }
}