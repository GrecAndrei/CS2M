// SPDX-License-Identifier: MIT
// EF Core entity for the `servers` table.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CS2M.ApiServer.Storage.Entities;

[Table("servers")]
public class ServerRecord
{
    /// <summary>
    ///     The opaque bearer token a game server uses to identify
    ///     itself. PK.
    /// </summary>
    [Key]
    [Column("token")]
    [MaxLength(128)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    ///     The game server's self-reported local network IP.
    /// </summary>
    [Column("local_ip")]
    [MaxLength(64)]
    [Required]
    public string LocalIp { get; set; } = string.Empty;

    [Column("local_port")]
    public int LocalPort { get; set; }

    /// <summary>
    ///     Optional public endpoint (host:port) populated once a
    ///     NAT-traversal helper resolves the address. Empty for now.
    /// </summary>
    [Column("public_endpoint")]
    [MaxLength(256)]
    public string? PublicEndpoint { get; set; }

    [Column("last_heartbeat_at")]
    public DateTimeOffset LastHeartbeatAt { get; set; }

    [Column("registered_at")]
    public DateTimeOffset RegisteredAt { get; set; }

    [Column("is_public")]
    public bool IsPublic { get; set; } = true;

    [Column("version")]
    [MaxLength(64)]
    public string? Version { get; set; }

    [Column("display_name")]
    [MaxLength(128)]
    public string? DisplayName { get; set; }
}