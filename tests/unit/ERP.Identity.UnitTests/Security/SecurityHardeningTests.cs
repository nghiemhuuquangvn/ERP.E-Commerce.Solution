using ERP.Shared.Security;
using Xunit;
using FluentAssertions;
using Moq;
using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Auth.Validators;
using ERP.Identity.Application.DTOs.Webhook;
using ERP.Identity.Application.DTOs.Webhook.Validators;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Shared.Localization;
using static ERP.Identity.Application.DTOs.Auth.RegisterDto;

namespace ERP.Identity.UnitTests.Security;

/// <summary>Regression tests for the 2026-10-19 IdentityService security hardening.</summary>
public class SecurityHardeningTests
{
    // ---- H5: webhook SSRF policy ----

    [Theory]
    [InlineData("http://hooks.example.com/x")]          // plain http
    [InlineData("https://localhost/x")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://169.254.169.254/latest/meta-data")] // cloud metadata
    [InlineData("https://10.0.0.5/x")]
    [InlineData("https://172.20.0.3/x")]
    [InlineData("https://192.168.1.1/x")]
    [InlineData("https://[::1]/x")]
    [InlineData("https://sqlserver/x")]                 // docker service name
    [InlineData("https://identityservice:5001/x")]
    [InlineData("https://printer.local/x")]
    [InlineData("https://user:pass@hooks.example.com/x")]
    [InlineData("not a url")]
    public void WebhookUrlRules_RejectsInternalOrUnsafeDestinations(string url) =>
        WebhookUrlRules.IsAllowedUrl(url).Should().BeFalse();

    [Theory]
    [InlineData("https://hooks.example.com/erp")]
    [InlineData("https://webhook.site/abc-123")]
    [InlineData("https://8.8.8.8/x")]
    public void WebhookUrlRules_AllowsPublicHttpsDestinations(string url) =>
        WebhookUrlRules.IsAllowedUrl(url).Should().BeTrue();

    [Theory]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fd00::1")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    public void WebhookUrlRules_BlocksResolvedInternalAddresses(string ip) =>
        WebhookUrlRules.IsBlockedAddress(IPAddress.Parse(ip)).Should().BeTrue();

    [Fact]
    public void CreateWebhookValidator_RejectsInternalUrl()
    {
        var result = new CreateWebhookEndpointRequestValidator().Validate(
            new CreateWebhookEndpointRequest("hook", "http://sqlserver:1433", null, new List<string>()));

        result.IsValid.Should().BeFalse();
    }

    // ---- C1: validator rejects privileged self-registration ----

    [Theory]
    [InlineData(UserType.Staff)]
    [InlineData(UserType.Admin)]
    [InlineData(UserType.SuperAdmin)]
    [InlineData(UserType.System)]
    public void RegisterValidator_RejectsNonCustomerUserType(UserType userType)
    {
        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns(new LocalizedString("k", "v"));

        var result = new RegisterRequestValidator(localizer.Object).Validate(
            new RegisterRequest("a@b.test", null, "Str0ng-Passw0rd!", "ACME", userType));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.UserType));
    }

    // ---- H2: OIDC login pages refuse open redirects ----

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example/phish")]
    [InlineData("/api/v1/users")]
    [InlineData("")]
    public void OidcLogin_RejectsNonAuthorizeReturnUrl(string returnUrl)
    {
        var result = CreateOidcController().Login(returnUrl);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void OidcLogin_AcceptsAuthorizeReturnUrl()
    {
        var result = CreateOidcController().Login("/connect/authorize?client_id=app&response_type=code");

        result.Should().BeOfType<ContentResult>()
            .Which.Content.Should().Contain("__RequestVerificationToken");
    }

    private static OidcAuthorizationController CreateOidcController()
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>()))
            .Returns(new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", "X-CSRF"));

        var controller = new OidcAuthorizationController(
            new Mock<IUnitOfWork>().Object,
            new Mock<IAuthService>().Object,
            new Mock<ILicenseService>().Object,
            null!, // AuthDbContext — not touched by the login GET
            antiforgery.Object);

        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor());
        controller.ControllerContext = new ControllerContext(actionContext);
        controller.Url = new UrlHelper(actionContext);
        return controller;
    }
}
