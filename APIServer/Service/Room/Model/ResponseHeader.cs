using System.Text;

namespace APIServer.Service.Room.Model;

public class ResponseHeader
{
    public Int32 packetId { get; set; }
    
    public ErrorCode errorCode { get; set; }

    public byte[] Serialize(Int32 fullPacketId)
    {
        List<byte> bytes = new List<byte>();
        packetId = fullPacketId;
        bytes.AddRange(BitConverter.GetBytes(packetId));
        bytes.AddRange(BitConverter.GetBytes((Int16)errorCode));
        return bytes.ToArray();
    }

    public virtual Int32 Deserialize(byte[] data)
    {
        int offset = 0;
        
        packetId = BitConverter.ToInt32(data, offset);
        offset += sizeof(Int32);

        errorCode = (ErrorCode)BitConverter.ToInt16(data, offset);
        offset += sizeof(ErrorCode);

        return offset;
    }

    protected String ReadString(byte[] data, ref int offset)
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