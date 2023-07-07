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
        bytes.AddRange(base.Serialize(
            userAssignedId.Length + token.Length + clientVersion.Length + sizeof(Int16) + 3, 
            (Int32)PacketIdDef.RoomEnterReq
            )); // +3 : \0 * 3
        bytes.AddRange(Encoding.UTF8.GetBytes(userAssignedId + '\0'));
        bytes.AddRange(Encoding.UTF8.GetBytes(token + '\0'));
        bytes.AddRange(Encoding.UTF8.GetBytes(clientVersion + '\0'));
        bytes.AddRange(BitConverter.GetBytes(roomId));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
        int offset = MAGIC.Length + sizeof(int) + sizeof(int);

        userAssignedId = ReadString(data, ref offset);
        token = ReadString(data, ref offset);
        clientVersion = ReadString(data, ref offset);
        roomId = BitConverter.ToInt16(data, offset);
    }


}

public class RoomEnterResponse : ResponseHeader
{
    public String roomInfoString { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(roomInfoString.Length + 1, (Int32)PacketIdDef.RoomEnterRes));
        bytes.AddRange(Encoding.UTF8.GetBytes(roomInfoString + '\0'));
        return bytes.ToArray();
    }
    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
        Int32 offset = MAGIC.Length + sizeof(int) + sizeof(int);
        roomInfoString = ReadString(data, ref offset);
    }
}

public class RoomEnterNotify : NotifyHeader
{
    public String enterUserNick { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(enterUserNick.Length + 1, (Int32)PacketIdDef.RoomEnterNtf)); // +1 : \0추가
        bytes.AddRange(Encoding.UTF8.GetBytes(enterUserNick + '\0'));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
        Int32 offset = MAGIC.Length + sizeof(int) + sizeof(int);
        enterUserNick = ReadString(data, ref offset);
    }
}


