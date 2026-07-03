using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Services;
using LoginRequest = ERP.Identity.Application.DTOs.Auth.LoginDto.LoginRequest;
using RegisterRequest = ERP.Identity.Application.DTOs.Auth.RegisterDto.RegisterRequest;
using RefreshTokenRequest = ERP.Identity.Application.DTOs.Auth.RefreshTokenDto.RefreshTokenRequest;
using ForgotPasswordRequestDto = ERP.Identity.Application.DTOs.Auth.ForgotPasswordRequest;
using ResetPasswordRequestDto = ERP.Identity.Application.DTOs.Auth.ResetPasswordRequest;
using ExternalLoginRequest = ERP.Identity.Application.DTOs.Auth.ExternalLoginDto.ExternalLoginRequest;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;

        public AuthController(IAuthService authService, ISettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.RegisterAsync(request, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.LoginAsync(request, GetClientIp(), Request.Headers.UserAgent.ToString(), cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        /// <summary>Mirrors the X-Forwarded-For-first resolution the refresh endpoint already used, shared here for login/2FA/logout audit logging.</summary>
        private string GetClientIp() =>
            Request.Headers.ContainsKey("X-Forwarded-For")
                ? Request.Headers["X-Forwarded-For"].ToString()
                : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken = default)
        {
            string IpAddress = Request.Headers.ContainsKey("X-Forwarded-For")
                ? Request.Headers["X-Forwarded-For"].ToString()
                : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var refreshTokenRequest = new RefreshTokenRequest(request.RefreshToken, request.TenantId, IpAddress);

            var result = await _authService.RefreshTokenAsync(refreshTokenRequest, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            var jti = User.FindFirst("jti")?.Value;
            DateTime? tokenExpiresAt = long.TryParse(User.FindFirst("exp")?.Value, out var expUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime
                : null;

            await _authService.LogoutAsync(parsedUserId, request.RefreshToken, jti, tokenExpiresAt, GetClientIp(), cancellationToken);
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ForgotPasswordAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ResetPasswordAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ConfirmEmailAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("external-login")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ExternalLoginAsync(request, cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        [HttpGet("external-login/twitter/authorize-url")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTwitterAuthorizeUrl([FromQuery] string tenantCode, CancellationToken cancellationToken = default)
        {
            var result = await _authService.GetTwitterAuthorizeUrlAsync(tenantCode, cancellationToken);
            return Ok(result);
        }

        [HttpPost("external-login/twitter/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> TwitterCallback([FromBody] TwitterCallbackRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.TwitterCallbackAsync(request, cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        [HttpPost("2fa/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.VerifyTwoFactorAsync(request, GetClientIp(), Request.Headers.UserAgent.ToString(), cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpPost("2fa/resend")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendTwoFactor([FromBody] ResendTwoFactorRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ResendTwoFactorAsync(request, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpGet("login-options")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLoginOptions([FromQuery] string tenantCode, CancellationToken cancellationToken = default)
        {
            var result = await _settingsService.GetPublicOptionsAsync(tenantCode, cancellationToken);
            return Ok(result);
        }
    }
}