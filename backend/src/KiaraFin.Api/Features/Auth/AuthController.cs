using KiaraFin.Api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuthService authService,
    ICurrentUser currentUser,
    IRequestOriginValidator originValidator,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "kiarafin_refresh_token";

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Cadastro desativado",
            detail: "O cadastro de novos usuários está permanentemente desativado.");
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.LoginAsync(request, cancellationToken);
        SetRefreshCookie(session);
        return Ok(session.Response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        originValidator.EnsureAllowed(Request);
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
            return Unauthorized();
        var session = await authService.RefreshAsync(refreshToken, cancellationToken);
        SetRefreshCookie(session);
        return Ok(session.Response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        originValidator.EnsureAllowed(Request);
        Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        await authService.LogoutAsync(refreshToken, cancellationToken);
        Response.Cookies.Delete(RefreshCookieName, CookieOptions());
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetCurrentUserAsync(currentUser.UserId, cancellationToken));

    private void SetRefreshCookie(AuthSession session) =>
        Response.Cookies.Append(
            RefreshCookieName,
            session.RefreshToken,
            CookieOptions(session.RefreshTokenExpiresAtUtc));

    private CookieOptions CookieOptions(DateTime? expires = null) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Path = "/api/auth",
        Expires = expires
    };
}
