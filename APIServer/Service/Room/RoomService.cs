using APIServer.Service.Room.Model;
using APIServer.Service.Session;
using APIServer.Service.Session.Model;
using APIServer.Controllers.ReqResModels;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using ZLogger;

namespace APIServer.Service.Room;

public class RoomService
{
    ConcurrentDictionary<long, WebSocket> _socketsDic = new ConcurrentDictionary<long, WebSocket>();
    readonly Dictionary<PacketIdDef, Func<CustomWebSocket, Task<ErrorCode>>> _funcDic;
    readonly ILogger<RoomService> _logger;
    readonly ISessionService _session;
    readonly IRoomDbService _roomDb;
    readonly String _clientVersion;
    readonly String _gameServerInfoString;

    public RoomService(IConfiguration config, ISessionService session, IRoomDbService roomService, ILogger<RoomService> logger)
    {
        _session = session;
        _roomDb = roomService;
        _funcDic = new Dictionary<PacketIdDef, Func<CustomWebSocket, Task<ErrorCode>>>
        {
            { PacketIdDef.RoomEnterReq, UserEnterRoom },
            { PacketIdDef.RoomLeaveReq, UserLeaveRoom },
            { PacketIdDef.RoomReadyReq, UserReady },
            { PacketIdDef.RoomUnreadyReq, UserUnready },
            { PacketIdDef.GameStartReq, GameStart }
        };
        _clientVersion = config.GetSection("Versions")["Client"];
        _gameServerInfoString = config.GetSection("GameServer")["InfoString"];
        _logger = logger;
    }

    // 요청 패킷을 받아서 확인하고 분기시키는 함수.
    public async Task<ErrorCode> ProcessRoomRequests(WebSocket webSocket)
    {
        RequestHeader sockHeader = new RequestHeader();

        CustomWebSocket cWs = new CustomWebSocket() { webSocket = webSocket };
        try
        {
            while (webSocket.CloseStatus.HasValue == false)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(cWs.buffer), CancellationToken.None);
                sockHeader.Deserialize(cWs.buffer);
                if (_funcDic.TryGetValue((PacketIdDef)sockHeader.packetId, out var func) == false)
                {
                    cWs.errorCode = ErrorCode.InvalidPacketForm;
                    await waitSockClose(cWs);
                    throw new ArgumentException("Invalid PacketId");
                }
                
                // 패킷에 맞는 함수로 분기
                var errorCode = await func(cWs);
                if (errorCode == ErrorCode.RoomLeaveSuccess || errorCode == ErrorCode.RoomDeleted)
                {
                    cWs.errorCode = ErrorCode.InvalidPacketType;
                    await waitSockClose(cWs);
                    break;
                }
                else if (errorCode != ErrorCode.None)
                {
                    _logger.ZLogInformationWithPayload(new { userId = cWs.userId, errorCode = errorCode }, "ProcessRoomRequests ErrorCode");
                    cWs.errorCode = errorCode;
                    await waitSockClose(cWs);
                    return errorCode;
                }
            }
            _logger.ZLogInformationWithPayload(new { }, "Test");
        }
        catch (Exception ex)
        {
            cWs.errorCode = ErrorCode.InvalidPacketForm;
            await waitSockClose(cWs);
        }
        return ErrorCode.None;
    }

    // 방 입장 패킷 처리하는 함수
    public async Task<ErrorCode> UserEnterRoom(CustomWebSocket cWs)
    {
        WebSocket sock;
        var request = new RoomEnterRequest();
        request.Deserialize(cWs.buffer);

        // 세션 체크
        var (errorCode, userInfoSession) = await GetSession(request.userAssignedId);
        if (errorCode != ErrorCode.None || userInfoSession == null)
        {
            _logger.ZLogInformationWithPayload(new { userId = cWs.userId, errorCode = errorCode, userInfoSession = userInfoSession },
                "UserEnterRoom errorCode != ErrorCode.None || userInfoSession == null");
            return errorCode;
        }
        if (request.token.Equals(userInfoSession.token) != true)
        {
            _logger.ZLogInformationWithPayload(new { userId = cWs.userId, errorCode = errorCode, reqTok = request.token, ourTok = userInfoSession.token },
                "UserEnterRoom invalid Token");
            return ErrorCode.InvalidToken;
        }
        else if (request.clientVersion.Equals(_clientVersion) != true)
        {
            _logger.ZLogInformationWithPayload(new { userId = cWs.userId, errorCode = errorCode, version = request.clientVersion },
                "UserEnterRoom invalid Token");
            return ErrorCode.WorngClientVersion;
        }

        // 방 입장
        cWs.userId = userInfoSession.userId;
        cWs.nickName = userInfoSession.nickname;
        var (rtErrorCode, orgInfoStr) = await _roomDb.EnterRoom(request.roomId, cWs.userId.Value, userInfoSession.nickname);
        if (rtErrorCode != ErrorCode.None || orgInfoStr == null)
        {
            _logger.ZLogInformationWithPayload(new { userId = cWs.userId, errorCode = errorCode, version = request.clientVersion },
                "_roomDb.EnterRoom rtErrorCode != ErrorCode.None || orgInfoStr == null");
            return rtErrorCode;
        }
        if (_socketsDic.TryAdd(cWs.userId.Value, cWs.webSocket) == false)
        {
            _logger.ZLogCriticalWithPayload(new { userId = cWs.userId, cWs.webSocket }, "UserEnterRoom _socketsDic.TryAdd");
            return ErrorCode.RoomDbError;
        }

        // 방 입장 응답
        RoomInfo roomInfo = new RoomInfo(orgInfoStr);
        RoomEnterResponse response = new RoomEnterResponse() { roomInfoString = roomInfo.info4Client };
        await cWs.webSocket.SendAsync(response.Serialize(), WebSocketMessageType.Binary, true, CancellationToken.None);
        RoomEnterNotify notify = new RoomEnterNotify() { enterUserNick = cWs.nickName };
        await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
        return ErrorCode.None;
    }

    // 방 퇴장 패킷 처리하는 함수
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
        // 서비스에 요청
        var (errorCode, orgInfoStr) = await _roomDb.LeaveRoom(cWs.userId.Value, cWs.nickName);
        _socketsDic.TryRemove(cWs.userId.Value, out var sock);
        RoomLeaveResponse response = new RoomLeaveResponse() { errorCode = errorCode };
        if (cWs.webSocket.State != WebSocketState.Connecting)
        {
            await cWs.webSocket.SendAsync(response.Serialize(), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        if (errorCode == ErrorCode.RoomLeaveSuccess && orgInfoStr != null)
        {
            RoomInfo roomInfo = new RoomInfo(orgInfoStr);
            RoomLeaveNotify notify = new RoomLeaveNotify() { leaveInfoString = roomInfo.info4Client };
            await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
        }
        return errorCode;
    }

    // 방 팀 선택 + 레디 패킷 처리하는 함수
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
        // 서비스에 요청
        var (errorCode, orgInfoStr) = await _roomDb.SetUserReady(cWs.userId.Value, cWs.nickName, request.team);
        if (errorCode == ErrorCode.None && orgInfoStr != null)
        {
            RoomInfo roomInfo = new RoomInfo(orgInfoStr);
            ReadyNotify notify = new ReadyNotify() { teamString = roomInfo.info4Client };
            await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
        }
        return errorCode;
    }

    // 방 팀 레디 해제 패킷 처리하는 함수
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
        // 서비스에 요청.
        var (errorCode, orgInfoStr) = await _roomDb.SetUserUnready(cWs.userId.Value, cWs.nickName);
        if (errorCode == ErrorCode.None && orgInfoStr != null)
        {
            RoomInfo roomInfo = new RoomInfo(orgInfoStr);
            UnReadyNotify notify = new UnReadyNotify() { teamString = roomInfo.info4Client };
            await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
        }
        return errorCode;
    }

    // 게임 시작 패킷 처리 함수.
    public async Task<ErrorCode> GameStart(CustomWebSocket cWs)
    {
        if (cWs.userId == null)
        {
            _logger.ZLogCritical("GameStart no userId");
            return ErrorCode.RoomDbError;
        }
        else if (_socketsDic.TryGetValue(cWs.userId.Value, out var ws) == false || ws.Equals(cWs.webSocket) == false)
        {
            _logger.ZLogCriticalWithPayload(new { userId = cWs.userId }, "GameStart userId not saved in dic");
            return ErrorCode.RoomDbError;
        }

        GameStartRequest request = new GameStartRequest();
        request.Deserialize(cWs.buffer);
        // 서비스에 요청.
        var (errorCode, orgInfoStr) = await _roomDb.GameStartCheck(cWs.userId.Value, cWs.nickName);
        GameStartResponse response = new GameStartResponse() { errorCode = errorCode };
        await cWs.webSocket.SendAsync(response.Serialize(), WebSocketMessageType.Binary, true, CancellationToken.None);
        
        if (errorCode == ErrorCode.None && orgInfoStr != null)
        {
            GameStartNotify notify = new GameStartNotify() { gameInfoString = _gameServerInfoString };
            RoomInfo roomInfo = new RoomInfo(orgInfoStr);

            var pubErrorCode = await _roomDb.PubGameStart(roomInfo.info4Client);
            if (pubErrorCode != ErrorCode.None)
            {
                return pubErrorCode;
            }

            await SendInRoomAsync(roomInfo.allUserIds, notify.Serialize(), CancellationToken.None);
            return ErrorCode.None;
        }
        return errorCode;
    }

    // 전달받은 유저 list에 패킷을 날리는 함수
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

    // 기다렸다가 소켓을 닫는 함수
    async Task waitSockClose(CustomWebSocket cWs)
    {
        var currentTime = DateTime.Now;
        short closeCount = 0;
        ResponseHeader response = new ResponseHeader() { errorCode = cWs.errorCode };
        if (cWs.webSocket.State == WebSocketState.Open || cWs.webSocket.State == WebSocketState.Connecting)
        {
            // 소켓 닫기 요청을 보냄
            await cWs.webSocket.SendAsync(response.Serialize((Int32)PacketIdDef.GenericError), WebSocketMessageType.Binary, true, CancellationToken.None);
            while (cWs.webSocket.CloseStatus.HasValue == false)
            {
                if (closeCount == 3)
                {
                    using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await cWs.webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, null, cts.Token);
                }
                ++closeCount;
                await Task.Delay(1000);
            }
        }

        if (cWs.userId != null)
        {
            await _roomDb.LeaveRoom(cWs.userId.Value, cWs.nickName);
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
