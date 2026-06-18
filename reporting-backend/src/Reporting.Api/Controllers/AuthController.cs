using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Reporting.Application.Auth;

namespace Reporting.Api.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
        => FromResult(await _auth.LoginAsync(request, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
        => FromResult(await _auth.RefreshAsync(request, ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
        => FromResult(await _auth.LogoutAsync(request.RefreshToken, ct));

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
        => FromResult(await _auth.GetMeAsync(CurrentUserId, ct));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
        => FromResult(await _auth.ChangePasswordAsync(CurrentUserId, request, ct));

    [HttpPost("change-email")]
    [Authorize]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request, CancellationToken ct)
        => FromResult(await _auth.ChangeEmailAsync(CurrentUserId, request, ct));
}
