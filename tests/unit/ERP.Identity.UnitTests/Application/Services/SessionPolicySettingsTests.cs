using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Application.Services;

/// <summary>S2 / BR-SES-05: the session values of a tenant stay in range and at most the platform's.</summary>
public class SessionPolicySettingsTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISystemAuthSettingsRepository> _system = new();
    private readonly Mock<ITenantAuthSettingsRepository> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TenantAuthSettings _tenantRow;

    public SessionPolicySettingsTests()
    {
        _tenantRow = TenantAuthSettings.CreateDefault(_tenantId);
        _uow.Setup(u => u.SystemAuthSettings).Returns(_system.Object);
        _uow.Setup(u => u.TenantAuthSettings).Returns(_tenant.Object);
        _uow.Setup(u => u.OutboxMessages).Returns(new Mock<IOutboxMessageRepository>().Object);
        _system.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SystemAuthSettings.CreateDefault());
        _tenant.Setup(t => t.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(_tenantRow);
    }

    private SettingsService Service() => new(_uow.Object, new Mock<ICacheService>().Object);

    private static UpdateTenantAuthSettingsRequest Request(int idle = 15, int absoluteHours = 12, int customerIdle = 10080, int customerDays = 30) =>
        new(true, true, true, true, true, false, 8, true, true, true, true, 5, 15, idle, absoluteHours, customerIdle, customerDays);

    [Theory]
    [InlineData(30, 12, 10080, 30)]   // idle above the platform's 15
    [InlineData(15, 24, 10080, 30)]   // absolute above 12 h
    [InlineData(15, 12, 20000, 30)]   // customer idle above 7 days
    [InlineData(15, 12, 10080, 60)]   // customer absolute above 30 days
    [InlineData(4, 12, 10080, 30)]    // idle below 5 minutes
    public async Task ATenant_CannotLoosenOrBreakThePolicy(int idle, int absoluteHours, int customerIdle, int customerDays)
    {
        var act = () => Service().UpdateTenantSettingsAsync(_tenantId, Request(idle, absoluteHours, customerIdle, customerDays), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task ATenant_MayTightenThePolicy()
    {
        var result = await Service().UpdateTenantSettingsAsync(_tenantId, Request(idle: 10, absoluteHours: 8, customerIdle: 1440, customerDays: 7), CancellationToken.None);

        result.SessionTimeoutMinutes.Should().Be(10);
        result.SessionAbsoluteHours.Should().Be(8);
        result.CustomerSessionTimeoutMinutes.Should().Be(1440);
        result.CustomerSessionAbsoluteDays.Should().Be(7);
        _tenantRow.SessionAbsoluteHours.Should().Be(8);
    }
}
