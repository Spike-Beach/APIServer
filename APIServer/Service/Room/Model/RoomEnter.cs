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
    public short roomId { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize());
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

    private string ReadString(byte[] data, ref int offset)
    {
        int length = 0;
        while (offset + length < data.Length && data[offset + length] != '\0')
        {
            length++;
        }

        string str = Encoding.UTF8.GetString(data, offset, length);
        offset += length + 1;
        return str;
    }
}

public class roomEnterResponse : ResponseHeader
{
    public String roomInfoString { get; set; }

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize());
        bytes.AddRange(Encoding.UTF8.GetBytes(roomInfoString + '\0'));
        return bytes.ToArray();
    }
}


