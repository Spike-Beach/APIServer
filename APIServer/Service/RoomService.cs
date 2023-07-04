using APIServer.Controllers;
using APIServer.Controllers.ReqResModels;
using APIServer.GanaricModels;
using APIServer.Service.Room;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace APIServer.Service;

public class RoomService
{
    ConcurrentDictionary<Int64, WebSocket> _socketsDic = new ConcurrentDictionary<Int64, WebSocket>();
    Dictionary<PacketId, Func<RoomReqUserInfo?, WebSocket?, Int16?, Task<ErrorCode>>> _funcDic;
    IRoomDbService _roomService;

    public RoomService(IRoomDbService roomService)
    {
        _roomService = roomService;
        _funcDic = new Dictionary<PacketId, Func<RoomReqUserInfo?, WebSocket?, Int16?, Task<ErrorCode>>>
        {
            { PacketId.RoomEnter, userEnterRoom },
            { (Int16)PacketId.RoomLeave, userLeaveRoom },
            { (Int16)PacketId.RoomReady, userReady },
        };
    }
    
    public async Task<ErrorCode> ProcessRoomRequests(WebSocket webSocket, Byte[] Buffer)
    {
        // 헤더 파싱 후 유효성 확인
        // 헤더에 따라서 처리할 함수를 호출 -> 무조건 RoomEnterrm 그 이외엔 연결 끊기.
        SockHeader sockHeader = new SockHeader();
        try
        {
            userEnterRoom()
            while (!webSocket.CloseStatus.HasValue)
            {
                sockHeader.Deserialize(Buffer);
                if (sockHeader.PacketId != (Int32)PacketId.RoomEnter)
                {
                    throw new ArgumentException("Invalid Room Status");
                }
                receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);
                _funcDic
            }
        }
        catch (Exception ex) 
        {
            // 로깅
            await waitSockClose(webSocket, ErrorCode.InvalidPacketForm);
        }
    }

    public async Task<ErrorCode> userEnterRoom(RoomReqUserInfo? userInfo, WebSocket? socket, Int16? roomId)
    {
        WebSocket sock;


        //var (errorCode, userInfoSession) = await GetSession(request.userAssignedId);
        //if (errorCode != ErrorCode.None || userInfoSession == null)
        //{
        //    await waitSockClose(webSocket);
        //    return;
        //}

        //if (request.token.Equals(userInfoSession.token) != true)
        //{
        //    await waitSockClose(webSocket);
        //    return;
        //}
        //else if (request.clientVersion.Equals(_clientVersion) != true)
        //{
        //    await waitSockClose(webSocket);
        //    return;
        //}

        //(errorCode, var roomInfoString) = await _roomService.userEnterRoom(
        //    new GanaricModels.UserInfo { UserId = userInfoSession.userId, nickname = userInfoSession.nickname }, 
        //    request.roomId, webSocket);
        //if (errorCode != ErrorCode.None || roomInfoString == null)
        //{
        //    await waitSockClose(webSocket);
        //    return;
        //}

        //await webSocket.SendAsync(Encoding.UTF8.GetBytes(roomInfoString), WebSocketMessageType.Text, true, CancellationToken.None);


        if (_socketsDic.TryAdd(userInfo.UserId, socket) == false)
        {
            return (ErrorCode.InvalidUserData, null);
        }
        var (rtErrorCode, roomIntoString) = await _roomService.EnterRoom(roomId, userInfo.UserId, userInfo.nickname);
        if (rtErrorCode != ErrorCode.None)
        {
            _socketsDic.TryRemove(userInfo.UserId, out sock);
        }

        var userIds = ParsingUserId(roomIntoString);
        String msg = "hello";
        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(60));
        await SendInRoomAsync(new List<Int64>() { userInfo.UserId }, Encoding.UTF8.GetBytes(msg), cts.Token);
        return (ErrorCode.InvalidUserData, null);
    }

    async Task SendInRoomAsync(List<Int64> userIdArr, byte[] Msg, CancellationToken token)
    {
        WebSocket ws;
        foreach (var userId in userIdArr)
        {
            if (_socketsDic.TryGetValue(userId, out ws))
            {
                try
                {
                    await ws.SendAsync(Msg, WebSocketMessageType.Text, true, token);
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
        }
    }

    static List<Int64> ParsingUserId(String roomEnterString)
    {
        List<Int64> numbers = new List<Int64>();

        int lastNewLineIndex = roomEnterString.LastIndexOf('\n');
        if (lastNewLineIndex != -1 && lastNewLineIndex + 1 < roomEnterString.Length)
        {
            string usersString = roomEnterString.Substring(lastNewLineIndex + 1);
            string[] usersStrings = usersString.Split(' ');

            foreach (string userChunk in usersStrings)
            {
                String[] userIdAndNick = userChunk.Split(':');
                if (Int64.TryParse(userIdAndNick[0], out Int64 number))
                {
                    numbers.Add(number);
                }
                else
                {
                    // Parsing failed for a number, handle the error or skip the number.
                }
            }
        }
        else
        {
            // Invalid input format, handle the error.
        }
        return numbers;
    }

    static async Task Echo(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                receiveResult.MessageType,
                receiveResult.EndOfMessage,
                CancellationToken.None);

            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }
    async Task waitSockClose(WebSocket webSocket, ErrorCode errorCode)
    {
        var currentTime = DateTime.Now;
        Int16 closeCount = 0;
        SockResponseHeader response = new SockResponseHeader();
        await webSocket.SendAsync(response.SetAndSerialize(errorCode), WebSocketMessageType.Binary, true, CancellationToken.None);
        while (!webSocket.CloseStatus.HasValue)
        {
            if (closeCount == 3)
            {
                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, null, cts.Token);
            }
            ++closeCount;
            await Task.Delay(1000);
        }
    }
}
