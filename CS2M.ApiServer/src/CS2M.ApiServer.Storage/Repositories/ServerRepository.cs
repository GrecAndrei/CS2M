// SPDX-License-Identifier: MIT
// Thin repository over ApiServerDbContext. Keeps the EF details out
// of the handlers and makes handlers easy to unit-test with an
// in-memory fake.

using CS2M.ApiServer.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace CS2M.ApiServer.Storage.Repositories;

public interface IServerRepository
{
    /// <summary>
    ///     Upsert a server record based on its token. Returns true if
    ///     this is the first time we see the token, false on update.
    /// </summary>
    Task<bool> UpsertFromRegistrationAsync(
        string token,
        string localIp,
        int localPort,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ServerRecord?> FindAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServerRecord>> ListAsync(
        bool publicOnly,
        DateTimeOffset? since,
        int take,
        CancellationToken cancellationToken);

    Task<int> DeleteStaleAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

public sealed class ServerRepository : IServerRepository
{
    private readonly ApiServerDbContext _db;

    public ServerRepository(ApiServerDbContext db)
    {
        _db = db;
    }

    public async Task<bool> UpsertFromRegistrationAsync(
        string token,
        string localIp,
        int localPort,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Servers
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.Servers.Add(new ServerRecord
            {
                Token = token,
                LocalIp = localIp,
                LocalPort = localPort,
                RegisteredAt = now,
                LastHeartbeatAt = now,
                IsPublic = true
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        existing.LocalIp = localIp;
        existing.LocalPort = localPort;
        existing.LastHeartbeatAt = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return false;
    }

    public Task<ServerRecord?> FindAsync(string token, CancellationToken cancellationToken)
        => _db.Servers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

    public async Task<IReadOnlyList<ServerRecord>> ListAsync(
        bool publicOnly,
        DateTimeOffset? since,
        int take,
        CancellationToken cancellationToken)
    {
        var q = _db.Servers.AsNoTracking().AsQueryable();
        if (publicOnly)
        {
            q = q.Where(s => s.IsPublic);
        }
        if (since is not null)
        {
            q = q.Where(s => s.LastHeartbeatAt >= since.Value);
        }
        return await q
            .OrderByDescending(s => s.LastHeartbeatAt)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> DeleteStaleAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var stale = await _db.Servers
            .Where(s => s.LastHeartbeatAt < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (stale.Count == 0)
        {
            return 0;
        }
        _db.Servers.RemoveRange(stale);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return stale.Count;
    }
}