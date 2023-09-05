using System.Net.WebSockets;
namespace APIServer.Service.Room;

public class CustomWebSocket
{
    public WebSocket webSocket { get; set; }
    public long? userId { get; set; }
    public String nickName { get; set; }
    public byte[] buffer { get; set; } = new byte[1024];
    public ErrorCode errorCode { get; set; }
}

public class RoomInfo
{
    public String info4Client { get; }
    public List<Int64> allUserIds { get; }
    public Int64 hostId { get; }
    public List<Int64> teamAUserIds { get; }
    public List<Int64> teamBUserIds { get; }
    public RoomInfo(String? orgInfoString)
    {
        if (orgInfoString == null)
        {
            return;
        }
        string[] splitData = orgInfoString.Split("\t", StringSplitOptions.RemoveEmptyEntries);

        allUserIds = new List<long>();
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
            else if (char.IsUpper(item[0]))
            {
                info4Client += "\t" + item;
            }
        }
    }
}
