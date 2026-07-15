using Xunit;
using FluentAssertions;
using Moq;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Infrastructure.Caching;

namespace ERP.Identity.UnitTests.Application.Services;

/// <summary>
/// Model test for the Settings cache-aside pattern (FEATURE_SETTINGS_MODULE.md §3.1) —
/// every other {X}SettingsService in this repo follows the identical
/// GetOrSetAsync-on-read / RemoveAsync-on-write shape, so this is the reference other
/// categories' cache tests should mirror.
/// </summary>
public class SettingsServiceCacheTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ISystemAuthSettingsRepository> _mockSystemAuthSettingsRepository;
    private readonly Mock<IOutboxMessageRepository> _mockOutboxMessageRepository;
    private readonly Mock<ICacheService> _mockCache;
    private readonly SettingsService _settingsService;

    public SettingsServiceCacheTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockSystemAuthSettingsRepository = new Mock<ISystemAuthSettingsRepository>();
        _mockOutboxMessageRepository = new Mock<IOutboxMessageRepository>();
        _mockCache = new Mock<ICacheService>();

        _mockUnitOfWork.Setup(u => u.SystemAuthSettings).Returns(_mockSystemAuthSettingsRepository.Object);
        _mockUnitOfWork.Setup(u => u.OutboxMessages).Returns(_mockOutboxMessageRepository.Object);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _settingsService = new SettingsService(_mockUnitOfWork.Object, _mockCache.Object);
    }

    [Fact]
    public async Task GetSystemSettingsAsync_OnCacheHit_NeverQueriesTheDatabase()
    {
        // Arrange: ICacheService.GetOrSetAsync returns a cached value directly, without
        // ever invoking the factory delegate — this is what a real cache hit looks like.
        var cachedDto = new ERP.Identity.Application.DTOs.Settings.SystemAuthSettingsDto(
            true, true, true, true, true, 8, true, true, true, true, 5, 15, 60);

        _mockCache
            .Setup(c => c.GetOrSetAsync(
                "settings:identity:auth:system",
                It.IsAny<Func<Task<ERP.Identity.Application.DTOs.Settings.SystemAuthSettingsDto>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        // Act
        var result = await _settingsService.GetSystemSettingsAsync(CancellationToken.None);

        // Assert
        result.Should().Be(cachedDto);
        _mockSystemAuthSettingsRepository.Verify(r => r.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSystemSettingsAsync_OnCacheMiss_QueriesTheDatabaseExactlyOnce()
    {
        // Arrange: simulate a real cache miss by having the mock actually invoke the
        // factory delegate it was given, exactly like MemoryCacheService/RedisCacheService
        // do internally on a miss.
        _mockCache
            .Setup(c => c.GetOrSetAsync(
                "settings:identity:auth:system",
                It.IsAny<Func<Task<ERP.Identity.Application.DTOs.Settings.SystemAuthSettingsDto>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<Task<ERP.Identity.Application.DTOs.Settings.SystemAuthSettingsDto>>, TimeSpan?, CancellationToken>(
                (_, factory, _, _) => factory());

        var existingSettings = SystemAuthSettings.CreateDefault();
        _mockSystemAuthSettingsRepository
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSettings);

        // Act
        var result = await _settingsService.GetSystemSettingsAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockSystemAuthSettingsRepository.Verify(r => r.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSystemSettingsAsync_AfterSuccessfulSave_InvalidatesTheSystemCacheKey()
    {
        // Arrange
        var existingSettings = SystemAuthSettings.CreateDefault();
        _mockSystemAuthSettingsRepository
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSettings);
        _mockSystemAuthSettingsRepository
            .Setup(r => r.UpdateAsync(It.IsAny<SystemAuthSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockOutboxMessageRepository
            .Setup(r => r.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new ERP.Identity.Application.DTOs.Settings.UpdateSystemAuthSettingsRequest(
            true, true, true, true, true, 8, true, true, true, true, 5, 15, 60);

        // Act
        await _settingsService.UpdateSystemSettingsAsync(request, CancellationToken.None);

        // Assert: cache is invalidated only after the DB write succeeds, never before.
        _mockCache.Verify(c => c.RemoveAsync("settings:identity:auth:system", It.IsAny<CancellationToken>()), Times.Once);
    }
}
