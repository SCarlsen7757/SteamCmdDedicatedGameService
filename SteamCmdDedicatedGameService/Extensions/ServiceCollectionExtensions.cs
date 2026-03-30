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
    /// Applies command-line argument overrides to configuration using switch mappings.
    /// Supported: --steamcmd, --appid, --serverpath, --exepath, --launchargs, --updateinterval
    /// </summary>
    public static IConfigurationManager ApplyCommandLineOverrides(
        this IConfigurationManager configuration,
        string[] args)
    {
        var switchMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--steamcmd"] = $"{SteamCmdConfiguration.SectionName}:ExePath",
            ["--appid"] = $"{GameServerConfiguration.SectionName}:AppId",
            ["--serverpath"] = $"{GameServerConfiguration.SectionName}:InstallDirectory",
            ["--exepath"] = $"{GameServerConfiguration.SectionName}:ExecutablePath",
            ["--launchargs"] = $"{GameServerConfiguration.SectionName}:LaunchArguments",
            ["--updateinterval"] = $"{GameServerConfiguration.SectionName}:UpdateIntervalMinutes",
        };

        configuration.AddCommandLine(args, switchMappings);

        return configuration;
    }
}
