namespace SteamCmdDedicatedGameService.Models;

public sealed class GameServerConfiguration
{
    public const string SectionName = "GameServer";

    /// <summary>
    /// Steam App ID for the dedicated server (e.g. 354060 for RaceRoom).
    /// </summary>
    public required int AppId { get; set; }

    /// <summary>
    /// Directory where the game server will be installed.
    /// </summary>
    public required string InstallDirectory { get; set; }

    /// <summary>
    /// Relative or absolute path to the server executable.
    /// </summary>
    public required string ExecutablePath { get; set; }

    /// <summary>
    /// Launch arguments passed to the game server executable.
    /// </summary>
    public string LaunchArguments { get; set; } = string.Empty;

    /// <summary>
    /// How often (in minutes) the service should check for updates. 0 = only on startup.
    /// </summary>
    public int UpdateIntervalMinutes { get; set; }
}
