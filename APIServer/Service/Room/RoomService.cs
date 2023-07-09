using APIServer.Controllers;
using APIServer.Controllers.ReqResModels;
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

namespace APIServer.Service.Room;

public class CustomWebSocket
{
    public WebSocket webSocket { get; set; }
    public long? userId { get; set; }
    public String nickName { get; set; }
    public byte[] buffer { get; set; } = new byte[1024];

}

public class RoomInfo
{
    public String info4Client { get; }
    public List<Int64> allUserIds { get; }
    public Int64 hostId { get; }
    public List<Int64> teamAUserIds { get; }
    public List<Int64> teamBUserIds { get; }
    public RoomInfo(String orgInfoString)
    {
        string[] splitData = orgInfoString.Split("\t", StringSplitOptions.RemoveEmptyEntries);

        allUserIds = new List<long>();
        long hostId = -1;
        teamAUserIds = new List<long>();
        teamBUserIds = new List<long>();

        foreach (string item in splitData)
        {
            if (item.StartsWith("u"))
            {
                string[] userIdStrings = item.Substring(1).Trim().Split(' ');
                foreach (string userIdString in userIdStrings)
                {
                    if (long.TryParse(userIdString, out long userId))
                    {
                        allUserIds.Add(userId);
                    }
                }
            }
            else if (item.StartsWith("h"))
            {
                if (long.TryParse(item.Substring(1).Trim(), out long userId))
                {
                    hostId = userId;
                }
            }
            else if (item.StartsWith("a"))
            {
                string[] userIdStrings = item.Substring(1).Trim().Split(' ');
                foreach (string userIdString in userIdStrings)
                {
                    if (long.TryParse(userIdString, out long userId))
                    {
                        teamAUserIds.Add(userId);
                    }
                }
            }
            else if (item.StartsWith("b"))
            {
                string[] userIdStrings = item.Substring(1).Trim().Split(' ');
                foreach (string userIdString in userIdStrings)
                {
                    if (long.TryParse(userIdString, out long userId))
                    {
                        teamBUserIds.Add(userId);
                    }
                }
            }
            else
            {
                info4Client += "\t" + item;
            }
        }
    }
}

public class RoomService
{
    ConcurrentDictionary<long, WebSocket> _socketsDic = new ConcurrentDictionary<long, WebSocket>();
    readonly Dictionary<PacketIdDef, Func<CustomWebSocket, Task<ErrorCode>>> _funcDic;
    readonly ILogger<RoomService> _logger;
    readonly ISessionService _session;
    readonly IRoomDbService _roomDb;
    readonly string _clientVersion;

    public RoomService(IConfiguration config, ISessionService session, IRoomDbService roomService, ILogger<RoomService> logger)
    {
        _session = session;
        _roomDb = roomService;
        _funcDic = new Dictionary<PacketIdDef, Func<CustomWebSocket, Task<ErrorCode>>>
        {
            { PacketIdDef.RoomEnterReq, UserEnterRoom },
            { PacketIdDef.RoomLeaveReq, UserLeaveRoom },
            { PacketIdDef.RoomReadyReq, UserReady },
            { PacketIdDef.RoomUnreadyReq, UserUnready }
        };
        _clientVersion = config.GetSection("Versions")["Client"];
        _logger = logger;
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
            if (sockHeader.packetId != (short)PacketIdDef.RoomEnterReq)
            {
                // 예외를 사용할 때 : 일반적인 흐름에 부합하지 않을때.
                throw new ArgumentException("Invalid Room Status");
            }

            if (_funcDic.TryGetValue((PacketIdDef)sockHeader.packetId, out var func) == false)
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
                if (_funcDic.TryGetValue((PacketIdDef)sockHeader.packetId, out func) == false)
                {
                    await waitSockClose(cWs, errorCode);
                    throw new ArgumentException("Invalid PacketId");
                }
                errorCode = await func(cWs);
                if (errorCode == ErrorCode.RoomLeaveSuccess || errorCode == ErrorCode.RoomDeleted)
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
        var (rtErrorCode, orgInfoStr) = await _roomDb.EnterRoom(request.roomId, cWs.userId.Value, userInfoSession.nickname);
        if (rtErrorCode != ErrorCode.None || orgInfoStr == null)
        {
            return rtErrorCode;
        }

        if (_socketsDic.TryAdd(cWs.userId.Value, cWs.webSocket) == false)
        {
            _logger.ZLogCriticalWithPayload(new { userId = cWs.userId, cWs.webSocket }, "UserEnterRoom _socketsDic.TryAdd");
            return ErrorCode.RoomDbError;
        }

        RoomInfo roomInfo = new RoomInfo(orgInfoStr);
        RoomEnterResponse response = new RoomEnterResponse() { roomInfoString = roomInfo.info4Client };
        await cWs.webSocket.SendAsync(response.Serialize(), WebSocketMessageType.Binary, true, CancellationToken.None);
        //CancellationTokenSource cts = new CancellationTokenSource();
        //cts.CancelAfter(TimeSpan.FromSeconds(60));
        RoomEnterNotify notify = new RoomEnterNotify() { enterUserNick = cWs.nickName };
        await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
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
        var (errorCode, orgInfoStr) = await _roomDb.LeaveRoom(cWs.userId.Value, cWs.nickName);
        _socketsDic.TryRemove(cWs.userId.Value, out var sock);
        RoomLeaveResponse response = new RoomLeaveResponse() { errorCode = errorCode };
        await cWs.webSocket.SendAsync(response.Serialize(), WebSocketMessageType.Binary, true, CancellationToken.None);
        if (errorCode == ErrorCode.RoomLeaveSuccess && orgInfoStr != null)
        {
            RoomInfo roomInfo = new RoomInfo(orgInfoStr);
            RoomLeaveNotify notify = new RoomLeaveNotify() { leaveUserNick = cWs.nickName };
            await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
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

        ReadyRequest request = new ReadyRequest();
        request.Deserialize(cWs.buffer);
        var (errorCode, orgInfoStr) = await _roomDb.SetUserReady(cWs.userId.Value, cWs.nickName, request.team);
        if (errorCode == ErrorCode.None && orgInfoStr != null)
        {
            RoomInfo roomInfo = new RoomInfo(orgInfoStr);
            ReadyNotify notify = new ReadyNotify() { teamString = roomInfo.info4Client };
            await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
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

        ReadyRequest request = new ReadyRequest();
        request.Deserialize(cWs.buffer);
        var (errorCode, orgInfoStr) = await _roomDb.SetUserUnready(cWs.userId.Value, cWs.nickName);
        if (errorCode == ErrorCode.None && orgInfoStr != null)
        {
            RoomInfo roomInfo = new RoomInfo(orgInfoStr);
            UnReadyNotify notify = new UnReadyNotify() { teamString = roomInfo.info4Client };
            await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
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
                    await ws.SendAsync(Msg, WebSocketMessageType.Binary, true, token);
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


    //static (string, List<Int64>) ParseString(string input)
    //{
    //    //string[] parts = input.Split('\t');

    //    //string title = parts[0];
    //    //string users = parts[1];
    //    //string aTeamReadyUsers = parts[2];
    //    //string bTeamReadyUsers = parts[3];
    //    //string allUserIds = parts[4];

    //    String titile = input.Substring(input.IndexOf("\tT "), )
    //    var userIds = allUserIds.Split(' ').Select(long.Parse).ToList();

    //    return ($"{title}\t{users}\t{aTeamReadyUsers}\t{bTeamReadyUsers}", userIds);
    //}

    async Task waitSockClose(CustomWebSocket cWs, ErrorCode errorCode)
    {
        var currentTime = DateTime.Now;
        short closeCount = 0;
        ResponseHeader response = new ResponseHeader() { errorCode = errorCode };
        await cWs.webSocket.SendAsync(response.Serialize((Int32)PacketIdDef.GenericError), WebSocketMessageType.Binary, true, CancellationToken.None);
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
