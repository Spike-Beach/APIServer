using System.Net.WebSockets;

namespace APIServer.GanaricModels;

public class RoomReqUserInfo
{
    public Int64 UserId { get; set; }
    public string nickname { get; set; }
    public WebSocket webSocket { get; set; }
}
