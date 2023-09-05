using APIServer.Controllers.ReqResModels;
using APIServer.Service.Room;
using APIServer.Service.Session;
using Microsoft.AspNetCore.Mvc;


namespace APIServer.Controllers;



[Route("[controller]")]
[ApiController]
public class RoomController : ControllerBase
{
    readonly ILogger<UserInfoController> _logger;
    readonly ISessionService _session;
    readonly IRoomDbService _roomDbService;
    readonly RoomService _roomService;


    public RoomController(IConfiguration config, ILogger<UserInfoController> logger, IRoomDbService roomDbService, RoomService roomService, ISessionService session)
    {
        _logger = logger;
        _session = session;

        _roomDbService = roomDbService;
        _roomService = roomService;
    }

    [HttpPost("Listup")]
    public async Task<RoomListupResponse> Listup(RoomListupRequset request)
    {
        RoomListupResponse response = new RoomListupResponse();
        (response.errorCode, var roomList) = await _roomDbService.GetRoomList();
        if (response.errorCode != ErrorCode.None || roomList == null)
        {
            return response;
        }

        response.roomList = roomList;
        return response;
    }

    [Route("ws/Enter")]
    public async Task Enter()
    {
        RoomService roomService;
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await _roomService.ProcessRoomRequests(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
