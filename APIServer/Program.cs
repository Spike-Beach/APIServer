using APIServer.Service.GameDataDb;
using APIServer.Service.Room;
using APIServer.Service.Session;
using DungeonFarming.Middleware;
using ZLogger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddZLoggerFile("logs/mainLog.log", options => { options.EnableStructuredLogging = true; });
    logging.AddZLoggerRollingFile((dt, x) => $"logs/{dt.ToLocalTime():yyyy-MM-dd}_{x:000}.log", x => x.ToLocalTime().Date, 1024);
    logging.AddZLoggerConsole(options => { options.EnableStructuredLogging = true; });
});

builder.Services.AddControllers();
builder.Services.AddTransient<IGameDbServcie, MysqlGameDbService>();
builder.Services.AddSingleton<ISessionService, RedisSessionService>();
builder.Services.AddSingleton<IRoomDbService, RedisRoomDbService>();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddHttpContextAccessor();


var app = builder.Build();
var roomService = app.Services.GetRequiredService<IRoomDbService>();
roomService.SetScripts();
app.UseAuthorization();
app.MapControllers();
app.UseMiddleware<AuthCheckMiddleware>();
app.UseMiddleware<VersionCheckMiddleware>();
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);
app.Run();


