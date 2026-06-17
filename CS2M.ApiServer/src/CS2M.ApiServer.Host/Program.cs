// SPDX-License-Identifier: MIT
// ASP.NET Core host for the CS2M API server.
//
// Wiring (M1):
//   - Bound on 0.0.0.0:8080 for HTTP (health endpoint, REST API later)
//   - Bound on 0.0.0.0:4242 for UDP (CS2M API traffic)
//   - The UDP listener is a BackgroundService that pumps datagrams
//     into the dispatcher, which decodes them and runs registered
//     handlers in the background.

using CS2M.ApiServer.Protocol.Codec;
using CS2M.ApiServer.Protocol.Dispatch;
using CS2M.ApiServer.Protocol.Dispatch.Handlers;
using CS2M.ApiServer.Protocol.Udp;
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

// Codec + listener + dispatcher.
builder.Services.AddSingleton<IApiCommandCodec, ApiCommandCodec>();
builder.Services.AddSingleton<ApiUdpListener>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ApiUdpListener>());
builder.Services.AddSingleton<IApiCommandReplier, ApiCommandReplier>();
builder.Services.AddSingleton<IUdpDatagramSink, ApiCommandDispatcher>();
builder.Services.AddSingleton<ApiCommandDispatcher>();
builder.Services.AddSingleton<IApiCommandHandler, ServerRegistrationHandler>();
builder.Services.AddSingleton<IApiCommandHandler, PortCheckRequestHandler>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { name = "CS2M.ApiServer", version = "0.1.0-m1" }));
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// The UDP listener is registered as a hosted service above. The
// ApplicationStarted hook logs the bound endpoint for visibility.
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
