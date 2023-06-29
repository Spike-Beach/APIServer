using APIServer.Service.Session.Model;

namespace APIServer.Service.Session;

public interface ISessionService
{
    public Task<ErrorCode> SetSession(SessionModel model);
    public Task<(ErrorCode, SessionModel?)> GetSession(String userAssignedId);
    public Task<ErrorCode> DeleteSession(String userAssignedId);
}
