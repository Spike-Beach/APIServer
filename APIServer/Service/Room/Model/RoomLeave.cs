﻿using APIServer.Controllers.ReqResModels;
using System.Text;

namespace APIServer.Service.Room.Model;

public class RoomLeaveRequest : RequestHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomLeaveReq));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        return base.Deserialize(data);
    }
}

public class RoomLeaveResponse : ResponseHeader
{
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomLeaveRes));
        return bytes.ToArray();
    }

    public override Int32 Deserialize(byte[] data)
    {
        return base.Deserialize(data);
    }
}

public class RoomLeaveNotify : NotifyHeader
{
    public String leaveInfoString { get; set; }
    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.RoomLeaveNtf));
        bytes.AddRange(Encoding.UTF8.GetBytes(leaveInfoString + '\0'));
        return bytes.ToArray();
    }
    public override Int32 Deserialize(byte[] data)
    {
        int offset = base.Deserialize(data);
        leaveInfoString = ReadString(data, ref offset);
        return offset;
    }
}