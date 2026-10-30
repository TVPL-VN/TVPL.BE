using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Viora.Application.Accounts;
using Viora.Infrastructure.Security;

namespace viora_BE.Controllers;

[ApiController]
[Route("api/auth/ankt")]
public sealed class AnktAuthController(IAnktLoginService login, IOptions<AnktMiniAppOptions> options) : ControllerBase
{
    [HttpPost("exchange")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Exchange(AnktExchangeRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var config = options.Value;
        if (!config.Enabled) return StatusCode(503, new { code = "ANKT_NOT_CONFIGURED", message = "Đăng nhập ANKT chưa được cấu hình." });
        // JSON + exact Origin prevents login CSRF. Host is the embedder; browser
        // requests originate from the verified Mini App website, not the ANKT host.
        if (Request.Headers.Origin.Count != 1 || Request.Headers.Origin[0] != config.MiniAppOrigin)
            return StatusCode(403, new { code = "ANKT_INVALID_ORIGIN", message = "Website Mini App không hợp lệ." });
        try
        {
            var result = await login.ExchangeAsync(request.Code, cancellationToken);
            var tokens = result.Tokens!;
            // Match existing website refresh transport. Browsers never receive
            // the refresh token in JSON; it is scoped to existing account routes.
            Response.Cookies.Append("refreshToken", tokens.RefreshToken, new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.None,
                Path = "/api/accounts", Expires = new DateTimeOffset(tokens.RefreshTokenExpiresAt)
            });
            return Ok(new LoginSuccessResponse(result.Status!.Value, tokens.AccessToken, null,
                tokens.AccessTokenExpiresAt, tokens.RefreshTokenExpiresAt, tokens.SessionId, result.User));
        }
        catch (AnktAuthenticationException error)
        {
            return StatusCode(error.Unavailable ? 503 : 401, new
            {
                code = error.Code,
                message = "Không thể đăng nhập ANKT. Hãy thử lại để lấy mã đăng nhập mới."
            });
        }
    }

    // Auth middleware checks upstream grant before this response is returned.
    [HttpGet("session")]
    [Authorize]
    public IActionResult Session()
    {
        Response.Headers.CacheControl = "no-store";
        return User.HasClaim(x => x.Type == "ankt_session")
            ? Ok(new { authenticated = true })
            : Unauthorized();
    }
}

public sealed record AnktExchangeRequest([param: Required, MaxLength(512)] string Code);
