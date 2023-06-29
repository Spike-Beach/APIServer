using APIServer.Controllers.ReqResModels;
using APIServer.Service;
using APIServer.Service.GameDataDb;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace APIServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class RegisterController : ControllerBase
    {
        readonly IGameDbServcie _gameDb;
        readonly ILogger<RegisterController> _logger;
        public RegisterController(IGameDbServcie gameDb, ILogger<RegisterController> logger)
        {
            _gameDb = gameDb;
            _logger = logger;
        }

        [HttpPost]
        public async Task<RegisterResponse> Registration(RegisterRequest request)
        {
            RegisterResponse response = new RegisterResponse();

            var (saltBytes, hashedPasswordBytes) = Security.GetSaltAndHashedPassword(request.password);
            (response.errorCode, var userId) = await _gameDb.RegistAccount(request.userAssignedId, request.nickname, saltBytes, hashedPasswordBytes);
            if (response.errorCode != ErrorCode.None)
            {
                return response;
            }

            response.errorCode = await _gameDb.RegistUserInfo(userId);
            if (response.errorCode != ErrorCode.None) 
            {
                return response;
            }

            _logger.ZLogInformationWithPayload(new { userId = userId }, "User Regist SUCCESS");
            return response;
        }
    }
}
