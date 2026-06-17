// SPDX-License-Identifier: MIT
// EF Core entity for the `port_check_results` table.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CS2M.ApiServer.Storage.Entities;

[Table("port_check_results")]
public class PortCheckResultRecord
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("token")]
    [MaxLength(128)]
    public string Token { get; set; } = string.Empty;

    [Column("port")]
    public int Port { get; set; }

    /// <summary>
    ///     0 = Reachable, 1 = Unreachable, 2 = Error.
    ///     Stored as smallint for forward-compatibility with new states.
    /// </summary>
    [Column("state")]
    public short State { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [Column("checked_at")]
    public DateTimeOffset CheckedAt { get; set; }
}