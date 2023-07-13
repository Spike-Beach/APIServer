using APIServer.Controllers.ReqResModels;
using APIServer.GanaricModels;
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

public enum PacketIdDef : Int32
{
    GenericError = 0,
    RoomEnterReq = 10,
    RoomEnterRes = 11,
    RoomEnterNtf = 12,
    RoomLeaveReq = 13,
    RoomLeaveRes = 14,
    RoomLeaveNtf = 15,
    RoomReadyReq = 16,
    RoomReadyRes = 17,
    RoomReadyNtf = 18,
    RoomUnreadyReq = 19,
    RoomUnreadyRes = 20,
    RoomUnreadyNtf = 21,
    GameStartReq = 22,
    GameStartRes = 23,
    GameStartNtf = 24,
}

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
    public async Task Enter()//(RoomEnterRequest request)
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


    //[HttpPost("Leave")]
    //public async Task<RoomLeaveResponse> Leave(RoomLeaveRequest request)
    //{
    //    var response = new RoomLeaveResponse();
    //    response.errorCode = await _roomDbService.LeaveRoom(0, 10, "User H");
    //    return response;
    //}

    //async Task<ErrorCode> UserRoomEnter((RoomReqUserInfo, Int16) input)
    //{
    //    return ErrorCode.None;
    //}

    //async Task<ErrorCode> UserRoomLeave((RoomReqUserInfo, Int16) input)
    //{
    //    return ErrorCode.None;
    //}

    //async Task<ErrorCode> UserReady((RoomReqUserInfo, Int16) input)
    //{
    //    return ErrorCode.None;
    //}


}
