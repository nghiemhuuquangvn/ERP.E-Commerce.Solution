using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Security;

/// <summary>S2 / SEC-M4 (BR-SES-09): "sign out this device" really ends it.</summary>
public class DeviceSessionRevokeTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IDeviceSessionRepository> _devices = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();

    public DeviceSessionRevokeTests()
    {
        _uow.Setup(u => u.DeviceSessions).Returns(_devices.Object);
        _uow.Setup(u => u.RefreshTokens).Returns(_tokens.Object);
    }

    [Fact]
    public async Task Revoke_EndsTheDevice_RevokesItsRefreshTokens_AndCommits()
    {
        var userId = Guid.NewGuid();
        var device = DeviceSession.Create(userId, Guid.NewGuid(), "phone-1", "Phone", null, null, null);
        _devices.Setup(d => d.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);

        await new SessionService(_uow.Object).RevokeSessionAsync(userId, device.Id, CancellationToken.None);

        device.IsActive.Should().BeFalse();
        _tokens.Verify(t => t.RevokeAllByDeviceSessionAsync(device.Id, It.IsAny<CancellationToken>()), Times.Once);
        // Found live 2026-11-17: nothing committed the revoke before, so the device kept refreshing.
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Touch_OfARevokedDevice_SaysNo()
    {
        var device = DeviceSession.Create(Guid.NewGuid(), Guid.NewGuid(), "phone-1", "Phone", null, null, null);
        device.Revoke();
        _devices.Setup(d => d.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);

        (await new SessionService(_uow.Object).TouchSessionAsync(device.Id, "1.2.3.4", CancellationToken.None)).Should().BeFalse();
    }
}
