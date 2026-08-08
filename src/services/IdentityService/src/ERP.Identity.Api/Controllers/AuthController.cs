using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ERP.Identity.Api.Authorization;
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
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
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
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.RegisterAsync(request, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result); // LoginResponse: { accessToken, refreshToken, user, succeeded }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _authService.LoginAsync(request, tenantCode!, GetClientIp(), Request.Headers.UserAgent.ToString(), cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        /// <summary>Mirrors the X-Forwarded-For-first resolution the refresh endpoint already used, shared here for login/2FA/logout audit logging.</summary>
        private string GetClientIp() =>
            Request.Headers.ContainsKey("X-Forwarded-For")
                ? Request.Headers["X-Forwarded-For"].ToString()
                : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        /// <summary>
        /// Reads the tenant code the Gateway resolved from the request's subdomain
        /// (see GatewayAuthorizationMiddleware.ResolveTenantCodeFromHost) — replaces the old
        /// manual "Tenant Code" login field. Missing header means the request didn't come
        /// through a recognizable tenant subdomain (e.g. hit the bare base domain directly),
        /// which is a hard error here rather than a silent fallback to some default tenant.
        /// </summary>
        private bool TryGetTenantCode(out string? tenantCode, out IActionResult? error)
        {
            tenantCode = Request.Headers["X-Tenant-Code"].ToString();
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                error = BadRequest(new { message = "Unable to resolve tenant from request host." });
                return false;
            }
            error = null;
            return true;
        }

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
            var callerUserId = this.GetCallerUserId();
            if (callerUserId is null)
                return Unauthorized();

            var jti = User.FindFirst("jti")?.Value;
            DateTime? tokenExpiresAt = long.TryParse(User.FindFirst("exp")?.Value, out var expUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime
                : null;

            await _authService.LogoutAsync(callerUserId.Value, request.RefreshToken, jti, tokenExpiresAt, GetClientIp(), cancellationToken);
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ForgotPasswordAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
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
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginRequest request, CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _authService.ExternalLoginAsync(request, tenantCode!, cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        [HttpGet("external-login/twitter/authorize-url")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTwitterAuthorizeUrl(CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _authService.GetTwitterAuthorizeUrlAsync(tenantCode!, cancellationToken);
            return Ok(result);
        }

        [HttpPost("external-login/twitter/callback")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> TwitterCallback([FromBody] TwitterCallbackRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.TwitterCallbackAsync(request, cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
        }

        [HttpPost("2fa/verify")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-2fa-verify")]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.VerifyTwoFactorAsync(request, GetClientIp(), Request.Headers.UserAgent.ToString(), cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpPost("2fa/resend")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-sensitive")]
        public async Task<IActionResult> ResendTwoFactor([FromBody] ResendTwoFactorRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _authService.ResendTwoFactorAsync(request, cancellationToken);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result);
        }

        [HttpGet("login-options")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLoginOptions(CancellationToken cancellationToken = default)
        {
            if (!TryGetTenantCode(out var tenantCode, out var error)) return error!;
            var result = await _settingsService.GetPublicOptionsAsync(tenantCode!, cancellationToken);
            return Ok(result);
        }
    }
}