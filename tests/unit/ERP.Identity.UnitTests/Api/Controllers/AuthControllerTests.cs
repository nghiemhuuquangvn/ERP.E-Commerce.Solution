using Xunit;
using FluentAssertions;
using Moq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Services;

namespace ERP.Identity.UnitTests.Api.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService = new();
    private readonly AuthController _controller;
    private readonly DefaultHttpContext _httpContext = new();

    public AuthControllerTests()
    {
        _controller = new AuthController(_mockAuthService.Object, new Mock<ISettingsService>().Object);
        _httpContext.Request.Headers["X-Tenant-Code"] = "tenant001";
        _httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        _controller.ControllerContext = new ControllerContext { HttpContext = _httpContext };
    }

    [Fact]
    public async Task Register_WithValidRequest_ShouldReturnOk()
    {
        var request = new RegisterDto.RegisterRequest("test@example.com", null, "password123", "tenant001", UserType.Customer);
        _mockAuthService.Setup(s => s.RegisterAsync(It.IsAny<RegisterDto.RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginDto.LoginResponse("token", "refresh", true));

        var result = await _controller.Register(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOk()
    {
        var request = new LoginDto.LoginRequest("test@example.com", "password123", UserType.Staff);
        _mockAuthService.Setup(s => s.LoginAsync(It.IsAny<LoginDto.LoginRequest>(), "tenant001", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginDto.LoginResponse("token123", "refreshtoken123", true));

        var result = await _controller.Login(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        var request = new LoginDto.LoginRequest("test@example.com", "wrongpassword", UserType.Staff);
        _mockAuthService.Setup(s => s.LoginAsync(It.IsAny<LoginDto.LoginRequest>(), "tenant001", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginDto.LoginResponse("", "", false, new[] { "Invalid credentials" }));

        var result = await _controller.Login(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_UsesConnectionIp_NotClientSuppliedForwardedHeader()
    {
        // X-Forwarded-For is resolved into RemoteIpAddress by UseForwardedHeaders (trusted
        // proxies only); the controller itself must never read the raw header.
        _httpContext.Request.Headers["X-Forwarded-For"] = "10.9.9.9";
        _mockAuthService.Setup(s => s.LoginAsync(It.IsAny<LoginDto.LoginRequest>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginDto.LoginResponse("t", "r", true));

        await _controller.Login(new LoginDto.LoginRequest("a@b.test", "p", UserType.Customer), CancellationToken.None);

        _mockAuthService.Verify(s => s.LoginAsync(It.IsAny<LoginDto.LoginRequest>(), "tenant001", "203.0.113.7", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ShouldReturnOk()
    {
        var request = new ForgotPasswordRequest("test@example.com", "tenant001");
        _mockAuthService.Setup(s => s.ForgotPasswordAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForgotPasswordResponse(true));

        var result = await _controller.ForgotPassword(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ShouldReturnOk()
    {
        var request = new ResetPasswordRequest("reset_token_123", "newpassword123");
        _mockAuthService.Setup(s => s.ResetPasswordAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetPasswordResponse(true, null));

        var result = await _controller.ResetPassword(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ConfirmEmail_WithValidRequest_ShouldReturnOk()
    {
        var request = new ConfirmEmailRequest(Guid.NewGuid().ToString(), "confirm_token");
        _mockAuthService.Setup(s => s.ConfirmEmailAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmEmailResponse(true, null));

        var result = await _controller.ConfirmEmail(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Register_UsesTenantOfHost_NotTheBody()
    {
        _mockAuthService.Setup(s => s.RegisterAsync(It.IsAny<RegisterDto.RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginDto.LoginResponse("t", "r", true));

        await _controller.Register(new RegisterDto.RegisterRequest("a@b.test", null, "p", "OTHER-TENANT", UserType.Customer), CancellationToken.None);

        _mockAuthService.Verify(s => s.RegisterAsync(It.Is<RegisterDto.RegisterRequest>(r => r.TenantCode == "tenant001"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignupTenant_OutsidePlatformHost_IsForbidden()
    {
        var result = await _controller.SignupTenant(new TenantSignupDto.TenantSignupRequest("X", "X", "a@b.test", "p"), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        _mockAuthService.Verify(s => s.SignupTenantAsync(It.IsAny<TenantSignupDto.TenantSignupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_WithoutHostTenant_IsRejected()
    {
        var result = await _controller.RefreshToken(new RefreshTokenDto.RefreshTokenRequest("token"), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
