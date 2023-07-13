using APIServer.Service.GameDataDb.Models;
using MySqlConnector;
using SqlKata.Compilers;
using SqlKata.Execution;
using System.Reflection;
using ZLogger;

namespace APIServer.Service.GameDataDb;

public class MysqlGameDbService : IGameDbServcie
{
    readonly ILogger<MysqlGameDbService> _logger;
    readonly QueryFactory _db;
    public MysqlGameDbService(IConfiguration config, ILogger<MysqlGameDbService> logger)
    {
        var connString = config.GetConnectionString("Mysql_GameDb");
        var connection = new MySqlConnection(connString);
        var compiler = new MySqlCompiler();
        _db = new QueryFactory(connection, compiler);
        _logger = logger;
    }

    public async Task<(ErrorCode, Int64)> RegistAccount(String userAssignedId, String nickname, byte[] salt, byte[] hashed_password)
    {
        try
        {
            var id = await _db.Query("user_accounts").InsertGetIdAsync<Int64>(new Account
            {
                user_assigned_id = userAssignedId,
                nickname = nickname,
                salt = salt,
                hashed_password = hashed_password,
            });
            _logger.ZLogInformationWithPayload(new { userAssignedId = userAssignedId }, "RegistAccount SUCCESS");
            return (ErrorCode.None, id);
        }
        catch (MySqlException ex)
        {
            if (ex.Number == 1062) //duplicated id exception
            {
                if (ex.Message.Contains("user_assigned_id"))
                {
                    return (ErrorCode.DuplicatedId, -1);
                }
                else if (ex.Message.Contains("nickname"))
                {
                    return (ErrorCode.DuplicatedNickname, -1);
                }
                return (ErrorCode.DuplicatedId, -1);
            }
            _logger.ZLogErrorWithPayload(ex, new { userAssignedId = userAssignedId }, "RegistAccount MYSQL_EXCEPTION");
            return (ErrorCode.AccountDbError, -1);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { userAssignedId = userAssignedId }, "RegistAccount EXCEPTION");
            return (ErrorCode.AccountDbError, -1);
        }
    }
    public async Task<(ErrorCode, Account?)> ReadAccount(string userAssignedId)
    {
        try
        {
            var account = await _db.Query("user_accounts")
                .Select("*")
                .Where("user_assigned_id", userAssignedId)
                .FirstAsync<Account?>();
            if (account == null)
            {
                _logger.ZLogWarningWithPayload(new { userAssignedId = userAssignedId }, "ReadAccount Invalid Id FAIL");
                return (ErrorCode.InvalidId, null);
            }

            return (ErrorCode.None, account);
        }
        catch (MySqlException ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { userAssignedId = userAssignedId }, "ReadAccount MYSQL_EXCEPTION");
            return (ErrorCode.AccountDbError, null);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { userAssignedId = userAssignedId }, "ReadAccount EXCEPTION");
            return (ErrorCode.AccountDbError, null);
        }
    }

    public async Task<ErrorCode> RegistUserInfo(Int64 userId)
    {
        try
        {
            var id = await _db.Query("user_infos").InsertAsync( new { user_id = userId });
            return ErrorCode.None;
        }
        catch (MySqlException ex)
        {
            if (ex.Number == 1062) //duplicated id exception
            {
                _logger.ZLogCriticalWithPayload(ex, new { userId = userId }, "RegistUserInfo duplicated id");
                return ErrorCode.DuplicatedId;
            }
            _logger.ZLogErrorWithPayload(ex, new { num = ex.Number, userId = userId }, "RegistUserInfo MYSQL_EXCEPTION");
            return ErrorCode.AccountDbError;
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { userId = userId }, "RegistUserInfo EXCEPTION");
            return ErrorCode.AccountDbError;
        }
    }

    public async Task<(ErrorCode, UserInfo?)> ReadUserInfo(Int64 userId)
    {
        try
        {
            var userInfo = await _db.Query("user_infos")
                .Select("*")
                .Where("user_id", userId)
                .FirstAsync<UserInfo>();
            if(userInfo == null)
            {
                _logger.ZLogCriticalWithPayload( new { userId = userId }, "user haven't info");
                return (ErrorCode.GameDbError, null);
            }
            return (ErrorCode.None, userInfo);
        }
        catch (MySqlException ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { userId = userId }, "ReadUserInfo MYSQL_EXCEPTION");
            return (ErrorCode.AccountDbError, null);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { userId = userId }, "ReadUserInfo EXCEPTION");
            return (ErrorCode.AccountDbError, null);
        }
    }
}
