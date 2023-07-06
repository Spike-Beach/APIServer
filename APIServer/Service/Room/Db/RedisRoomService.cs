//using CloudStructures;
//using CloudStructures.Structures;
using Microsoft.AspNetCore.Hosting.Server;
using StackExchange.Redis;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using ZLogger;

namespace APIServer.Service.Room;

public enum Team : Int16
{ 
    teamA = 0,
    teamB = 1,
}


public class RedisRoomDbService : IRoomDbService
{
    readonly Int16 MAX_ROOM_NUM = 50;
    readonly Int16 MAX_ROOM_USER_NUM = 4;
    readonly ILogger<RedisRoomDbService> _logger;
    readonly IDatabase _db;
    readonly StackExchange.Redis.IServer _server;

    readonly String SCRIPT_ROOM_LIST_UP = "local result = {}; for i=0, 50 do local title = redis.call('GET', 'room:' .. i .. ':title'); if title then local users = redis.call('ZCARD', 'room:' .. i .. ':users'); table.insert(result, i .. ':' .. title .. '-' .. users); end; end; return result;";
    readonly String SCRIPT_ROOM_ENTER = "if redis.call('EXISTS', @KEY0) ~= 0 then return 103 end local title = redis.call('GET', @KEY1) if title == nil then return 101 end local userIds = redis.call('ZRANGE', @KEY2, 0, -1) if #userIds >= 4 then return 102 end redis.call('SET', @KEY0, @ARGV2) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) local alluserIds = {} local allnicknames = {} local users = redis.call('ZRANGE', @KEY2, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) table.insert(allnicknames, users[i]) end local auserIds = {} local anicknames = {} local buserIds = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', @KEY3, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(auserIds, tonumber(readys_a[i + 1])) table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', @KEY4, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(buserIds, tonumber(readys_b[i + 1])) table.insert(bnicknames, readys_b[i]) end return title .. '\t' .. table.concat(allnicknames, ' ') .. '\t' .. table.concat(anicknames, ' ') .. '\t' .. table.concat(bnicknames, ' ') .. '\t' .. table.concat(alluserIds, ' ') .. '\t' .. table.concat(auserIds, ' ') .. '\t' .. table.concat(buserIds, ' ')";
    readonly String SCRIPT_ROOM_LEAVE = "local roomId = redis.call('GET', @KEY0) if roomId == nil then return 106 end redis.call('DEL', @KEY0) redis.call('zrem', 'room:' .. roomId .. ':users', @ARGV1) redis.call('zrem', 'room:' .. roomId .. ':readys_a', @ARGV1) redis.call('zrem', 'room:' .. roomId .. ':readys_b', @ARGV1) return 105";
    readonly String SCRIPT_READY_A = "local roomId = redis.call('GET', @KEY0) if roomId == nil then return 106 end local scoreinusers = redis.call('zscore', 'room:' .. roomId .. ':users', @ARGV1) local scoreinreadys_a = redis.call('zscore', 'room:' .. roomId .. ':readys_a', @ARGV1) local scoreinreadys_b = redis.call('zscore', 'room:' .. roomId .. ':readys_b', @ARGV1) if scoreinusers == nil then return 100 end if scoreinreadys_a == nil and scoreinreadys_b == nil then redis.call('ZADD', 'room:' .. roomId .. ':readys_a', @ARGV0, @ARGV1) local userIds = {} local users = redis.call('ZRANGE', 'room:' .. roomId .. ':users', 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(auserIds, tonumber(users[i + 1])) end return userIds end return 107";
    readonly String SCRIPT_READY_B = "local roomId = redis.call('GET', @KEY0) if roomId == nil then return 106 end local scoreinusers = redis.call('zscore', 'room:' .. roomId .. ':users', @ARGV1) local scoreinreadys_a = redis.call('zscore', 'room:' .. roomId .. ':readys_a', @ARGV1) local scoreinreadys_b = redis.call('zscore', 'room:' .. roomId .. ':readys_b', @ARGV1) if scoreinusers == nil then return 100 end if scoreinreadys_a == nil and scoreinreadys_b == nil then redis.call('ZADD', 'room:' .. roomId .. ':readys_b', @ARGV0, @ARGV1) local userIds = {} local users = redis.call('ZRANGE', 'room:' .. roomId .. ':users', 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(auserIds, tonumber(users[i + 1])) end return userIds end return 107";
    readonly String SCRIPT_UNREADY_A = "local roomId = redis.call('GET', @KEY0) if roomId == nil then return 106 end local scoreinusers = redis.call('zscore', 'room:' .. roomId .. ':users', @ARGV1) local scoreinreadys_a = redis.call('zscore', 'room:' .. roomId .. ':readys_a', @ARGV1) if scoreinusers == nil then return 100 end if scoreinreadys_a ~= nil then redis.call('ZREM', 'room:' .. roomId .. ':readys_a', @ARGV1) local userIds = {} local users = redis.call('ZRANGE', 'room:' .. roomId .. ':users', 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(userIds, tonumber(users[i + 1])) end return userIds end return 108";
    readonly String SCRIPT_UNREADY_B = "local roomId = redis.call('GET', @KEY0) if roomId == nil then return 106 end local scoreinusers = redis.call('zscore', 'room:' .. roomId .. ':users', @ARGV1) local scoreinreadys_b = redis.call('zscore', 'room:' .. roomId .. ':readys_b', @ARGV1) if scoreinusers == nil then return 100 end if scoreinreadys_b ~= nil then redis.call('ZREM', 'room:' .. roomId .. ':readys_b', @ARGV1) local userIds = {} local users = redis.call('ZRANGE', 'room:' .. roomId .. ':users', 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(userIds, tonumber(users[i + 1])) end return userIds end return 108";
    LoadedLuaScript _loadedRoomListUp;
    LoadedLuaScript _loadedRoomEnter;
    LoadedLuaScript _loadedRoomLeave;
    LoadedLuaScript _loadedReadyA;
    LoadedLuaScript _loadedReadyB;
    LoadedLuaScript _loadedUnreadyA;
    LoadedLuaScript _loadedUnreadyB;

    public RedisRoomDbService(IConfiguration config, ILogger<RedisRoomDbService> logger)
    {
        _logger = logger;
        var connectionString = config.GetConnectionString("Redis_Room");
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(connectionString);
        _db = redis.GetDatabase();
        _server = redis.GetServer(connectionString);
        var redisConfig = new CloudStructures.RedisConfig("room", connectionString);
    }

    public ErrorCode SetScripts()
    {
        try
        {
            var prepared = LuaScript.Prepare(SCRIPT_ROOM_LIST_UP);
            _loadedRoomListUp = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_ROOM_ENTER);
            _loadedRoomEnter = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_ROOM_LEAVE);
            _loadedRoomLeave = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_READY_A);
            _loadedReadyA = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_READY_B);
            _loadedReadyB = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_UNREADY_A);
            _loadedUnreadyA = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_UNREADY_B);
            _loadedUnreadyB = prepared.Load(_server);
            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogCriticalWithPayload(ex, new { session = "SetScripts" }, "SetScripts EXCEPTION");
            return ErrorCode.RoomDbError;
        }
    }

    public async Task<(ErrorCode, String?)> EnterRoom(short roomId, long userId, string nickname)
    {
        try
        {
            Object test = new { 
                KEY0 = (RedisKey)$"user:{userId}:room", // STRING
                KEY1 = (RedisKey)$"room:{roomId}:title", // STRING
                KEY2 = (RedisKey)$"room:{roomId}:users", // ZSET
                KEY3 = (RedisKey)$"room:{roomId}:readys_a", // ZSET
                KEY4 = (RedisKey)$"room:{roomId}:readys_b", // ZSET
                ARGV0 = (RedisValue)userId, ARGV1 = (RedisValue)nickname, ARGV2 = (RedisValue)roomId
            };

            var redisResult = await _loadedRoomEnter.EvaluateAsync(_db, test);
            if (redisResult == null)
            {
                _logger.ZLogErrorWithPayload(new { func = "EnterRoom", userId = userId }, "EnterRoom return null");
                return (ErrorCode.RoomDbError, null);
            }
            else if (redisResult.Type == ResultType.Integer)
            {
                return ((ErrorCode)((Int16)redisResult), null);
            }
            else if (redisResult.Type == ResultType.BulkString)
            {
                return (ErrorCode.None, (String)redisResult);
            }
            _logger.ZLogErrorWithPayload(new { func = "EnterRoom", userId = userId }, "EnterRoom return any other");
            return (ErrorCode.RoomDbError, null);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { func = "EnterRoom", userId = userId }, "SetScripts EXCEPTION");
            return (ErrorCode.RoomDbError, null);
        }
    }

    public async Task<(ErrorCode, List<string>?)> GetRoomList()
    {
        try
        {
            var redisResult = await _loadedRoomListUp.EvaluateAsync(_db);
            if (redisResult == null)
            {
                return (ErrorCode.RoomNotExist, null);
            }
            if (redisResult.Type != ResultType.MultiBulk)
            {
                return (ErrorCode.RoomDbError, null);
            }

            List<string> roomList = new List<string>();
            foreach (RedisResult item in (RedisResult[])redisResult)
            {
                roomList.Add((String)item);
            }
            return (ErrorCode.None, roomList);
        }
        catch (Exception ex) 
        {
            _logger.ZLogErrorWithPayload(ex, new { }, "GetRoomList EXCEPTION");
            return (ErrorCode.SessionError, null);
        }
    }

    public async Task<(ErrorCode, List<Int64>?)> LeaveRoom(Int64 userId, String nickname)
    {
        try
        {
            Object test = new
            {
                KEY0 = (RedisKey)$"user:{userId}:room", // STRING
                ARGV0 = (RedisValue)userId,
                ARGV1 = (RedisValue)nickname
            };

            var redisResult = await _loadedRoomLeave.EvaluateAsync(_db, test);
            if (redisResult == null)
            {
                _logger.ZLogErrorWithPayload(new { userId = userId }, "LeaveRoom return null");
                return (ErrorCode.RoomDbError, null);
            }
            else if (redisResult.Type == ResultType.Integer)
            {
                _logger.ZLogWarningWithPayload(new { userId = userId }, "SetUserReady status error");
                return ((ErrorCode)((Int16)redisResult), null);
            }
            else if (redisResult.Type == ResultType.MultiBulk)
            {
                List<Int64> userList = new List<Int64>();
                foreach (RedisResult item in (RedisResult[])redisResult)
                {
                    userList.Add((Int64)item);
                }
                return (ErrorCode.None, userList);
            }
            return (ErrorCode.RoomDbError, null);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { func = "EnterRoom", userId = userId }, "SetScripts EXCEPTION");
            return (ErrorCode.RoomDbError, null);
        }
    }


    public async Task<(ErrorCode, List<Int64>?)> SetUserReady(Int64 userId, String nickname, Team team)
    {
        try
        {
            Object test = new
            {
                KEY0 = (RedisKey)$"user:{userId}:room", // STRING
                ARGV0 = (RedisValue)userId,
                ARGV1 = (RedisValue)nickname
            };

            RedisResult redisResult;
            if (team == Team.teamA)
            {
                redisResult = await _loadedReadyA.EvaluateAsync(_db, test);
            }
            else // team == Team.teamB
            {
                redisResult = await _loadedReadyB.EvaluateAsync(_db, test);
            }

            if (redisResult == null)
            {
                _logger.ZLogErrorWithPayload(new { userId = userId }, "SetUserReady return null");
                return (ErrorCode.RoomDbError, null);
            }
            else if (redisResult.Type == ResultType.Integer)
            {
                _logger.ZLogWarningWithPayload(new { userId = userId }, "SetUserReady status error");
                return ((ErrorCode)((Int16)redisResult), null);
            }
            else if (redisResult.Type == ResultType.MultiBulk)
            {
                List<Int64> userList = new List<Int64>();
                foreach (RedisResult item in (RedisResult[])redisResult)
                {
                    userList.Add((Int64)item);
                }
                return (ErrorCode.None, userList);
            }
            return (ErrorCode.RoomDbError, null);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { func = "EnterRoom", userId = userId }, "SetUserReady EXCEPTION");
            return (ErrorCode.RoomDbError, null);
        }
    }

    public async Task<(ErrorCode, List<Int64>?)> SetUserUnready(Int64 userId, String nickname, Team team)
    {
        try
        {
            Object test = new
            {
                KEY0 = (RedisKey)$"user:{userId}:room", // STRING
                ARGV0 = (RedisValue)userId,
                ARGV1 = (RedisValue)nickname
            };

            RedisResult redisResult;
            if (team == Team.teamA)
            {
                redisResult = await _loadedUnreadyA.EvaluateAsync(_db, test);
            }
            else // team == Team.teamB
            {
                redisResult = await _loadedUnreadyB.EvaluateAsync(_db, test);
            }

            if (redisResult == null)
            {
                _logger.ZLogErrorWithPayload(new { userId = userId }, "SetUserReady return null");
                return (ErrorCode.RoomDbError, null);
            }
            else if (redisResult.Type == ResultType.Integer)
            {
                _logger.ZLogWarningWithPayload(new { userId = userId }, "SetUserReady status error");
                return ((ErrorCode)((Int16)redisResult), null);
            }
            else if (redisResult.Type == ResultType.MultiBulk)
            {
                List<Int64> userList = new List<Int64>();
                foreach (RedisResult item in (RedisResult[])redisResult)
                {
                    userList.Add((Int64)item);
                }
                return (ErrorCode.None, userList);
            }
            return (ErrorCode.RoomDbError, null);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { func = "EnterRoom", userId = userId }, "SetUserReady EXCEPTION");
            return (ErrorCode.RoomDbError, null);
        }
    }
}
