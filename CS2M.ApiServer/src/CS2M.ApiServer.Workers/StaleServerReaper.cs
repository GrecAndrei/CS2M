// SPDX-License-Identifier: MIT
// Periodically deletes server registrations whose
// last_heartbeat_at is older than the configured threshold.
// The mod sends a ServerRegistrationCommand every 5 minutes;
// if we miss 3 in a row (15 minutes) the record is stale.

using CS2M.ApiServer.Storage.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CS2M.ApiServer.Workers;

public sealed class StaleServerReaperOptions
{
    public int IntervalSeconds { get; set; } = 60;
    public int StaleAfterMinutes { get; set; } = 15;
}

public sealed class StaleServerReaper : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleServerReaper> _logger;
    private readonly StaleServerReaperOptions _options;

    public StaleServerReaper(
        IServiceScopeFactory scopeFactory,
        IOptions<StaleServerReaperOptions> options,
        ILogger<StaleServerReaper> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "StaleServerReaper started (interval={Interval}s, stale={Stale}m)",
            _options.IntervalSeconds, _options.StaleAfterMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var servers = scope.ServiceProvider.GetRequiredService<IServerRepository>();
                var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_options.StaleAfterMinutes);
                var deleted = await servers
                    .DeleteStaleAsync(cutoff, stoppingToken)
                    .ConfigureAwait(false);

                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Reaped {Count} stale server registration(s) (cutoff={Cutoff:o})",
                        deleted, cutoff);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StaleServerReaper sweep failed");
            }
        }
    }
}
