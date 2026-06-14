using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Services;

namespace ERP.Identity.UnitTests.Api.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _controller = new AuthController(_mockAuthService.Object);
    }

    [Fact]
    public async Task Register_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = new RegisterDto.RegisterRequest("test@example.com", "password123", "Test User", "tenant001");
        var response = new RegisterDto.RegisterResponse(true, null);

        _mockAuthService.Setup(s => s.RegisterAsync(It.IsAny<RegisterDto.RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.Register(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult?.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOk()
    {
        // Arrange
        var request = new LoginDto.LoginRequest("test@example.com", "password123", "tenant001", UserType.Staff);
        var response = new LoginDto.LoginResponse("token123", "refreshtoken123", true);

        _mockAuthService.Setup(s => s.LoginAsync(It.IsAny<LoginDto.LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new LoginDto.LoginRequest("test@example.com", "wrongpassword", "tenant001", UserType.Staff);
        var response = new LoginDto.LoginResponse("", "", false, new[] { "Invalid credentials" });

        _mockAuthService.Setup(s => s.LoginAsync(It.IsAny<LoginDto.LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ShouldReturnOk()
    {
        // Arrange
        var request = new ForgotPasswordRequest("test@example.com", "tenant001");
        var response = new ForgotPasswordResponse(true, "reset_token_123");

        _mockAuthService.Setup(s => s.ForgotPasswordAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.ForgotPassword(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ShouldReturnOk()
    {
        // Arrange
        var request = new ResetPasswordRequest("reset_token_123", "newpassword123");
        var response = new ResetPasswordResponse(true, null);

        _mockAuthService.Setup(s => s.ResetPasswordAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.ResetPassword(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ConfirmEmail_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = new ConfirmEmailRequest(Guid.NewGuid().ToString(), "confirm_token");
        var response = new ConfirmEmailResponse(true, null);

        _mockAuthService.Setup(s => s.ConfirmEmailAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.ConfirmEmail(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
