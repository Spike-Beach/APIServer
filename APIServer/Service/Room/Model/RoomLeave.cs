namespace APIServer.Service.Room.Model;

public class RoomLeaveRequest : RequestHeader
{
    public override byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize());
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}

public class RoomLeaveResponse : ResponseHeader
{
    public override byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize());
        return bytes.ToArray();
    }

    public override byte[] SetAndSerialize(ErrorCode inputErrorCode)
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.SetAndSerialize(inputErrorCode));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}
