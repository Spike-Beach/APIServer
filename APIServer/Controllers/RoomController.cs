using APIServer.Controllers.ReqResModels;
using APIServer.GanaricModels;
using APIServer.Service;
using APIServer.Service.Room;
using APIServer.Service.Session;
using APIServer.Service.Session.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace APIServer.Controllers;

public enum PacketId : Int32
{
    RoomEnter = 10,
    RoomLeave = 11,
    RoomReady = 12
}

[Route("[controller]")]
[ApiController]
public class RoomController : ControllerBase
{
    readonly ILogger<UserInfoController> _logger;
    readonly ISessionService _session;
    readonly IRoomDbService _roomDbService;
    readonly RoomService _roomService;
    readonly String _clientVersion;
    byte[] _buffer = new byte[1024];

    public RoomController(IConfiguration config, ILogger<UserInfoController> logger, IRoomDbService roomDbService, RoomService roomService, ISessionService session)
    {
        _logger = logger;
        _session = session;
        _clientVersion = config.GetSection("Versions")["Client"];
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
    public async Task Enter()//(RoomEnterRequest request)
    {
        RoomService roomService;
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();





            RoomEnterRequest request = new RoomEnterRequest();
            var receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(_buffer), CancellationToken.None);
            request.Deserialize(_buffer);
            await _roomService.ProcessRoomRequests(webSocket, _buffer);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }


    [HttpPost("Leave")]
    public async Task<RoomLeaveResponse> Leave(RoomLeaveRequest request)
    {
        var response = new RoomLeaveResponse();
        response.errorCode = await _roomDbService.LeaveRoom(0, 10, "User H");
        return response;
    }

    async Task<(ErrorCode, SessionModel?)> GetSession(String userAssignedId)
    {
        var (errorCode, userInfo) = await _session.GetSession(userAssignedId);
        if (errorCode != ErrorCode.None || userInfo == null)
        {
            return (errorCode, null);
        }
        return (errorCode, userInfo);
    }

    async Task<ErrorCode> UserRoomEnter((RoomReqUserInfo, Int16) input)
    {
        return ErrorCode.None;
    }

    async Task<ErrorCode> UserRoomLeave((RoomReqUserInfo, Int16) input)
    {
        return ErrorCode.None;
    }

    async Task<ErrorCode> UserReady((RoomReqUserInfo, Int16) input)
    {
        return ErrorCode.None;
    }


}
