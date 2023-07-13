using APIServer.Controllers;
using System.Text;

namespace APIServer.Service.Room.Model;

public class GameStartRequest : RequestHeader
{
    public byte[] Serialize()
    {
        return base.Serialize((Int32)PacketIdDef.GameStartReq);
    }
    
    public override Int32 Deserialize(byte[] data)
    {
        return base.Deserialize(data);
    }
}

public class GameStartResponse : ResponseHeader
{
    public byte[] Serialize()
    {
        return base.Serialize((Int32)PacketIdDef.GameStartRes);
    }

    public override int Deserialize(byte[] data)
    {
        return base.Deserialize(data);
    }
}

public class GameStartNotify : NotifyHeader
{
    public String gameInfoString { get; set; } // ip:port
    public byte[] Serialize() 
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(base.Serialize((Int32)PacketIdDef.GameStartNtf));
        bytes.AddRange(Encoding.UTF8.GetBytes(gameInfoString + '\0'));
        return bytes.ToArray();
    }
    public override Int32 Deserialize(byte[] data)
    {
        Int32 offset = base.Deserialize(data);
        gameInfoString = ReadString(data, ref offset);
        return offset;
    }
}

