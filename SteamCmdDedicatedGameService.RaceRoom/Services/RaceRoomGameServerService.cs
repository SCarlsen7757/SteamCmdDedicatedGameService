using SteamCmdDedicatedGameService.Models;
using SteamCmdDedicatedGameService.Services;

namespace SteamCmdDedicatedGameService.RaceRoom.Services;

/// <summary>
/// RaceRoom Dedicated Server specific implementation.
/// App ID: 354060
/// Provides RaceRoom-specific launch arguments, error detection, and lifecycle hooks.
/// </summary>
public class RaceRoomGameServerService(ILogger<RaceRoomGameServerService> logger)
    : BaseGameServerService(logger)
{
    /// <summary>
    /// Builds the launch arguments for the RaceRoom dedicated server.
    /// Combines default RaceRoom flags with any user-supplied arguments from configuration.
    /// </summary>
    protected override string GetLaunchArguments(GameServerConfiguration config)
    {
        // RaceRoom dedicated server typically uses a config file to set up the server.
        // Allow users to override via configuration, but provide sensible defaults.
        if (!string.IsNullOrWhiteSpace(config.LaunchArguments))
            return config.LaunchArguments;

        return string.Empty;
    }

    /// <summary>
    /// RaceRoom-specific error detection.
    /// Adds patterns commonly seen in RaceRoom server output alongside the base patterns.
    /// </summary>
    protected override bool IsErrorLine(string line, HealthCheckConfiguration healthConfig)
    {
        // RaceRoom-specific error indicators
        string[] raceRoomPatterns =
        [
            "error",
        ];

        if (raceRoomPatterns.Any(p => line.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return true;

        return base.IsErrorLine(line, healthConfig);
    }

    protected override Task OnServerStartedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("RaceRoom Dedicated Server has started. Waiting for server initialization...");
        return Task.CompletedTask;
    }

    protected override Task OnServerStoppingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating graceful shutdown of RaceRoom Dedicated Server...");
        return Task.CompletedTask;
    }
}
