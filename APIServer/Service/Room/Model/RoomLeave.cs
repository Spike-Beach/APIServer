﻿using APIServer.Controllers;
using System.Text;

namespace APIServer.Service.Room.Model;

public class RoomLeaveRequest : RequestHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(0, (Int32)PacketIdDef.RoomLeaveReq));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}

public class RoomLeaveResponse : ResponseHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(0, (Int32)PacketIdDef.RoomLeaveRes));
        return bytes.ToArray();
    }

    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
    }
}

public class RoomLeaveNotify : NotifyHeader
{
    public String leaveUserNick { get; set; }
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize(leaveUserNick.Length + 1, (Int32)PacketIdDef.RoomLeaveNtf));
        bytes.AddRange(Encoding.UTF8.GetBytes(leaveUserNick + '\0'));
        return bytes.ToArray();
    }
    public override void Deserialize(byte[] data)
    {
        base.Deserialize(data);
        int offset = MAGIC.Length + sizeof(int) + sizeof(int);
        leaveUserNick = ReadString(data, ref offset);
    }
}