using Microsoft.Extensions.Logging;
using SteamCmdDedicatedGameService.Models;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

namespace SteamCmdDedicatedGameService.Services;

/// <summary>
/// Abstract base class for managing a dedicated game server lifecycle.
/// Inherit from this class to create game-specific implementations with custom
/// launch arguments, error patterns, or startup/shutdown behaviour.
/// </summary>
public abstract class BaseGameServerService : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<string> _recentErrors = new();
    private const int MaxStoredErrors = 50;
    private Process? _serverProcess;
    private HealthCheckConfiguration? _healthConfig;
    private bool _disposed;

    protected BaseGameServerService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the game server process is currently running.
    /// </summary>
    public bool IsRunning => HasProcessExited() == false;

    /// <summary>
    /// Gets recent error lines captured from the game server console output.
    /// </summary>
    public IReadOnlyCollection<string> RecentErrors => [.. _recentErrors];

    /// <summary>
    /// Gets the full path to the game server executable.
    /// Override in derived classes if the executable path needs special resolution.
    /// </summary>
    protected virtual string GetExecutablePath(GameServerConfiguration config)
    {
        string exePath = config.ExecutablePath;

        if (!Path.IsPathRooted(exePath))
            exePath = Path.Combine(config.InstallDirectory, exePath);

        return exePath;
    }

    /// <summary>
    /// Gets the launch arguments for the game server.
    /// Override in derived classes to provide game-specific arguments.
    /// </summary>
    protected virtual string GetLaunchArguments(GameServerConfiguration config)
    {
        return config.LaunchArguments;
    }

    /// <summary>
    /// Determines whether a line of console output is an error.
    /// Override in derived classes for game-specific error detection.
    /// </summary>
    protected virtual bool IsErrorLine(string line, HealthCheckConfiguration healthConfig)
    {
        return healthConfig.ErrorPatterns
            .Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Called when the game server process has started.
    /// Override to perform game-specific post-start actions.
    /// </summary>
    protected virtual Task OnServerStartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called before the game server process is stopped.
    /// Override to perform game-specific graceful shutdown (e.g. sending commands).
    /// </summary>
    protected virtual Task OnServerStoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the game server process and begins monitoring console output.
    /// </summary>
    public async Task<bool> StartAsync(GameServerConfiguration gameConfig, HealthCheckConfiguration healthConfig, CancellationToken cancellationToken)
    {
        string exePath = GetExecutablePath(gameConfig);
        string arguments = GetLaunchArguments(gameConfig);

        if (!File.Exists(exePath))
        {
            _logger.LogError("Game server executable not found at {Path}.", exePath);
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Starting game server: {ExePath} {Arguments}", exePath, arguments);

        ClearErrors();

        try
        {
            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? gameConfig.InstallDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _healthConfig = healthConfig;
            _serverProcess.OutputDataReceived += OnOutputDataReceived;
            _serverProcess.ErrorDataReceived += OnErrorDataReceived;

            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            _logger.LogInformation("Game server started with PID {Pid}.", _serverProcess.Id);

            await OnServerStartedAsync(cancellationToken);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 740)
        {
            CleanupProcess();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start game server.");
            CleanupProcess();
            return false;
        }
    }

    /// <summary>
    /// Stops the game server process gracefully, or kills it after a timeout.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_serverProcess is null || HasProcessExited() != false)
        {
            _logger.LogInformation("Game server is not running.");
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Stopping game server (PID {Pid})...", _serverProcess.Id);

        try
        {
            await OnServerStoppingAsync(cancellationToken);

            // Give the process a chance to exit gracefully
            _serverProcess.CloseMainWindow();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            try
            {
                await _serverProcess.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Game server did not exit gracefully within timeout. Killing process...");
                _serverProcess.Kill(entireProcessTree: true);
            }

            _logger.LogInformation("Game server stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping game server.");
            try { _serverProcess.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Clears the stored error list.
    /// </summary>
    public void ClearErrors()
    {
        while (_recentErrors.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Safely checks whether the process has exited.
    /// Returns <c>null</c> if no process is associated, otherwise <c>true</c> or <c>false</c>.
    /// </summary>
    private bool? HasProcessExited()
    {
        if (_serverProcess is null)
            return null;

        try
        {
            return _serverProcess.HasExited;
        }
        catch (InvalidOperationException)
        {
            // No OS process associated (e.g. Start() was never called or failed)
            return null;
        }
    }

    /// <summary>
    /// Detaches event handlers and disposes/nulls the process field.
    /// </summary>
    private void CleanupProcess()
    {
        if (_serverProcess is null)
            return;

        _serverProcess.OutputDataReceived -= OnOutputDataReceived;
        _serverProcess.ErrorDataReceived -= OnErrorDataReceived;
        _serverProcess.Dispose();
        _serverProcess = null;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e) => HandleOutputLine(e.Data, _healthConfig!);

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e) => HandleErrorLine(e.Data, _healthConfig!);

    private void HandleOutputLine(string? data, HealthCheckConfiguration healthConfig)
    {
        if (string.IsNullOrWhiteSpace(data))
            return;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[GameServer] {Output}", data);

        if (IsErrorLine(data, healthConfig))
        {
            RecordError(data);
        }
    }

    private void HandleErrorLine(string? data, HealthCheckConfiguration healthConfig)
    {
        if (string.IsNullOrWhiteSpace(data))
            return;

        _logger.LogWarning("[GameServer StdErr] {Output}", data);
        RecordError(data);
    }

    private void RecordError(string line)
    {
        _recentErrors.Enqueue(line);

        // Keep the queue bounded
        while (_recentErrors.Count > MaxStoredErrors)
            _recentErrors.TryDequeue(out _);
    }

    public virtual void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_serverProcess is not null)
        {
            if (HasProcessExited() == false)
            {
                try { _serverProcess.Kill(entireProcessTree: true); } catch { /* best effort */ }
            }

            CleanupProcess();
        }
        GC.SuppressFinalize(this);
    }
}
