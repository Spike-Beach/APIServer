namespace APIServer.Controllers.ReqResModels;

public class RoomListupRequset : BaseRequest
{
}

public class RoomListupResponse : BaseResponse
{
    public List<String>? roomList { get; set; }
}
