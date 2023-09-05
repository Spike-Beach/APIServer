using APIServer.Controllers.ReqResModels;
using System.Text;

namespace APIServer.Service.Room.Model;

public class UnreadyRequest : RequestHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomUnreadyReq));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        return base.Deserialize(data);
    }
}

public class UnreadyResponse : ResponseHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomUnreadyRes));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        return base.Deserialize(data);
    }
}

public class UnReadyNotify : NotifyHeader
{
    public String teamString { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomUnreadyNtf));
        bytes.AddRange(Encoding.UTF8.GetBytes(teamString + '\0'));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        int offset = base.Deserialize(data);
        teamString = ReadString(data, ref offset);
        return offset;
    }
}
