namespace APIServer.Service.Room.Model;

public class UserReadyRequest : RequestHeader
{
    public Team team { get; set; }

    public override byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize());
        bytes.AddRange(BitConverter.GetBytes((int)team));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
        int offset = MAGIC.Length + sizeof(int) + sizeof(int);
        team = (Team)BitConverter.ToInt32(data, offset);
    }
}

public class UserUnreadyRequest : RequestHeader
{
    public Team team { get; set; }

    public override byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize());
        bytes.AddRange(BitConverter.GetBytes((int)team));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
        int offset = MAGIC.Length + sizeof(int) + sizeof(int);
        team = (Team)BitConverter.ToInt16(data, offset);
    }
}

public class UserReadyResponse : ResponseHeader
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

public class UserUnreadyResponse : ResponseHeader
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

