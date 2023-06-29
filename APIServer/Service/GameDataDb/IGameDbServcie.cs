using APIServer.Service.GameDataDb.Models;

namespace APIServer.Service.GameDataDb;

public interface IGameDbServcie
{
    Task<(ErrorCode, Int64)> RegistAccount(String userAssignedId, String nickname, byte[] salt, byte[] hashed_password);
    Task<(ErrorCode, Account?)> ReadAccount(String userAssignedId);
    Task<ErrorCode> RegistUserInfo(Int64 userId);

    Task<(ErrorCode, UserInfo?)> ReadUserInfo(Int64 userId);
}
