namespace APIServer.Service.Session.Model;

public enum UserStatus : Int16
{
    Login = 0,
    Room = 1,
    Gaming = 2
}

public class SessionModel
{
    public Int64 userId { get; set; }
    public String userAssignedId { get; set; }
    public String token { get; set; }

}
