﻿using System.Text;

namespace APIServer.Service.Room.Model;

public class RequestHeader
{
    public readonly byte[] MAGIC = { 0x0C, 0x0B }; // 매직. 피드(\f)와 수직 탭(\v)
    public int PacketSize { get; set; }
    public int PacketId { get; set; }

    protected byte[] Serialize(Int32 bodySize, Int32 fullPacketId)
    {
        List<byte> bytes = new List<byte>();
        PacketSize = MAGIC.Length + sizeof(Int32) + sizeof(Int32) + bodySize;
        PacketId = fullPacketId;
        bytes.AddRange(MAGIC);
        bytes.AddRange(BitConverter.GetBytes(PacketSize));
        bytes.AddRange(BitConverter.GetBytes(PacketId));
        return bytes.ToArray();
    }

    public virtual void Deserialize(byte[] data)
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