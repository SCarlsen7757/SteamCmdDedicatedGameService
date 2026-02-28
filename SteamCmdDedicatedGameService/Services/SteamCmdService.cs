using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SteamCmdDedicatedGameService.Models;
using System.Diagnostics;

namespace SteamCmdDedicatedGameService.Services;

public sealed class SteamCmdService(
    ILogger<SteamCmdService> logger,
    IOptions<SteamCmdConfiguration> steamCmdOptions)
{
    private readonly SteamCmdConfiguration _config = steamCmdOptions.Value;

    /// <summary>
    /// Validates that steamcmd.exe exists at the configured path.
    /// </summary>
    public bool Validate()
    {
        if (!File.Exists(_config.ExePath))
        {
            logger.LogError("SteamCMD not found at {Path}. Please install SteamCMD and set the correct path.", _config.ExePath);
            return false;
        }

        logger.LogInformation("SteamCMD found at {Path}.", _config.ExePath);
        return true;
    }

    /// <summary>
    /// Runs SteamCMD to install or update a game server.
    /// </summary>
    public async Task<bool> InstallOrUpdateGameServerAsync(int appId, string installDirectory, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting SteamCMD update for App ID {AppId} in {Directory}...", appId, installDirectory);

        Directory.CreateDirectory(installDirectory);

        string arguments = $"+login anonymous +force_install_dir \"{installDirectory}\" +app_update {appId} validate +quit";

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _config.ExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    logger.LogInformation("[SteamCMD] {Output}", e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    logger.LogWarning("[SteamCMD Error] {Output}", e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                logger.LogInformation("SteamCMD update for App ID {AppId} completed successfully.", appId);
                return true;
            }

            logger.LogError("SteamCMD exited with code {ExitCode} for App ID {AppId}.", process.ExitCode, appId);
            return false;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("SteamCMD update cancelled for App ID {AppId}.", appId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run SteamCMD for App ID {AppId}.", appId);
            return false;
        }
    }
}
