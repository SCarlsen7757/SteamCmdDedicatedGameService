namespace SteamCmdDedicatedGameService.Models;

public sealed class HealthCheckConfiguration
{
    public const string SectionName = "HealthCheck";

    /// <summary>
    /// Maximum number of consecutive failures before the service triggers a full restart cycle.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 3;

    /// <summary>
    /// Interval in seconds between health checks.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Error patterns to look for in game server console output (case-insensitive).
    /// </summary>
    public List<string> ErrorPatterns { get; set; } = ["error", "exception", "crash", "fatal"];
}
