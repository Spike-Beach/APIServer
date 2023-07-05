using APIServer.Controllers;
using APIServer.GanaricModels;
using APIServer.Service.Room.Model;
using APIServer.Service.Session;
using APIServer.Service.Session.Model;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using ZLogger;
using static Humanizer.In;

namespace APIServer.Service.Room;

public class CustomWebSocket
{
    public WebSocket webSocket { get; set; }
    public long? userId { get; set; }
    public String nickName { get; set; }
    public byte[] buffer { get; set; } = new byte[1024];

}

public class RoomService
{
    ConcurrentDictionary<long, WebSocket> _socketsDic = new ConcurrentDictionary<long, WebSocket>();
    readonly Dictionary<PacketId, Func<CustomWebSocket, Task<ErrorCode>>> _funcDic;
    readonly ILogger<RoomService> _logger;
    readonly ISessionService _session;
    readonly IRoomDbService _roomDb;
    readonly string _clientVersion;

    public RoomService(IConfiguration config, ISessionService session, IRoomDbService roomService, ILogger<RoomService> logger)
    {
        _session = session;
        _roomDb = roomService;
        _funcDic = new Dictionary<PacketId, Func<CustomWebSocket, Task<ErrorCode>>>
        {
            { PacketId.RoomEnter, UserEnterRoom },
            { PacketId.RoomLeave, UserLeaveRoom },
            { PacketId.RoomReady, UserReady },
            { PacketId.RoomReady, UserUnready }
        };
        _clientVersion = config.GetSection("Versions")["Client"];
        _logger = logger;
    }

    bool CheckUserStatus(Int64 UserId, PacketId packetId)
    {
        // 방 나가기 및 레디 기능은 유저가 있어야함
        if (packetId == PacketId.RoomReady && _socketsDic.ContainsKey(UserId) == true)
        {
            return true;
        }
        // 방입장 -> 유저가 방에 이미 들어와 있으면 안됨
        else if (packetId == PacketId.RoomEnter && _socketsDic.ContainsKey(UserId) != true)
        {
            return true;
        }
        else if (packetId == PacketId.RoomLeave && _socketsDic.ContainsKey(UserId) == true)
        {
            return true;
        }
        _logger.ZLogErrorWithPayload(new { UserId, packetId }, "Invalid User Status");
        return false;
    }

    public async Task<ErrorCode> ProcessRoomRequests(WebSocket webSocket)
    {
        // 헤더 파싱 후 유효성 확인
        // 헤더에 따라서 처리할 함수를 호출 -> 무조건 RoomEnterrm 그 이외엔 연결 끊기.
        RequestHeader sockHeader = new RequestHeader();
        //CancellationTokenSource cts = new CancellationTokenSource();
        //cts.CancelAfter(5000);

        CustomWebSocket cWs = new CustomWebSocket() { webSocket = webSocket };
        try
        {
            var receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(cWs.buffer), CancellationToken.None);

            // 맨처음은 방 입장이여야만 한다.
            sockHeader.Deserialize(cWs.buffer);
            if (sockHeader.PacketId != (short)PacketId.RoomEnter)
            {
                // 예외를 사용할 때 : 일반적인 흐름에 부합하지 않을때.
                throw new ArgumentException("Invalid Room Status");
            }

            if (_funcDic.TryGetValue((PacketId)sockHeader.PacketId, out var func) == false)
            {
                throw new ArgumentException("Invalid PacketId");
            }

            var errorCode = await func(cWs);
            if (errorCode != ErrorCode.None)
            {
                await waitSockClose(cWs, errorCode);
                return errorCode;
            }

            while (!webSocket.CloseStatus.HasValue)
            {
                receiveResult = await webSocket.ReceiveAsync( new ArraySegment<byte>(cWs.buffer), CancellationToken.None);
                sockHeader.Deserialize(cWs.buffer);
                if (_funcDic.TryGetValue((PacketId)sockHeader.PacketId, out func) == false)
                {
                    await waitSockClose(cWs, errorCode);
                    throw new ArgumentException("Invalid PacketId");
                }
                errorCode = await func(cWs);
                if (errorCode == ErrorCode.RoomLeaveSuccess)
                {
                    await waitSockClose(cWs, errorCode);
                    break ;
                }
                else if (errorCode != ErrorCode.None) 
                {
                    await waitSockClose(cWs, errorCode);
                    return errorCode;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogWarningWithPayload(new { userId = cWs.userId, ex.Message, ex.StackTrace }, "ProcessRoomRequests Exception");
            await waitSockClose(cWs, ErrorCode.InvalidPacketForm);
        }
        return ErrorCode.None;
    }

    public async Task<ErrorCode> UserEnterRoom(CustomWebSocket cWs)
    {
        WebSocket sock;
        var request = new RoomEnterRequest();
        request.Deserialize(cWs.buffer);

        var (errorCode, userInfoSession) = await GetSession(request.userAssignedId);
        if (errorCode != ErrorCode.None || userInfoSession == null)
        {
            return errorCode;
        }
        if (request.token.Equals(userInfoSession.token) != true)
        {
            return ErrorCode.InvalidToken;
        }
        else if (request.clientVersion.Equals(_clientVersion) != true)
        {
            return ErrorCode.WorngClientVersion;
        }

        cWs.userId = userInfoSession.userId;
        cWs.nickName = userInfoSession.nickname;
        var (rtErrorCode, roomIntoString) = await _roomDb.EnterRoom(request.roomId, cWs.userId.Value, userInfoSession.nickname);
        if (rtErrorCode != ErrorCode.None || roomIntoString == null)
        {
            return rtErrorCode;
        }

        if (_socketsDic.TryAdd(cWs.userId.Value, cWs.webSocket) == false)
        {
            // 로깅
            return ErrorCode.RoomDbError;
        }

        var (roomInfoString, userIds) = ParseString(roomIntoString);
        await cWs.webSocket.SendAsync(Encoding.UTF8.GetBytes(roomInfoString), WebSocketMessageType.Text, true, CancellationToken.None);
        //CancellationTokenSource cts = new CancellationTokenSource();
        //cts.CancelAfter(TimeSpan.FromSeconds(60));
        string msg = "Enter:" + cWs.nickName;
        await SendInRoomAsync(userIds, Encoding.UTF8.GetBytes(msg), CancellationToken.None);
        return ErrorCode.None;
    }

    public async Task<ErrorCode> UserLeaveRoom(CustomWebSocket cWs)
    {
        // cWs유효성 검증
        if (cWs.userId == null)
        {
            _logger.ZLogCritical("userLeaveRoom no userId");
            return ErrorCode.RoomDbError;
        }
        else if (_socketsDic.TryGetValue(cWs.userId.Value, out var ws) == false || ws.Equals(cWs.webSocket) == false)
        {
            _logger.ZLogCriticalWithPayload(new { userId = cWs.userId }, "userLeaveRoom userId not saved in dic");
            return ErrorCode.RoomDbError;
        }

        RoomLeaveRequest request = new RoomLeaveRequest();
        request.Deserialize(cWs.buffer);
        var (errorCode, userIds) = await _roomDb.LeaveRoom(cWs.userId.Value, cWs.nickName);
        if (errorCode == ErrorCode.None && userIds != null)
        {
            string msg = "Leave:" + cWs.nickName;
            await SendInRoomAsync(userIds, Encoding.UTF8.GetBytes(msg), CancellationToken.None);
        }
        return errorCode;
    }

    public async Task<ErrorCode> UserReady(CustomWebSocket cWs)
    {
        if (cWs.userId == null)
        {
            _logger.ZLogCritical("userLeaveRoom no userId");
            return ErrorCode.RoomDbError;
        }
        else if (_socketsDic.TryGetValue(cWs.userId.Value, out var ws) == false || ws.Equals(cWs.webSocket) == false)
        {
            _logger.ZLogCriticalWithPayload(new { userId = cWs.userId }, "userLeaveRoom userId not saved in dic");
            return ErrorCode.RoomDbError;
        }

        UserReadyRequest request = new UserReadyRequest();
        request.Deserialize(cWs.buffer);
        var (errorCode, userIds) = await _roomDb.SetUserReady(cWs.userId.Value, cWs.nickName, request.team);
        if (errorCode == ErrorCode.None && userIds != null)
        {
            string msg = "Ready:" + cWs.nickName;
            await SendInRoomAsync(userIds, Encoding.UTF8.GetBytes(msg), CancellationToken.None);
        }
        return errorCode;
    }

    public async Task<ErrorCode> UserUnready(CustomWebSocket cWs)
    {
        if (cWs.userId == null)
        {
            _logger.ZLogCritical("userLeaveRoom no userId");
            return ErrorCode.RoomDbError;
        }
        else if (_socketsDic.TryGetValue(cWs.userId.Value, out var ws) == false || ws.Equals(cWs.webSocket) == false)
        {
            _logger.ZLogCriticalWithPayload(new { userId = cWs.userId }, "userLeaveRoom userId not saved in dic");
            return ErrorCode.RoomDbError;
        }

        UserReadyRequest request = new UserReadyRequest();
        request.Deserialize(cWs.buffer);
        var (errorCode, userIds) = await _roomDb.SetUserUnready(cWs.userId.Value, cWs.nickName, request.team);
        if (errorCode == ErrorCode.None && userIds != null)
        {
            string msg = "Unready:" + cWs.nickName;
            await SendInRoomAsync(userIds, Encoding.UTF8.GetBytes(msg), CancellationToken.None);
        }
        return errorCode;
    }

    async Task SendInRoomAsync(List<Int64> userIdArr, byte[] Msg, CancellationToken token)
    {
        WebSocket ws;
        try
        {
            foreach (var userId in userIdArr)
            {
                if (_socketsDic.TryGetValue(userId, out ws))
                {
                    await ws.SendAsync(Msg, WebSocketMessageType.Text, true, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Send operation canceled.");
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"WebSocket error occurred: {ex.Message}");
        }
    }


    static (string, List<Int64>) ParseString(string input)
    {
        string[] parts = input.Split('\t');

        string title = parts[0];
        string users = parts[1];
        string aTeamReadyUsers = parts[2];
        string bTeamReadyUsers = parts[3];
        string allUserIds = parts[4];

        var userIds = allUserIds.Split(' ').Select(long.Parse).ToList();

        return ($"{title}\t{users}\t{aTeamReadyUsers}\t{bTeamReadyUsers}", userIds);
    }

    async Task waitSockClose(CustomWebSocket cWs, ErrorCode errorCode)
    {
        var currentTime = DateTime.Now;
        short closeCount = 0;
        ResponseHeader response = new ResponseHeader();
        await cWs.webSocket.SendAsync(response.SetAndSerialize(errorCode), WebSocketMessageType.Binary, true, CancellationToken.None);
        while (!cWs.webSocket.CloseStatus.HasValue)
        {
            if (closeCount == 3)
            {
                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await cWs.webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, null, cts.Token);
            }
            ++closeCount;
            await Task.Delay(1000);
        }
        if (cWs.userId != null)
        {
            _socketsDic.TryRemove(cWs.userId.Value, out var sock);
        }
    }

    async Task<(ErrorCode, SessionModel?)> GetSession(string userAssignedId)
    {
        var (errorCode, userInfo) = await _session.GetSession(userAssignedId);
        if (errorCode != ErrorCode.None || userInfo == null)
        {
            return (errorCode, null);
        }
        return (errorCode, userInfo);
    }
}
