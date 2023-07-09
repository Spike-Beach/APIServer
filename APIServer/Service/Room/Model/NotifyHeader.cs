using System.Text;

namespace APIServer.Service.Room.Model;

public class NotifyHeader
{
    public Int32 packetId { get; set; }

    protected byte[] Serialize(Int32 fullPacketId)
    {
        List<byte> bytes = new List<byte>();
        packetId = fullPacketId;
        bytes.AddRange(BitConverter.GetBytes(packetId));
        return bytes.ToArray();
    }

    public virtual Int32 Deserialize(byte[] data)
    {
        packetId = BitConverter.ToInt32(data, 0);
        return sizeof(int);
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
