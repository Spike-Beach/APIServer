//using CloudStructures;
//using CloudStructures.Structures;
using Microsoft.AspNetCore.Hosting.Server;
using StackExchange.Redis;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using ZLogger;

namespace APIServer.Service.Room;

public class RedisRoomDbService : IRoomDbService
{
    readonly Int16 MAX_ROOM_NUM = 50;
    readonly Int16 MAX_ROOM_USER_NUM = 4;
    //readonly RedisConnection _redisConnection;
    readonly ILogger<RedisRoomDbService> _logger;
    readonly IDatabase _db;
    readonly StackExchange.Redis.IServer _server;

    readonly String SCRIPT_ROOM_LIST_UP = "local result = {}; for i=0, 50 do local title = redis.call('GET', 'room:' .. i .. ':title'); if title then local users = redis.call('ZCARD', 'room:' .. i .. ':users'); table.insert(result, i .. ':' .. title .. '-' .. users); end; end; return result;";
    readonly String SCRIPT_ROOM_ENTER = "if redis.call('EXISTS', @KEY0) ~= 0 then return 103 end local title = redis.call('GET', @KEY1) if title == nil then return 101 end local data = redis.call('ZRANGE', @KEY2, 0, -1, 'WITHSCORES') local users = {} for i = 1, #data, 2 do local score = data[i + 1] local member = data[i] table.insert(users, score .. ':' .. member) end if #users >= 4 then return 102 end redis.call('SET', @KEY0, @ARGV1) redis.call('ZADD', @KEY2, @ARGV0, @ARGV2) local readys = redis.call('SMEMBERS', @KEY3) return title .. '\\n' .. table.concat(users, ' ') .. '\\n' .. table.concat(readys, ' ')";    //readonly String SCRIPT_ROOM_LEAVE= @"redis.call('DEL', 'user:@userId:room') 
    readonly String SCRIPT_ROOM_LEAVE = @"redis.call('DEL', 'user:{0}:room') redis.call('ZREM', 'room:{1}:users', '{2}') redis.call('SREM', 'room:{1}:readys', '{0}') return ";
    //@"if redis.call('EXISTS', 'user:@userId:room') ~= 0 then return 103 end 
    //if redis.call('EXISTS', 'room:@roomId:title') == 0 then return 101 end 
    //local usersNum = redis.call('ZCARD', 'room:@roomId:users') 
    //if usersNum >= 4 then return 102 end 
    //redis.call('SET', 'user:@userId:room', '@roomId') 
    //redis.call('ZADD', 'room:@roomId:users', '@userId', '@nickname') return 0";
    LoadedLuaScript _lodedRoomListUp;
    LoadedLuaScript _lodedRoomEnter;
    LoadedLuaScript _lodedRoomLeave;

    // 스크립트 필요 방 입장, 방리스트받아오기, 레디 하기
    //readonly String SCRIPT_ROOM_ENTER = @"";
    //readonly String SCRIPT_ROOM_LIST_UP = @"local result = {}; 
    //    for i=0,@userId do local title = redis.call('GET', 'room:' .. i .. ':title'); 
    //    if title then local users = redis.call('ZCARD', 'room:' .. i .. ':users'); 
    //    table.insert(result, i .. ':' .. title .. '-' .. users); end; end; return result;";

    public RedisRoomDbService(IConfiguration config, ILogger<RedisRoomDbService> logger)
    {
        _logger = logger;
        var connectionString = config.GetConnectionString("Redis_Room");
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(connectionString);
        _db = redis.GetDatabase();
        _server = redis.GetServer(connectionString);
        var redisConfig = new CloudStructures.RedisConfig("room", connectionString);
        //_redisConnection = new RedisConnection(redisConfig);
    }

    // 초기화 작업이라 async할 이유 없음.
    public ErrorCode SetScripts()
    {
        try
        {
            var prepared = LuaScript.Prepare(SCRIPT_ROOM_LIST_UP);
            _lodedRoomListUp = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_ROOM_ENTER);
            _lodedRoomEnter = prepared.Load(_server);
            prepared = LuaScript.Prepare(SCRIPT_ROOM_LEAVE);
            _lodedRoomLeave = prepared.Load(_server);
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
        // 우선 자신이 방에 포함되어 있는지 확인 ->  별도의 저장공간에 저장. SET user:1:room 0
        // 방에 인원이 다 꽉차 있는지 확인한다. 현재는 4명까지만.
        // 이후 입장
        try
        {
            //var prepared = LuaScript.Prepare(String.Format(SCRIPT_ROOM_ENTER, userId, roomId, nickname));
            //var redisResult = _db.ScriptEvaluate(prepared);
            //RedisKey[] keys = { $"user:{userId}:room", $"room:{roomId}:title", $"room:{roomId}:users", $"room:{roomId}:readys" };
            //RedisValue[] argvs = { userId, roomId, nickname };
            //RedisKey[] keys = { $"user:{userId}:room", $"room:{roomId}:title", $"room:{roomId}:users", $"room:{roomId}:readys" };
            //RedisValue[] argvs = { userId, roomId, nickname };
            Object test = new { 
                KEY0 = (RedisKey)$"user:{userId}:room", 
                KEY1 = (RedisKey)$"room:{roomId}:title", 
                KEY2 = (RedisKey)$"room:{roomId}:users", 
                KEY3 = (RedisKey)$"room:{roomId}:readys", 
                ARGV0 = (RedisValue)userId, ARGV1 = (RedisValue)roomId, ARGV2 = (RedisValue)nickname  
            };
            //var redisResult = await _lodedRoomEnter.EvaluateAsync(_db, keys, argvs);
            //var redisResult = await _db.ScriptEvaluateAsync(_, keys, argvs);
            var redisResult = await _lodedRoomEnter.EvaluateAsync(_db, test);
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
            var redisResult = await _lodedRoomListUp.EvaluateAsync(_db);
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

    public async Task<ErrorCode> LeaveRoom(short roomId, short userId, String nickname)
    {
        try
        {
            //var prepared = LuaScript.Prepare(String.Format(SCRIPT_ROOM_LEAVE, userId, roomId, nickname));
            //var redisResult = _db.ScriptEvaluate(prepared);
            var redisResult = _lodedRoomLeave.EvaluateAsync(_db);
            if (redisResult == null)
            {
                _logger.ZLogErrorWithPayload(new { func = "LeaveRoom", userId = userId }, "LeaveRoom return null");
                return ErrorCode.RoomDbError;
            }
            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogErrorWithPayload(ex, new { func = "EnterRoom", userId = userId }, "SetScripts EXCEPTION");
            return ErrorCode.RoomDbError;
        }
    }


    public Task<ErrorCode> SetUserReady(short roomId, short userId)
    {
        throw new NotImplementedException();
    }
}
