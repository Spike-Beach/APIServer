using APIServer.Controllers;
using System.Text;

namespace APIServer.Service.Room.Model;

public class ReadyRequest : RequestHeader
{
    public Team team { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(sizeof(Team), (Int32)PacketIdDef.RoomReadyReq));
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



public class ReadyResponse : ResponseHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(0, (Int32)PacketIdDef.RoomReadyRes));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}

public class ReadyNotify : NotifyHeader
{
    public String teamString { get; set; }
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(teamString.Length + 1, (Int32)PacketIdDef.RoomReadyNtf));
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

