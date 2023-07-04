using APIServer.Service.Room;
using System.Text;
using static Humanizer.In;

namespace APIServer.Controllers.ReqResModels;

public class SockHeader
{
    public readonly Byte[] MAGIC = { 0x09, 0x0B }; // 매직. 탭(\t)과 수직 탭(\v)
    public Int32 PacketSize { get; set; }
    public Int32 PacketId { get; set; }

    public Byte[] Serialize()
    {
        List<Byte> bytes = new List<Byte>();
        bytes.AddRange(MAGIC);
        bytes.AddRange(BitConverter.GetBytes(PacketSize));
        bytes.AddRange(BitConverter.GetBytes(PacketId));
        return bytes.ToArray();
    }

    public virtual void Deserialize(Byte[] data)
    {
        int offset = 0;
        if (data.Length < MAGIC.Length)
        {
            throw new ArgumentException("Invalid data length");
        }
        for (int i = 0; i < MAGIC.Length; i++)
        {
            if (data[offset + i] != MAGIC[i])
            {
                throw new ArgumentException("Invalid magic value");
            }
        }
        offset += MAGIC.Length;

        PacketSize = BitConverter.ToInt32(data, offset);
        offset += sizeof(int);

        PacketId = BitConverter.ToInt32(data, offset);
    }
}

public class RoomEnterRequest : SockHeader
{
    public String userAssignedId { get; set; }
    public String token { get; set; }
    public String clientVersion { get; set; }
    public Int16 roomId { get; set;}

    public Byte[] Serialize()
    {
        List<Byte> bytes = new List<Byte>();
        bytes.AddRange(base.Serialize());
        bytes.AddRange(Encoding.UTF8.GetBytes(userAssignedId + '\0'));
        bytes.AddRange(Encoding.UTF8.GetBytes(token + '\0'));
        bytes.AddRange(Encoding.UTF8.GetBytes(clientVersion + '\0'));
        bytes.AddRange(BitConverter.GetBytes(roomId));
        return bytes.ToArray();
    }

    public override void Deserialize(Byte[] data)
    {
        base.Deserialize(data);
        int offset = MAGIC.Length + sizeof(int) + sizeof(int);

        userAssignedId = ReadString(data, ref offset);
        token = ReadString(data, ref offset);
        clientVersion = ReadString(data, ref offset);
        roomId = BitConverter.ToInt16(data, offset);
    }

    private string ReadString(Byte[] data, ref int offset)
    {
        int length = 0;
        while (offset + length < data.Length && data[offset + length] != '\0')
        {
            length++;
        }

        string str = Encoding.UTF8.GetString(data, offset, length);
        offset += length + 1; // 문자열 끝에 '\0'를 건너뜁니다.
        return str;
    }
}

public class SockResponseHeader
{
    ErrorCode errorCode { get; set; }

    public Byte[] SetAndSerialize(ErrorCode inputErrorCode)
    {
        List<Byte> bytes = new List<Byte>();
        errorCode = inputErrorCode;
        bytes.AddRange(BitConverter.GetBytes((Int16)errorCode));
        return bytes.ToArray();
    }
}
