// SPDX-License-Identifier: MIT
// ASP.NET Core WebApplicationFactory tuned for the API server tests.
// - Picks a random local UDP port so tests can run in parallel without
//   colliding on 4242.
// - Overrides the listen address to "127.0.0.1" so the test never
//   touches a public interface.
// - Replaces the Postgres-backed DbContext with a SQLite in-memory
//   instance so tests do not need a running database.
// - Exposes a helper that waits for the UDP listener to bind before
//   returning control to test code.

using System.Net;
using System.Net.Sockets;
using CS2M.ApiServer.Protocol.Udp;
using CS2M.ApiServer.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CS2M.ApiServer.Tests.Helpers;

public sealed class ApiServerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public int UdpPort { get; } = FindFreeUdpPort();

    private SqliteConnection? _sqlite;
    private string? _sqliteConnectionString;

    public async Task InitializeAsync()
    {
        // Open a shared in-memory SQLite connection. The default
        // "Filename=:memory:" creates one private database per
        // connection; adding Cache=Shared makes subsequent
        // connections land on the same store.
        _sqlite = new SqliteConnection("Filename=file::memory:?cache=shared");
        await _sqlite.OpenAsync();
        _sqliteConnectionString = _sqlite.ConnectionString;
        // Force the host to build so the SQLite DbContext is registered
        // before we call EnsureCreated().
        _ = Server;
        await EnsureSqliteSchemaAsync().ConfigureAwait(false);
    }

    private async Task EnsureSqliteSchemaAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiServerDbContext>();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_sqlite is not null)
        {
            await _sqlite.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Udp:Port"] = UdpPort.ToString(),
                ["Udp:BindAddress"] = "127.0.0.1",
                ["Urls"] = "http://127.0.0.1:0"
            });
        });
        builder.ConfigureServices(services =>
        {
            // Remove every EF-registered descriptor for ApiServerDbContext
            // (DbContextOptions<T>, DbContextOptions, the context itself,
            // any factory it might have added) and replace with SQLite.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApiServerDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || d.ServiceType == typeof(ApiServerDbContext))
                .ToList();
            foreach (var d in toRemove)
            {
                services.Remove(d);
            }
            services.AddDbContext<ApiServerDbContext>(opt =>
                opt.UseSqlite(_sqliteConnectionString!));
        });
    }

    public async Task WaitForUdpListenerAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var listener = Services.GetRequiredService<ApiUdpListener>();
        while (DateTime.UtcNow < deadline)
        {
            if (listener.LocalEndpoint is not null)
            {
                return;
            }
            await Task.Delay(50).ConfigureAwait(false);
        }
        throw new TimeoutException("UDP listener did not bind within timeout.");
    }

    private static int FindFreeUdpPort()
    {
        var probe = new UdpClient(0);
        try
        {
            return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
        }
        finally
        {
            probe.Close();
        }
    }
}