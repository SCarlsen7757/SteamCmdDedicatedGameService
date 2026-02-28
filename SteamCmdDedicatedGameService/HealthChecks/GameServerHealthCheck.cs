using Microsoft.Extensions.Diagnostics.HealthChecks;

using SteamCmdDedicatedGameService.Services;

namespace SteamCmdDedicatedGameService.HealthChecks;

/// <summary>
/// Health check that reports the status of the game server process
/// and whether recent errors have been detected in its console output.
/// </summary>
public sealed class GameServerHealthCheck(BaseGameServerService gameServer) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!gameServer.IsRunning)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Game server process is not running."));
        }

        var errors = gameServer.RecentErrors;
        if (errors.Count > 0)
        {
            string errorSummary = string.Join(Environment.NewLine, errors.TakeLast(5));
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Game server is running but has {errors.Count} recent error(s). Latest: {errorSummary}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Game server is running with no recent errors."));
    }
}
