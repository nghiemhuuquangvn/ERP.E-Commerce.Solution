using System.Security.Claims;
using ERP.Identity.Api.Controllers;
using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Application.Services;

/// <summary>S3: SEC-M6 (BR-BF-08) and the lock policy ranges (BR-BF-06).</summary>
public class TwoFactorAndLockPolicyTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISystemAuthSettingsRepository> _system = new();
    private readonly Mock<ITenantAuthSettingsRepository> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public TwoFactorAndLockPolicyTests()
    {
        _uow.Setup(u => u.SystemAuthSettings).Returns(_system.Object);
        _uow.Setup(u => u.TenantAuthSettings).Returns(_tenant.Object);
        _uow.Setup(u => u.OutboxMessages).Returns(new Mock<IOutboxMessageRepository>().Object);
        _system.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SystemAuthSettings.CreateDefault());
        _tenant.Setup(t => t.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(TenantAuthSettings.CreateDefault(_tenantId));
    }

    private SettingsService Service() => new(_uow.Object, new Mock<ICacheService>().Object);

    private static TenantAuthSettings TenantWith(bool twoFactorEnabled, bool required)
    {
        var t = TenantAuthSettings.CreateDefault(Guid.NewGuid());
        t.Update(true, true, true, true, twoFactorEnabled, required, 8, true, true, true, true, 5, 15, 15);
        return t;
    }

    [Fact]
    public void AUsersOwnTwoFactor_SurvivesTheShopSwitchingTwoFactorOff()
    {
        var user = User.Create("a@b.test", "a", "hash", Guid.NewGuid(), UserType.Staff);
        user.EnableTwoFactor("Email");

        Service().IsTwoFactorRequiredForUser(user, SystemAuthSettings.CreateDefault(), TenantWith(twoFactorEnabled: false, required: false))
            .Should().BeTrue("a shop may require 2FA, never remove what a person turned on (SEC-M6)");
    }

    [Fact]
    public void TheShopRequirement_StillAppliesToEveryone_AndThePlatformSwitchIsTheEmergencyOff()
    {
        var user = User.Create("a@b.test", "a", "hash", Guid.NewGuid(), UserType.Staff);

        Service().IsTwoFactorRequiredForUser(user, SystemAuthSettings.CreateDefault(), TenantWith(true, required: true)).Should().BeTrue();
        Service().IsTwoFactorRequiredForUser(user, SystemAuthSettings.CreateDefault(), TenantWith(true, required: false)).Should().BeFalse();
    }

    private static UpdateTenantAuthSettingsRequest Request(int perDevice = 5, int threshold = 20, int window = 60) =>
        new(true, true, true, true, true, false, 8, true, true, true, true, perDevice, 15, 15, 12, 10080, 30, threshold, window);

    [Theory]
    [InlineData(5, 5, 60)]    // threshold not above the per-device limit: one device would lock the account
    [InlineData(5, 30, 60)]   // above the platform threshold (20)
    [InlineData(5, 20, 30)]   // shorter window than the platform (60)
    [InlineData(2, 20, 60)]   // per-device limit below the range
    public async Task ALockPolicyThatIsWeakOrUnsafe_IsRefused(int perDevice, int threshold, int window)
    {
        var act = () => Service().UpdateTenantSettingsAsync(_tenantId, Request(perDevice, threshold, window), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task AShopMayLockSoonerAndLonger()
    {
        var result = await Service().UpdateTenantSettingsAsync(_tenantId, Request(perDevice: 4, threshold: 10, window: 120), CancellationToken.None);

        result.AccountLockThreshold.Should().Be(10);
        result.AccountLockWindowMinutes.Should().Be(120);
    }

    [Fact]
    public async Task SavingAuthSettings_ReConfirmsTheCaller_BeforeAnythingIsChanged()
    {
        var settings = new Mock<ISettingsService>();
        var users = new Mock<IUserService>();
        var callerId = Guid.NewGuid();
        users.Setup(u => u.ConfirmCallerAsync(callerId, "AuthSettingsChange", null, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("ReauthenticationRequired", "Enter your current password."));
        var controller = new SettingsController(settings.Object, new Mock<IPermissionService>().Object, users.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", callerId.ToString()), new Claim(ClaimTypes.NameIdentifier, callerId.ToString())], "test")),
                },
            },
        };

        var act = () => controller.UpdateTenantSettings(_tenantId, Request(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        settings.Verify(s => s.UpdateTenantSettingsAsync(It.IsAny<Guid>(), It.IsAny<UpdateTenantAuthSettingsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
