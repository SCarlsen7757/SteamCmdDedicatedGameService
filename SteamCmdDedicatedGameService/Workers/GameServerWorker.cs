using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SteamCmdDedicatedGameService.Models;
using SteamCmdDedicatedGameService.Services;

using System.ComponentModel;

namespace SteamCmdDedicatedGameService.Workers;

/// <summary>
/// Main background service that orchestrates the full game server lifecycle:
/// SteamCMD validation → Install/Update → Start → Monitor → Restart on failure.
/// </summary>
public sealed class GameServerWorker(
    ILogger<GameServerWorker> logger,
    IHostApplicationLifetime appLifetime,
    SteamCmdService steamCmd,
    BaseGameServerService gameServer,
    HealthCheckService healthCheckService,
    IOptions<GameServerConfiguration> gameOptions,
    IOptions<HealthCheckConfiguration> healthOptions) : BackgroundService
{
    private readonly GameServerConfiguration _gameConfig = gameOptions.Value;
    private readonly HealthCheckConfiguration _healthConfig = healthOptions.Value;
    private int _consecutiveFailures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("GameServerWorker starting for App ID {AppId}...", _gameConfig.AppId);

        // Validate SteamCMD is available
        if (!steamCmd.Validate())
        {
            logger.LogCritical("SteamCMD validation failed. Service cannot start.");
            Environment.ExitCode = 1;
            appLifetime.StopApplication();
            return;
        }

        try
        {
            // Initial install/update + start
            await RunUpdateAndStartCycleAsync(stoppingToken);

            // Main monitoring loop
            await MonitorLoopAsync(stoppingToken);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 740)
        {
            logger.LogCritical(ex, "The game server requires administrator privileges. Stopping the service.");
            Environment.ExitCode = 1;
            appLifetime.StopApplication();
            return;
        }

        // Cleanup on stop
        logger.LogInformation("GameServerWorker stopping. Shutting down game server...");
        await gameServer.StopAsync(stoppingToken);
    }

    private async Task RunUpdateAndStartCycleAsync(CancellationToken cancellationToken)
    {
        // Install / Update
        bool updated = await steamCmd.InstallOrUpdateGameServerAsync(
            _gameConfig.AppId,
            _gameConfig.InstallDirectory,
            cancellationToken);

        if (!updated)
        {
            logger.LogWarning("SteamCMD update failed. Attempting to start server with existing files...");
        }

        // Start the game server
        bool started = await gameServer.StartAsync(_gameConfig, _healthConfig, cancellationToken);
        if (!started)
        {
            logger.LogError("Failed to start game server.");
        }
        else
        {
            _consecutiveFailures = 0;
        }
    }

    private async Task MonitorLoopAsync(CancellationToken stoppingToken)
    {
        TimeSpan checkInterval = TimeSpan.FromSeconds(_healthConfig.CheckIntervalSeconds);
        TimeSpan? updateInterval = _gameConfig.UpdateIntervalMinutes > 0
            ? TimeSpan.FromMinutes(_gameConfig.UpdateIntervalMinutes)
            : null;

        DateTimeOffset lastUpdateCheck = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(checkInterval, stoppingToken);

            // Run health check
            HealthReport report = await healthCheckService.CheckHealthAsync(stoppingToken);

            switch (report.Status)
            {
                case HealthStatus.Healthy:
                    _consecutiveFailures = 0;
                    logger.LogDebug("Health check passed.");
                    break;

                case HealthStatus.Degraded:
                    _consecutiveFailures++;
                    logger.LogWarning("Health check degraded ({Count}/{Max}): {Description}",
                        _consecutiveFailures, _healthConfig.MaxConsecutiveFailures,
                        report.Entries.Values.FirstOrDefault().Description);
                    break;

                case HealthStatus.Unhealthy:
                    _consecutiveFailures++;
                    logger.LogError("Health check unhealthy ({Count}/{Max}): {Description}",
                        _consecutiveFailures, _healthConfig.MaxConsecutiveFailures,
                        report.Entries.Values.FirstOrDefault().Description);
                    break;
            }

            // Check if we need to restart
            if (_consecutiveFailures >= _healthConfig.MaxConsecutiveFailures)
            {
                logger.LogWarning("Consecutive failure threshold reached ({Count}). Restarting game server with update...",
                    _consecutiveFailures);

                await gameServer.StopAsync(stoppingToken);
                gameServer.ClearErrors();
                _consecutiveFailures = 0;

                await RunUpdateAndStartCycleAsync(stoppingToken);
                lastUpdateCheck = DateTimeOffset.UtcNow;
            }

            // Periodic update check
            if (updateInterval.HasValue &&
                DateTimeOffset.UtcNow - lastUpdateCheck >= updateInterval.Value)
            {
                logger.LogInformation("Periodic update check triggered.");

                await gameServer.StopAsync(stoppingToken);
                await RunUpdateAndStartCycleAsync(stoppingToken);
                lastUpdateCheck = DateTimeOffset.UtcNow;
            }
        }
    }
}
