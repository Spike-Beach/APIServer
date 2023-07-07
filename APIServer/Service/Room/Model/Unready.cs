using APIServer.Controllers;
using System.Text;

namespace APIServer.Service.Room.Model;

public class UnreadyRequest : RequestHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(0, (Int32)PacketIdDef.RoomUnreadyReq));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}

public class UnreadyResponse : ResponseHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(0, (Int32)PacketIdDef.RoomUnreadyRes));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}

public class UnReadyNotify : NotifyHeader
{
    public String teamString { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(teamString.Length + 1, (Int32)PacketIdDef.RoomUnreadyNtf));
        bytes.AddRange(Encoding.UTF8.GetBytes(teamString + '\0'));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
        int offset = MAGIC.Length + sizeof(int) + sizeof(int);
        teamString = ReadString(data, ref offset);
    }
}
