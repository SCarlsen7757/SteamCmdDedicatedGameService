using SteamCmdDedicatedGameService.Extensions;
using SteamCmdDedicatedGameService.RaceRoom.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "RaceRoomDedicatedServer";
});

builder.Configuration.ApplyCommandLineOverrides(args);
builder.Services.AddGameServer<RaceRoomGameServerService>(builder.Configuration);

var host = builder.Build();
host.Run();
