namespace APIServer.Service.Room.Model;

public class ResponseHeader
{
    public ErrorCode errorCode { get; set; }

    public virtual byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes((short)errorCode));
        return bytes.ToArray();
    }

    public virtual byte[] SetAndSerialize(ErrorCode inputErrorCode)
    {
        List<byte> bytes = new List<byte>();
        errorCode = inputErrorCode;
        bytes.AddRange(BitConverter.GetBytes((short)errorCode));
        return bytes.ToArray();
    }

    public virtual void Deserialize(byte[] data)
    {
        errorCode = (ErrorCode)BitConverter.ToInt16(data, 0);
    }
}