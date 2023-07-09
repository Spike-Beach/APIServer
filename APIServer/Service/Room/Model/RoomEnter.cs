using APIServer.Controllers;
using APIServer.Controllers.ReqResModels;
using APIServer.Service.Room;
using System.Text;
using static Humanizer.In;

namespace APIServer.Service.Room.Model;
public class RoomEnterRequest : RequestHeader
{
    public string userAssignedId { get; set; }
    public string token { get; set; }
    public string clientVersion { get; set; }
    public Int16 roomId { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomEnterReq));
        bytes.AddRange(Encoding.UTF8.GetBytes(userAssignedId + '\0'));
        bytes.AddRange(Encoding.UTF8.GetBytes(token + '\0'));
        bytes.AddRange(Encoding.UTF8.GetBytes(clientVersion + '\0'));
        bytes.AddRange(BitConverter.GetBytes(roomId));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        int offset = base.Deserialize(data);

        userAssignedId = ReadString(data, ref offset);
        token = ReadString(data, ref offset);
        clientVersion = ReadString(data, ref offset);
        roomId = BitConverter.ToInt16(data, offset);
        return offset + sizeof(Int16);
    }
}

public class RoomEnterResponse : ResponseHeader
{
    public String roomInfoString { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomEnterRes));
        bytes.AddRange(Encoding.UTF8.GetBytes(roomInfoString + '\0'));
        return bytes.ToArray();
    }
    public override Int32 Deserialize(byte[] data)
    {
        Int32 offset = base.Deserialize(data);
        roomInfoString = ReadString(data, ref offset);
        return offset;
    }
}

public class RoomEnterNotify : NotifyHeader
{
    public String enterUserNick { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomEnterNtf)); // +1 : \0추가
        bytes.AddRange(Encoding.UTF8.GetBytes(enterUserNick + '\0'));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        Int32 offset = base.Deserialize(data);
        enterUserNick = ReadString(data, ref offset);
        return offset;
    }
}


