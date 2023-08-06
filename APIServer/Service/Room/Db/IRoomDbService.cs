namespace APIServer.Service.Room;

public class Room
{
    public String title { get; set; }
    public List<String> users { get; set; }
    public List<String> readyUsers { get; set; }
}
// -> 이걸 뭉치지 말고, key를 바꿔서 여러개로? room1title, room1users


public interface IRoomDbService
{
    ErrorCode SetScripts();
    ErrorCode Init();
    Task<(ErrorCode, List<String>?)> GetRoomList();
    Task<(ErrorCode, String?)> EnterRoom(Int16 roomId, Int64 userId, String nickname);
    Task<(ErrorCode, String?)> LeaveRoom(Int64 userId, String nickname);
    Task<(ErrorCode, String?)> SetUserReady(Int64 userId, String nickname, Team team);
    Task<(ErrorCode, String?)> SetUserUnready(Int64 userId, String nickname);
}
