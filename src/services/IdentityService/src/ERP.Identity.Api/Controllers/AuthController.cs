using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Services;
using Microsoft.AspNetCore.Identity.Data;
using LoginRequest = ERP.Identity.Application.DTOs.Auth.LoginDto.LoginRequest;
using RegisterRequest = ERP.Identity.Application.DTOs.Auth.RegisterDto.RegisterRequest;
using RefreshTokenRequest = ERP.Identity.Application.DTOs.Auth.RefreshTokenDto.RefreshTokenRequest;
using static ERP.Identity.Application.DTOs.Auth.RevokeDto;

namespace ERP.Identity.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
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
            var result = await _authService.LoginAsync(request, cancellationToken);
            if (!result.Succeeded) return Unauthorized(result.Errors);
            return Ok(result);
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
    }
}