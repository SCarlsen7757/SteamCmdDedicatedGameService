namespace SteamCmdDedicatedGameService.Models;

public sealed class SteamCmdConfiguration
{
    public const string SectionName = "SteamCmd";

    /// <summary>
    /// Full path to the steamcmd.exe executable.
    /// </summary>
    public required string ExePath { get; set; }
}
