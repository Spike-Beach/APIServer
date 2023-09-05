using APIServer;
using APIServer.Service.Session;
using APIServer.Service.Session.Model;
using System.Text;
using System.Text.Json;
using ZLogger;

namespace DungeonFarming.Middleware;

public class AuthCheckMiddleware
{
    readonly RequestDelegate _next;
    readonly ISessionService _sessionDb;
    readonly ILogger<AuthCheckMiddleware> _logger;

    public AuthCheckMiddleware(RequestDelegate next, ISessionService sessionService, ILogger<AuthCheckMiddleware> logger)
    {
        _next = next;
        _sessionDb = sessionService;
        _logger = logger;
    }
    public async Task Invoke(HttpContext context)
    {
        String path = context.Request.Path;
        if (path.StartsWith("/Register") || path.StartsWith("/Login") || path.Contains("/ws"))
        {
            await _next(context);
            return;
        }

        // http 파싱
        context.Request.EnableBuffering();
        using var streamReader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true);
        var requestBody = await streamReader.ReadToEndAsync();
        if (String.IsNullOrEmpty(requestBody))
        {
            _logger.ZLogWarningWithPayload(new { Path = context.Request.Path }, "Http Body NULLorEMPTY");
            await SetContext(context, 400, ErrorCode.InvalidBodyForm);
            return ;
        }

        // 토큰 추출
        var (userAssignedId, requestToken) = await GetIdToken(context, requestBody);
        if (userAssignedId == null || requestToken == null)
        {
            return;
        }

        // 세션 가져와서 토큰 검증
        var userSession = await GetUserSession(userAssignedId);
        if (userSession == null)
        {
            return;
        }
        if (userSession.token != requestToken)
        {
            await SetContext(context, 400, ErrorCode.InvalidToken);
            return ;
        }

        // 세션 정보 컨택스트에 저장
        context.Items["session"] = userSession;
        context.Request.Body.Position = 0;
        
        await _next(context);
    }

    private async Task<(String?, String?)> GetIdToken(HttpContext context, String requestBody)
    {
        try
        {
            // json에서 파싱 후 추출
            var doc = JsonDocument.Parse(requestBody);
            if (doc == null)
            {
                _logger.ZLogWarningWithPayload( new { Path = context.Request.Path }, "Http Body UNPARSINGABLE");
                await SetContext(context, 400, ErrorCode.InvalidBodyForm);
                return (null, null);
            }
            else if (doc.RootElement.TryGetProperty("userAssignedId", out var id))
            {
                if (doc.RootElement.TryGetProperty("token", out var token))
                {
                    return (id.GetString(), token.GetString());
                }
                _logger.ZLogWarningWithPayload(new { Path = context.Request.Path }, "Http token not include");
                return (null, null);
            }
            _logger.ZLogWarningWithPayload(new { Path = context.Request.Path }, "Http userAssignedId not include");
            return (null, null);
        }
        catch (Exception ex) 
        {
            _logger.ZLogWarningWithPayload(ex, new { Path = context.Request.Path }, "middleware json doc exception ");
            await SetContext(context, 500, ErrorCode.ServerError);
            return (null, null);
        }
    }

    async Task<SessionModel?> GetUserSession(String userAssignedId)
    {
        var (errorCode, userInfo) = await _sessionDb.GetSession(userAssignedId);
        if (errorCode != ErrorCode.None || userInfo == null)
        {
            return null;
        }
        return userInfo;
    }

    private async Task SetContext(HttpContext context, Int32 statusCode, ErrorCode errorCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var responseContent = new { error_code = errorCode };
        await context.Response.WriteAsJsonAsync(responseContent);
    }

}
