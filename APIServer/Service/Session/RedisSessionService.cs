using APIServer.Service.Session.Model;
using CloudStructures;
using CloudStructures.Structures;
using ZLogger;

namespace APIServer.Service.Session;

public class RedisSessionService : ISessionService
{

    RedisConnection _redisConnection;
    ILogger<RedisSessionService> _logger;

    public RedisSessionService(IConfiguration config, ILogger<RedisSessionService> logger)
    {
        _logger = logger;
        var connectionString = config.GetConnectionString("Redis_Session");
        var redisConfig = new CloudStructures.RedisConfig("session", connectionString);
        _redisConnection = new RedisConnection(redisConfig);
    }

    String GenerateSessionKey(String userAssignedId)
    {
        return "Session:" + userAssignedId;
    }

    // redis에 세션 set
    public async Task<ErrorCode> SetSession(SessionModel model)
    {
        try
        {
            var keyString = GenerateSessionKey(model.userAssignedId);
            var redisString = new RedisString<SessionModel>(_redisConnection, keyString, TimeSpan.FromHours(1));
            if (await redisString.SetAsync(model, TimeSpan.FromHours(1)) == true)
            {
                return ErrorCode.None;
            }
            else
            {
                _logger.ZLogErrorWithPayload(new { session = model }, "SetUserInfoSession Session Set FAIL");
                return ErrorCode.SessionError;
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { session = model }, "SetUserInfoSession EXCEPTION");
            return ErrorCode.SessionError;
        }
    }

    // redis에서 세션 get
    public async Task<(ErrorCode, SessionModel?)> GetSession(string userAssignedId)
    {
        try
        {
            var keyString = GenerateSessionKey(userAssignedId);
            var redisString = new RedisString<SessionModel>(_redisConnection, keyString, TimeSpan.FromHours(1));
            RedisHashSet<SessionModel> redisSet = new RedisHashSet<SessionModel>(_redisConnection, "Session", TimeSpan.FromHours(1));
            var session = await redisString.GetAsync();
            if (session.HasValue == false)
            {
                _logger.ZLogErrorWithPayload(new { userAssignedId = userAssignedId }, "GetUserInfoSession Invalid Id FAIL");
                return (ErrorCode.InvalidId, null);
            }
            return (ErrorCode.None, session.Value);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { userAssignedId = userAssignedId }, "GetUserInfoSession EXCEPTION");
            return (ErrorCode.SessionError, null);
        }
    }

    // redis에서 세션 delete
    public async Task<ErrorCode> DeleteSession(string userAssignedId)
    {
        try
        {
            var keyString = GenerateSessionKey(userAssignedId);
            var redisString = new RedisString<SessionModel>(_redisConnection, keyString, TimeSpan.FromHours(1));
            await redisString.DeleteAsync();
            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { userAssignedId = userAssignedId }, "GetUserInfoSession EXCEPTION");
            return ErrorCode.SessionError;
        }
    }
}
