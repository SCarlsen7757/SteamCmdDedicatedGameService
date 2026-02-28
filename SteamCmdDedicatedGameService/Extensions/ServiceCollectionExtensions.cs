using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SteamCmdDedicatedGameService.HealthChecks;
using SteamCmdDedicatedGameService.Models;
using SteamCmdDedicatedGameService.Services;
using SteamCmdDedicatedGameService.Workers;

namespace SteamCmdDedicatedGameService.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core game server services, configuration, health checks, and the worker.
    /// Call <see cref="AddGameServer{T}"/> from the host project to provide the game-specific implementation.
    /// </summary>
    public static IServiceCollection AddGameServer<TGameServer>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TGameServer : BaseGameServerService
    {
        // Bind configuration sections
        services.Configure<SteamCmdConfiguration>(
            configuration.GetSection(SteamCmdConfiguration.SectionName));
        services.Configure<GameServerConfiguration>(
            configuration.GetSection(GameServerConfiguration.SectionName));
        services.Configure<HealthCheckConfiguration>(
            configuration.GetSection(HealthCheckConfiguration.SectionName));

        // Core services
        services.AddSingleton<SteamCmdService>();
        services.AddSingleton<BaseGameServerService, TGameServer>();

        // Health checks
        services.AddHealthChecks()
            .AddCheck<GameServerHealthCheck>("game_server");

        // Main worker
        services.AddHostedService<GameServerWorker>();

        return services;
    }

    /// <summary>
    /// Parses command-line arguments and overrides configuration values.
    /// Supported: --steamcmd, --appid, --serverpath, --exepath, --launchargs, --updateinterval
    /// </summary>
    public static IConfigurationManager ApplyCommandLineOverrides(
        this IConfigurationManager configuration,
        string[] args)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--steamcmd":
                    overrides[$"{SteamCmdConfiguration.SectionName}:ExePath"] = args[++i];
                    break;
                case "--appid":
                    overrides[$"{GameServerConfiguration.SectionName}:AppId"] = args[++i];
                    break;
                case "--serverpath":
                    overrides[$"{GameServerConfiguration.SectionName}:InstallDirectory"] = args[++i];
                    break;
                case "--exepath":
                    overrides[$"{GameServerConfiguration.SectionName}:ExecutablePath"] = args[++i];
                    break;
                case "--launchargs":
                    overrides[$"{GameServerConfiguration.SectionName}:LaunchArguments"] = args[++i];
                    break;
                case "--updateinterval":
                    overrides[$"{GameServerConfiguration.SectionName}:UpdateIntervalMinutes"] = args[++i];
                    break;
            }
        }

        if (overrides.Count > 0)
        {
            configuration.AddInMemoryCollection(overrides);
        }

        return configuration;
    }
}
