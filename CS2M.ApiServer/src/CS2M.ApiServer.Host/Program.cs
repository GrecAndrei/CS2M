// SPDX-License-Identifier: MIT
// ASP.NET Core host for the CS2M API server.

using CS2M.ApiServer.Core.Dispatch;
using CS2M.ApiServer.Protocol.Codec;
using CS2M.ApiServer.Protocol.Dispatch;
using CS2M.ApiServer.Protocol.Dispatch.Handlers;
using CS2M.ApiServer.Protocol.Udp;
using CS2M.ApiServer.Storage;
using CS2M.ApiServer.Storage.Repositories;
using CS2M.ApiServer.Workers.Channels;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Structured logging. JSON to stdout, level driven by configuration.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithProperty("Application", "CS2M.ApiServer")
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// Options.
builder.Services.Configure<UdpListenerOptions>(builder.Configuration.GetSection("Udp"));
builder.Services.Configure<UdpRateLimiterOptions>(builder.Configuration.GetSection("UdpRateLimiter"));

// PostgreSQL via EF Core 8.
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=127.0.0.1;Port=5432;Username=cs2m;Password=cs2m;Database=cs2m";
builder.Services.AddDbContext<ApiServerDbContext>(opt =>
    opt.UseNpgsql(connectionString));
builder.Services.AddScoped<IServerRepository, ServerRepository>();

// Bounded work queue shared by the dispatcher and the probe worker.
builder.Services.Configure<PortCheckQueueOptions>(builder.Configuration.GetSection("PortCheckQueue"));
builder.Services.AddSingleton<PortCheckQueue>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PortCheckQueueOptions>>().Value;
    var channel = System.Threading.Channels.Channel.CreateBounded<CS2M.ApiServer.Core.Dispatch.IPortCheckWorkItem>(
        new System.Threading.Channels.BoundedChannelOptions(opts.Capacity)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    return new PortCheckQueue(channel);
});
builder.Services.AddSingleton<CS2M.ApiServer.Core.Dispatch.IPortCheckEnqueuer>(sp =>
    sp.GetRequiredService<PortCheckQueue>());

// Codec + listener + dispatcher.
builder.Services.AddSingleton<IApiCommandCodec, ApiCommandCodec>();
builder.Services.AddSingleton<ApiUdpListener>();
builder.Services.AddSingleton<CS2M.ApiServer.Core.Dispatch.IProbeReplyChannel>(sp => sp.GetRequiredService<ApiUdpListener>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ApiUdpListener>());
builder.Services.AddSingleton<IApiCommandReplier>(sp => new ApiCommandReplier(
    sp.GetRequiredService<IApiCommandCodec>(),
    () => sp.GetRequiredService<ApiUdpListener>()));
builder.Services.AddSingleton<ApiCommandDispatcher>();
builder.Services.AddSingleton<IUdpDatagramSink>(sp =>
{
    var inner = sp.GetRequiredService<ApiCommandDispatcher>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UdpRateLimiterOptions>>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UdpRateLimiter>>();
    return new UdpRateLimiter(inner, options, logger);
});
builder.Services.AddScoped<IApiCommandHandler, ServerRegistrationHandler>();
builder.Services.AddScoped<IApiCommandHandler, PortCheckRequestHandler>();

// Background workers.
builder.Services.Configure<CS2M.ApiServer.Workers.StaleServerReaperOptions>(
    builder.Configuration.GetSection("StaleServerReaper"));
builder.Services.AddHostedService<CS2M.ApiServer.Workers.PortReachableChecker>();
builder.Services.AddHostedService<CS2M.ApiServer.Workers.StaleServerReaper>();

// REST API surface.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Prometheus metrics — captured on the main HTTP endpoint at /metrics.

// Build the app.
var app = builder.Build();

// Apply EF migrations on startup so a fresh Postgres doesn't need a
// separate migration step. Safe to run repeatedly because EF only
// applies pending migrations. For non-Postgres providers (e.g. the
// SQLite in-memory store used in tests) EnsureCreated is used so the
// schema is built without needing a migration bundle. We log and
// continue if the migration fails so the UDP listener can still
// accept traffic while the database is temporarily unavailable.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApiServerDbContext>();
    var provider = db.Database.ProviderName ?? string.Empty;
    try
    {
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }
        else
        {
            await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
            app.Logger.LogInformation(
                "Database schema ensured via EnsureCreated (provider={Provider}).",
                provider);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex,
            "Database initialisation failed; continuing without an initialised schema. " +
            "The /readyz probe will report the database as down until the issue is resolved.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();

// Liveness and readiness probes.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", async (ApiServerDbContext db, ApiUdpListener listener) =>
{
    var dbOk = false;
    try
    {
        dbOk = await db.Database.CanConnectAsync().ConfigureAwait(false);
    }
    catch
    {
        dbOk = false;
    }
    var udpOk = listener.LocalEndpoint is not null;
    var ready = dbOk && udpOk;
    return Results.Json(new
    {
        status = ready ? "ready" : "not_ready",
        database = dbOk ? "ok" : "down",
        udp = udpOk ? "bound" : "not_bound",
        udpEndpoint = listener.LocalEndpoint?.ToString()
    }, statusCode: ready ? 200 : 503);
});

app.MapGet("/", () => Results.Ok(new { name = "CS2M.ApiServer", version = "0.1.0-m2" }));

app.Lifetime.ApplicationStarted.Register(() =>
{
    var listener = app.Services.GetRequiredService<ApiUdpListener>();
    app.Logger.LogInformation(
        "CS2M.ApiServer ready (UDP: {LocalEndpoint}, HTTP: {Urls})",
        listener.LocalEndpoint,
        string.Join(", ", app.Urls));
});

app.Run();

// Required for WebApplicationFactory<T> in tests.
public partial class Program;