using APIServer.Controllers.ReqResModels;
using APIServer.Service;
using APIServer.Service.GameDataDb;
using APIServer.Service.Session;
using APIServer.Service.Session.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        readonly IGameDbServcie _gameDb;
        readonly ISessionService _sessionService;
        readonly ILogger<LoginController> _logger;
        public LoginController(IGameDbServcie gameDb, ISessionService sessionService, ILogger<LoginController> logger)
        {
            _gameDb = gameDb;
            _sessionService = sessionService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<LoginResponse> Login(LoginRequest request)
        {
            // DB와 연결하여, 로그인 정보 읽어옴
            LoginResponse response = new LoginResponse();
            var (errorCode, account) = await _gameDb.ReadAccount(request.userAssignedId);
            if (errorCode != ErrorCode.None || account == null)
            {
                response.errorCode = errorCode;
                return response;
            }

            // 비밀번호 검증
            if (Security.VerifyHashedPassword(request.password, account.salt, account.hashed_password) == false)
            {
                response.errorCode = ErrorCode.WorngPassword;
                return response;
            }

            // 토큰 생성 및 세션 저장
            response.token = Security.GenerateToken();
            response.errorCode = await _sessionService.SetSession(new SessionModel
            {
                userId = account.user_id,
                userAssignedId = account.user_assigned_id,
                nickname = account.nickname,
                token = response.token,
                status = UserStatus.Login
            });
            if (response.errorCode != ErrorCode.None)
            {
                response.token = "";
                return response;
            }

            return response;
        }
    }
}
