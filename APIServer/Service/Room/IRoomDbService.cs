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
    Task<(ErrorCode, List<String>?)> GetRoomList();
    //Task<(ErrorCode, Int16 roomId)> CreateRoom(String roomTitle);
    Task<(ErrorCode, String?)> EnterRoom(Int16 roomId, Int64 userId, String nickname);
    Task<ErrorCode> LeaveRoom(Int16 roomId, Int16 userId, String nickname);
    Task<ErrorCode> SetUserReady(Int16 roomId, Int16 userId);
}
