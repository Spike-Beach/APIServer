using APIServer.Controllers.ReqResModels;
using APIServer.Service.GameDataDb;
using APIServer.Service.Session.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UserInfoController : ControllerBase
    {
        readonly IGameDbServcie _gameDbServcie;
        readonly ILogger<UserInfoController> _logger;
        readonly SessionModel _session;

        public UserInfoController(IHttpContextAccessor httpContextAccessor, IGameDbServcie gameDbServcie, ILogger<UserInfoController> logger)
        {
            _gameDbServcie = gameDbServcie;
            _logger = logger;

            _session = httpContextAccessor.HttpContext.Items["session"] as SessionModel;
        }

        public async Task<UserInfoResponse> UserInfo(UserInfoRequest request)
        {
            UserInfoResponse response = new UserInfoResponse();
            (response.errorCode, var userInfo) = await _gameDbServcie.ReadUserInfo(_session.userId);
            if (response.errorCode != ErrorCode.None || userInfo == null)
            {
                return response;
            }

            response.winCnt = userInfo.win_cnt;
            response.loseCnt = userInfo.lose_cnt;
            return response;
        }
    }
}
