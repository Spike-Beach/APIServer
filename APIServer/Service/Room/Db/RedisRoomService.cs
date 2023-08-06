//using CloudStructures;
//using CloudStructures.Structures;
using Microsoft.AspNetCore.Hosting.Server;
using StackExchange.Redis;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using ZLogger;
using ZLogger.Entries;

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

    readonly String SCRIPT_ROOM_LIST_UP = "local roomKeys = redis.call('KEYS', 'room:*:title') local roomInfo = {} for i, roomKey in ipairs(roomKeys) do local roomId = roomKey:match('room:(%d+):title') local title = redis.call('GET', roomKey) local users = redis.call('ZCARD', 'room:' .. roomId .. ':users') table.insert(roomInfo, '\t' .. roomId .. '\t' .. title .. '\t' .. users) end return roomInfo";
    //    readonly String SCRIPT_ROOM_ENTER = "if redis.call('EXISTS', @KEY0) ~= 0 then return 103 end local title = redis.call('GET', @KEY1) if title == false or title == nil then redis.call('DEL', @KEY2, @KEY3, @KEY4, @KEY5) redis.call('INCR', 'room:count') local title2 = 'Room' .. @ARGV2 redis.call('SET', @KEY1, title2) redis.call('SET', @KEY0, @ARGV2) redis.call('ZADD', @KEY5, @ARGV0, @ARGV1) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) return '\tT ' .. title2 .. '\tU ' .. @ARGV1 .. '\tH ' .. @ARGV1 .. '\tu ' .. @ARGV0 .. '\th ' .. @ARGV0 end local userIds = redis.call('ZRANGE', @KEY2, 0, -1) if #userIds >= 4 then return 102 end redis.call('SET', @KEY0, @ARGV2) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) local alluserIds = {} local allnicknames = {} local users = redis.call('ZRANGE', @KEY2, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) table.insert(allnicknames, users[i]) end local anicknames = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', @KEY3, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', @KEY4, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(bnicknames, readys_b[i]) end local host = redis.call('ZRANGE', 'room:' .. @ARGV2.. ':host', 0, -1, 'WITHSCORES') return '\tT ' .. title .. '\tU ' .. table.concat(allnicknames, ' ') .. '\tH '  .. host[1] .. '\tA ' .. table.concat(anicknames, ' ') .. '\tB ' .. table.concat(bnicknames, ' ') .. '\tu ' .. table.concat(alluserIds, ' ') .. '\th ' .. host[2]";
    //readonly String SCRIPT_ROOM_ENTER = "if redis.call('EXISTS', @KEY0) ~= 0 then return 103 end local roomId = tonumber(@ARGV2) if roomId == -1 then roomId = math.random(0, 9999) while redis.call('EXISTS', 'room:' .. roomId .. ':title') == 1 do roomId = math.random(0, 9999) end end local title = redis.call('GET', @KEY1) if title == false or title == nil then redis.call('DEL', @KEY2, @KEY3, @KEY4, @KEY5) if redis.call('GET', 'room:count') >= 49 then return 107 end redis.call('INCR', 'room:count') local title2 = 'Room' .. roomId redis.call('SET', @KEY1, title2) redis.call('SET', @KEY0, roomId) redis.call('ZADD', @KEY5, @ARGV0, @ARGV1) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) return '\tI ' .. roomId .. '\tT ' .. title2 .. '\tU ' .. @ARGV1 .. '\tH ' .. @ARGV1 .. '\tu ' .. @ARGV0 .. '\th ' .. @ARGV0 end local userIds = redis.call('ZRANGE', @KEY2, 0, -1) if #userIds >= 4 then return 102 end redis.call('SET', @KEY0, roomId) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) local alluserIds = {} local allnicknames = {} local users = redis.call('ZRANGE', @KEY2, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) table.insert(allnicknames, users[i]) end local anicknames = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', @KEY3, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', @KEY4, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(bnicknames, readys_b[i]) end local host = redis.call('ZRANGE', 'room:' .. roomId.. ':host', 0, -1, 'WITHSCORES') return '\tI ' .. roomId .. '\tT ' .. title .. '\tU ' .. table.concat(allnicknames, ' ') .. '\tH '  .. host[1] .. '\tA ' .. table.concat(anicknames, ' ') .. '\tB ' .. table.concat(bnicknames, ' ') .. '\tu ' .. table.concat(alluserIds, ' ') .. '\th ' .. host[2]";
    readonly String SCRIPT_ROOM_ENTER = "if redis.call('EXISTS', @KEY0) == 1 then return 103 end local roomId = tonumber(@ARGV2) if roomId == -1 then roomId = math.random(0, 9999) while redis.call('EXISTS', 'room:' .. roomId .. ':title') == 1 do roomId = math.random(0, 9999) end end local title = redis.call('GET', @KEY1) if title == false or title == nil then redis.call('DEL', @KEY2, @KEY3, @KEY4, @KEY5) if tonumber(redis.call('GET', 'room:count')) >= 49 then return 107 end redis.call('INCR', 'room:count') local title2 = 'Room' .. roomId redis.call('SET', @KEY1, title2) redis.call('SET', @KEY0, roomId) redis.call('ZADD', @KEY5, @ARGV0, @ARGV1) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) return '\tI ' .. roomId .. '\tT ' .. title2 .. '\tU ' .. @ARGV1 .. '\tH ' .. @ARGV1 .. '\tu ' .. @ARGV0 .. '\th ' .. @ARGV0 end local userIds = redis.call('ZRANGE', @KEY2, 0, -1) if #userIds >= 4 then return 102 end redis.call('SET', @KEY0, roomId) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) local alluserIds = {} local allnicknames = {} local users = redis.call('ZRANGE', @KEY2, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) table.insert(allnicknames, users[i]) end local anicknames = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', @KEY3, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', @KEY4, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(bnicknames, readys_b[i]) end local host = redis.call('ZRANGE', 'room:' .. roomId.. ':host', 0, -1, 'WITHSCORES') return '\tI ' .. roomId .. '\tT ' .. title .. '\tU ' .. table.concat(allnicknames, ' ') .. '\tH '  .. host[1] .. '\tA ' .. table.concat(anicknames, ' ') .. '\tB ' .. table.concat(bnicknames, ' ') .. '\tu ' .. table.concat(alluserIds, ' ') .. '\th ' .. host[2]";
    //readonly String SCRIPT_ROOM_ENTER = "if redis.call('EXISTS', @KEY0) == 1 then return 103 end local roomId = tonumber(@ARGV2) if roomId == -1 then roomId = math.random(0, 9999) while redis.call('EXISTS', 'room:' .. roomId .. ':title') == 1 do roomId = math.random(0, 9999) end end local title = redis.call('GET', @KEY1) if title == false or title == nil then redis.call('DEL', @KEY2, @KEY3, @KEY4, @KEY5) redis.call('INCR', 'room:count') local title2 = 'Room' .. roomId redis.call('SET', @KEY1, title2) redis.call('SET', @KEY0, roomId) redis.call('ZADD', @KEY5, @ARGV0, @ARGV1) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) return '\tI ' .. roomId .. '\tT ' .. title2 .. '\tU ' .. @ARGV1 .. '\tH ' .. @ARGV1 .. '\tu ' .. @ARGV0 .. '\th ' .. @ARGV0 end local userIds = redis.call('ZRANGE', @KEY2, 0, -1) if #userIds >= 4 then return 102 end redis.call('SET', @KEY0, roomId) redis.call('ZADD', @KEY2, @ARGV0, @ARGV1) local alluserIds = {} local allnicknames = {} local users = redis.call('ZRANGE', @KEY2, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) table.insert(allnicknames, users[i]) end local anicknames = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', @KEY3, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', @KEY4, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(bnicknames, readys_b[i]) end local host = redis.call('ZRANGE', 'room:' .. roomId.. ':host', 0, -1, 'WITHSCORES') return '\tI ' .. roomId .. '\tT ' .. title .. '\tU ' .. table.concat(allnicknames, ' ') .. '\tH '  .. host[1] .. '\tA ' .. table.concat(anicknames, ' ') .. '\tB ' .. table.concat(bnicknames, ' ') .. '\tu ' .. table.concat(alluserIds, ' ') .. '\th ' .. host[2]";
    readonly String SCRIPT_ROOM_LEAVE = "local roomId = redis.call('GET', @KEY0) if roomId == false or roomId == nil then return 104 end local roomtitle = 'room:' .. roomId .. ':title' local roomusers = 'room:' .. roomId .. ':users' local roomreadys_a = 'room:' .. roomId .. ':readys_a' local roomreadys_b = 'room:' .. roomId .. ':readys_b' local roomhost = 'room:' .. roomId .. ':host' redis.call('DEL', @KEY0) redis.call('ZREM', roomreadys_a, @ARGV1) redis.call('ZREM', roomreadys_b, @ARGV1) redis.call('ZREM', roomusers, @ARGV1) local isdeletedhost = tonumber(redis.call('ZREM', roomhost, @ARGV1)) if isdeletedhost > 0 then local newhost = redis.call('ZPOPMIN', roomusers, 1) if #newhost == 0 then redis.call('DEL', roomtitle) redis.call('DECR', 'room:count') return 108 end redis.call('ZADD', roomhost, newhost[2], newhost[1]) end local alluserIds = {} local users = redis.call('ZRANGE', roomusers, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) end local host = redis.call('ZRANGE', roomhost, 0, -1, 'WITHSCORES') return '\tL ' .. @ARGV1 .. '\tH ' .. host[1] .. '\tu ' .. table.concat(alluserIds, ' ')";
    readonly String SCRIPT_READY_A =    "local roomId = redis.call('GET', @KEY0) if roomId == false or roomId == nil then return 104 end local roomusers = 'room:' .. roomId .. ':users' local roomreadys_a = 'room:' .. roomId .. ':readys_a' local roomreadys_b = 'room:' .. roomId .. ':readys_b' local scoreinusers = redis.call('zscore', roomusers, @ARGV1) if scoreinusers == false or scoreinusers == nil then return 104 end redis.call('ZREM', roomreadys_b, @ARGV1) redis.call('ZADD', roomreadys_a, @ARGV0, @ARGV1) local alluserIds = {} local users = redis.call('ZRANGE', roomusers, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) end local anicknames = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', roomreadys_a, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', roomreadys_b, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(bnicknames, readys_b[i]) end return '\tA ' .. table.concat(anicknames, ' ') .. '\tB ' .. table.concat(bnicknames, ' ') .. '\tu ' .. table.concat(alluserIds, ' ')";
    readonly String SCRIPT_READY_B =    "local roomId = redis.call('GET', @KEY0) if roomId == false or roomId == nil then return 104 end local roomusers = 'room:' .. roomId .. ':users' local roomreadys_a = 'room:' .. roomId .. ':readys_a' local roomreadys_b = 'room:' .. roomId .. ':readys_b' local scoreinusers = redis.call('zscore', roomusers, @ARGV1) if scoreinusers == false or scoreinusers == nil then return 104 end redis.call('ZREM', roomreadys_a, @ARGV1) redis.call('ZADD', roomreadys_b, @ARGV0, @ARGV1) local alluserIds = {} local users = redis.call('ZRANGE', roomusers, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) end local anicknames = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', roomreadys_a, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', roomreadys_b, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(bnicknames, readys_b[i]) end return '\tA ' .. table.concat(anicknames, ' ') .. '\tB ' .. table.concat(bnicknames, ' ') .. '\tu ' .. table.concat(alluserIds, ' ')";
    readonly String SCRIPT_UNREADY =    "local roomId = redis.call('GET', @KEY0) if roomId == false or roomId == nil then return 104 end local roomusers = 'room:' .. roomId .. ':users' local roomreadys_a = 'room:' .. roomId .. ':readys_a' local roomreadys_b = 'room:' .. roomId .. ':readys_b' local scoreinusers = redis.call('zscore', roomusers, @ARGV1) if scoreinusers == false or scoreinusers == nil then return 104 end redis.call('ZREM', roomreadys_a, @ARGV1) redis.call('ZREM', roomreadys_b, @ARGV1) local alluserIds = {} local users = redis.call('ZRANGE', roomusers, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) end local anicknames = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', roomreadys_a, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', roomreadys_b, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(bnicknames, readys_b[i]) end return '\tA ' .. table.concat(anicknames, ' ') .. '\tB ' .. table.concat(bnicknames, ' ') .. '\tu ' .. table.concat(alluserIds, ' ')";
    readonly String SCRIPT_GAEMSTART =  "local roomId = redis.call('GET', @KEY0) if roomId == false or roomId == nil then return 104 end local roomusers = 'room:' .. roomId .. ':users' local roomreadys_a = 'room:' .. roomId .. ':readys_a' local roomreadys_b = 'room:' .. roomId .. ':readys_b' local roomtitle = 'room:' .. roomId .. ':title' local title = redis.call('GET', roomtitle) if title == false or title == nil then return 3 end local alluserIds = {} local allnicknames = {} local users = redis.call('ZRANGE', roomusers, 0, -1, 'WITHSCORES') for i = 1, #users, 2 do table.insert(alluserIds, tonumber(users[i + 1])) table.insert(allnicknames, users[i]) end  local anicknames = {} local bnicknames = {} local readys_a = redis.call('ZRANGE', roomreadys_a, 0, -1, 'WITHSCORES') for i = 1, #readys_a, 2 do table.insert(anicknames, readys_a[i]) end local readys_b = redis.call('ZRANGE', roomreadys_b, 0, -1, 'WITHSCORES') for i = 1, #readys_b, 2 do table.insert(bnicknames, readys_b[i]) end local host = redis.call('ZRANGE', 'room:' .. roomId.. ':host', 0, -1, 'WITHSCORES') if host[2] ~= @ARGV0 then return 109 end if #users ~= 8 or #readys_a ~= 4 or #readys_b ~= 4 then return 110 end return '\tI ' .. roomId .. '\tT ' .. title .. '\tU ' .. table.concat(allnicknames, ' ') .. '\tH '  .. host[1] .. '\tA ' .. table.concat(anicknames, ' ') .. '\tB ' .. table.concat(bnicknames, ' ') .. '\tu ' .. table.concat(alluserIds, ' ')";
    LoadedLuaScript _loadedRoomListUp;
    LoadedLuaScript _loadedRoomEnter;
    LoadedLuaScript _loadedRoomLeave;
    LoadedLuaScript _loadedReadyA;
    LoadedLuaScript _loadedReadyB;
    LoadedLuaScript _loadedUnready;
    LoadedLuaScript _loadedGameStart;

    public RedisRoomDbService(IConfiguration config, ILogger<RedisRoomDbService> logger)
    {
        _logger = logger;
        var connectionString = config.GetConnectionString("Redis_Room");
        var options = ConfigurationOptions.Parse(connectionString);
        options.Password = "1q2w3e4r";
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(options);
        _db = redis.GetDatabase();
        _server = redis.GetServer(connectionString);
        var redisConfig = new CloudStructures.RedisConfig("room", options);
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
            prepared = LuaScript.Prepare(SCRIPT_UNREADY);
            _loadedUnready = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_GAEMSTART);
            _loadedGameStart = prepared.Load(_server);
            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogCriticalWithPayload(ex, new { session = "SetScripts" }, "SetScripts EXCEPTION");
            return ErrorCode.RoomDbError;
        }
    }

    public ErrorCode Init()
    {
        try
        {
            _db.StringSet("room:count", "0");
        }
        catch (Exception ex)
        {
            _logger.ZLogCriticalWithPayload(ex, new { session = "SetScripts" }, "SetScripts EXCEPTION");
            return ErrorCode.RoomDbError;
        }
        return ErrorCode.None;
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
                KEY5 = (RedisKey)$"room:{roomId}:host", // ZSET
                ARGV0 = (RedisValue)userId, ARGV1 = (RedisValue)nickname, ARGV2 = (RedisValue)roomId
            };

            var prepared = LuaScript.Prepare(SCRIPT_ROOM_ENTER);
            //var redisResult = _db.ScriptEvaluate(prepared, test);
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

    public async Task<(ErrorCode, String?)> LeaveRoom(Int64 userId, String nickname)
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
            else if (redisResult.Type == ResultType.BulkString)
            {
                return (ErrorCode.RoomLeaveSuccess, (String)redisResult);
            }
            return (ErrorCode.RoomDbError, null);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { func = "EnterRoom", userId = userId }, "SetScripts EXCEPTION");
            return (ErrorCode.RoomDbError, null);
        }
    }


    public async Task<(ErrorCode, String?)> SetUserReady(Int64 userId, String nickname, Team team)
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
            else if (redisResult.Type == ResultType.BulkString)
            {
                return (ErrorCode.None, (String)redisResult);
            }
            return (ErrorCode.RoomDbError, null);
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { func = "EnterRoom", userId = userId }, "SetUserReady EXCEPTION");
            return (ErrorCode.RoomDbError, null);
        }
    }

    public async Task<(ErrorCode, String?)> SetUserUnready(Int64 userId, String nickname)
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
            redisResult = await _loadedUnready.EvaluateAsync(_db, test);

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
            else if (redisResult.Type == ResultType.BulkString)
            {
                return (ErrorCode.None, (String)redisResult);
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
