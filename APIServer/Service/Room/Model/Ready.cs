using APIServer.Controllers.ReqResModels;
using System.Text;

namespace APIServer.Service.Room.Model;

public class ReadyRequest : RequestHeader
{
    public Team team { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomReadyReq));
        bytes.AddRange(BitConverter.GetBytes((int)team));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        int offset = base.Deserialize(data);
        team = (Team)BitConverter.ToInt32(data, offset);
        return offset + sizeof(Team);
    }
}



public class ReadyResponse : ResponseHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomReadyRes));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        return base.Deserialize(data);
    }
}

public class ReadyNotify : NotifyHeader
{
    public String teamString { get; set; }
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomReadyNtf));
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

