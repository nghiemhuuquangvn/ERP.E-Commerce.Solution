using System.Net;
using System.Security.Claims;
using ERP.Identity.Api.Authorization;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Api.Controllers;

/// <summary>S2 / SEC-H3: web apps get the refresh token as an HttpOnly cookie, never in the body.</summary>
public class RefreshTokenCookieTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private readonly Mock<IAuthService> _auth = new();

    private (AuthController Controller, DefaultHttpContext Http) Controller(bool cookieMode, string? cookie = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Code"] = "ACME";
        http.Request.Headers["X-Tenant-Id"] = TenantId.ToString();
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        if (cookieMode) http.Request.Headers[RefreshTokenCookie.TransportHeader] = RefreshTokenCookie.CookieTransport;
        if (cookie is not null) http.Request.Headers.Cookie = $"{RefreshTokenCookie.CookieName}={cookie}";
        var controller = new AuthController(_auth.Object, new Mock<ISettingsService>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        return (controller, http);
    }

    private void LoginReturns(string refresh) =>
        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginDto.LoginRequest>(), "ACME", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginDto.LoginResponse("access", refresh, true, RefreshTokenExpiresAt: DateTime.UtcNow.AddMinutes(15)));

    [Fact]
    public async Task CookieMode_SetsAnHttpOnlyStrictCookie_AndLeavesTheBodyWithoutTheToken()
    {
        LoginReturns("secret-refresh");
        var (controller, http) = Controller(cookieMode: true);

        var result = (OkObjectResult)await controller.Login(new LoginDto.LoginRequest("a@b.test", "pw", UserType.Admin), CancellationToken.None);

        ((LoginDto.LoginResponse)result.Value!).RefreshToken.Should().BeNull();
        var setCookie = http.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain($"{RefreshTokenCookie.CookieName}=secret-refresh")
            .And.Contain("httponly").And.Contain("samesite=strict").And.Contain("secure").And.Contain("path=/api/v1/auth");
    }

    [Fact]
    public async Task BodyMode_ForMobileApps_IsUnchanged()
    {
        LoginReturns("secret-refresh");
        var (controller, http) = Controller(cookieMode: false);

        var result = (OkObjectResult)await controller.Login(new LoginDto.LoginRequest("a@b.test", "pw", UserType.Admin), CancellationToken.None);

        ((LoginDto.LoginResponse)result.Value!).RefreshToken.Should().Be("secret-refresh");
        http.Response.Headers.SetCookie.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_ReadsTheCookie_OnlyWithTheTransportHeader()
    {
        _auth.Setup(a => a.RefreshTokenAsync(It.IsAny<RefreshTokenDto.RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenDto.RefreshTokenResponse("access-2", "refresh-2"));

        var (withHeader, _) = Controller(cookieMode: true, cookie: "from-cookie");
        await withHeader.RefreshToken(new RefreshTokenDto.RefreshTokenRequest(null), CancellationToken.None);
        _auth.Verify(a => a.RefreshTokenAsync(It.Is<RefreshTokenDto.RefreshTokenRequest>(r => r.RefreshToken == "from-cookie"), It.IsAny<CancellationToken>()), Times.Once);

        // CSRF guard: a cross-site request carries the cookie but cannot add the custom header.
        var (withoutHeader, _) = Controller(cookieMode: false, cookie: "from-cookie");
        await withoutHeader.RefreshToken(new RefreshTokenDto.RefreshTokenRequest(null), CancellationToken.None);
        _auth.Verify(a => a.RefreshTokenAsync(It.Is<RefreshTokenDto.RefreshTokenRequest>(r => r.RefreshToken == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_RevokesTheCookieToken_AndDeletesTheCookie()
    {
        var userId = Guid.NewGuid();
        var (controller, http) = Controller(cookieMode: true, cookie: "from-cookie");
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));

        await controller.Logout(new RefreshTokenDto.RefreshTokenRequest(null), CancellationToken.None);

        _auth.Verify(a => a.LogoutAsync(userId, "from-cookie", It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        http.Response.Headers.SetCookie.ToString().Should().Contain($"{RefreshTokenCookie.CookieName}=;").And.Contain("expires=");
    }
}
