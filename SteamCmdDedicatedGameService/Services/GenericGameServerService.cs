using Microsoft.Extensions.Logging;

namespace SteamCmdDedicatedGameService.Services;

/// <summary>
/// A generic game server service that uses configuration values as-is.
/// Suitable for any game server that does not need custom logic.
/// </summary>
public class GenericGameServerService(ILogger<GenericGameServerService> logger)
    : BaseGameServerService(logger);
