// SPDX-License-Identifier: MIT
// EF Core database context for the CS2M API server.
//
// Schema (PostgreSQL 16):
//   servers (
//     token              text primary key,
//     local_ip           text not null,
//     local_port         int  not null,
//     public_endpoint    text,
//     last_heartbeat_at  timestamptz not null,
//     registered_at      timestamptz not null,
//     is_public          boolean not null default true,
//     version            text,
//     display_name       text
//   )
//   port_check_results (
//     id                 bigserial primary key,
//     token              text not null references servers(token)
//                                     on delete cascade,
//     port               int  not null,
//     state              smallint not null,  -- 0 Reachable, 1 Unreachable, 2 Error
//     message            text,
//     checked_at         timestamptz not null
//   )

using CS2M.ApiServer.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace CS2M.ApiServer.Storage;

public sealed class ApiServerDbContext : DbContext
{
    public ApiServerDbContext(DbContextOptions<ApiServerDbContext> options) : base(options)
    {
    }

    public DbSet<ServerRecord> Servers => Set<ServerRecord>();
    public DbSet<PortCheckResultRecord> PortCheckResults => Set<PortCheckResultRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServerRecord>(e =>
        {
            e.Property(p => p.Token).IsRequired();
            e.HasIndex(p => p.LastHeartbeatAt);
            e.HasIndex(p => p.IsPublic);
        });

        modelBuilder.Entity<PortCheckResultRecord>(e =>
        {
            e.HasOne<ServerRecord>()
                .WithMany()
                .HasForeignKey(p => p.Token)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.Token, p.CheckedAt });
        });
    }
}