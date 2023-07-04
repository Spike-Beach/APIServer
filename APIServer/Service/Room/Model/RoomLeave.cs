namespace APIServer.Service.Room.Model;

public class RoomLeaveRequest : RequestHeader
{
    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}

public class RoomLeaveResponse : ResponseHeader
{
    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}
