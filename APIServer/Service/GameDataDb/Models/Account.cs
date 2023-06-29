namespace APIServer.Service.GameDataDb.Models;

public class Account
{
    public Int64 user_id { get; set; }
    public String user_assigned_id { get; set; }
    public String nickname { get; set; }
    public Byte[] salt { get; set; }
    public Byte[] hashed_password { get; set; }
}
