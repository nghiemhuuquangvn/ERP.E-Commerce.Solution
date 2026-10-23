using ERP.Identity.Application.DTOs.Webhook;
using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.BackgroundJobs;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.Identity.UnitTests.Infrastructure;

/// <summary>ROADMAP P0-8: webhook signing secrets are encrypted at rest.</summary>
public class WebhookSecretTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static AesSecretProtector Aes() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Security:SettingsEncryptionKey"] = "unit-test-key" }).Build());

    private static (WebhookEndpointService Service, Mock<IWebhookEndpointRepository> Repo) NewService()
    {
        var repo = new Mock<IWebhookEndpointRepository>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.WebhookEndpoints).Returns(repo.Object);
        return (new WebhookEndpointService(uow.Object, Aes()), repo);
    }

    [Fact]
    public async Task Create_StoresTheSecretEncrypted_AndNeverReturnsIt()
    {
        var (service, repo) = NewService();
        WebhookEndpoint? stored = null;
        repo.Setup(r => r.AddAsync(It.IsAny<WebhookEndpoint>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookEndpoint, CancellationToken>((w, _) => stored = w);

        var dto = await service.CreateAsync(Tenant, new CreateWebhookEndpointRequest("erp", "https://hooks.example.com/in", "s3cret-value", null), CancellationToken.None);

        stored!.Secret.Should().StartWith(SecretEnvelope.Prefix).And.NotContain("s3cret-value");
        SecretEnvelope.Open(Aes(), stored.Secret!).Should().Be("s3cret-value");
        dto.HasSecret.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithoutASecret_StoresNone()
    {
        var (service, repo) = NewService();
        WebhookEndpoint? stored = null;
        repo.Setup(r => r.AddAsync(It.IsAny<WebhookEndpoint>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookEndpoint, CancellationToken>((w, _) => stored = w);

        var dto = await service.CreateAsync(Tenant, new CreateWebhookEndpointRequest("erp", "https://hooks.example.com/in", null, null), CancellationToken.None);

        stored!.Secret.Should().BeNull();
        dto.HasSecret.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WithoutASecret_KeepsTheStoredOne_AndWithOneReplacesIt()
    {
        var (service, repo) = NewService();
        var endpoint = WebhookEndpoint.Create(Tenant, "erp", "https://hooks.example.com/in", SecretEnvelope.Seal(Aes(), "first"), new List<string>());
        var original = endpoint.Secret;
        repo.Setup(r => r.GetByIdAsync(endpoint.Id, It.IsAny<CancellationToken>())).ReturnsAsync(endpoint);

        await service.UpdateAsync(endpoint.Id, Tenant, new UpdateWebhookEndpointRequest("renamed", "https://hooks.example.com/in", null, null), CancellationToken.None);
        endpoint.Secret.Should().Be(original, "the form never gets the secret back, so saving it again must not wipe it");
        endpoint.Name.Should().Be("renamed");

        await service.UpdateAsync(endpoint.Id, Tenant, new UpdateWebhookEndpointRequest("renamed", "https://hooks.example.com/in", "second", null), CancellationToken.None);
        SecretEnvelope.Open(Aes(), endpoint.Secret!).Should().Be("second");
    }

    [Fact]
    public async Task TheStartupJob_EncryptsLegacyPlaintextSecrets_AndLeavesTheRestAlone()
    {
        await using var db = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Mock<IDomainEventDispatcher>().Object);
        var plain = WebhookEndpoint.Create(Tenant, "old", "https://hooks.example.com/a", "legacy-secret", new List<string>());
        var sealedAlready = WebhookEndpoint.Create(Tenant, "new", "https://hooks.example.com/b", SecretEnvelope.Seal(Aes(), "kept"), new List<string>());
        var unsigned = WebhookEndpoint.Create(Tenant, "none", "https://hooks.example.com/c", null, new List<string>());
        db.WebhookEndpoints.AddRange(plain, sealedAlready, unsigned);
        await db.SaveChangesAsync();
        var sealedBefore = sealedAlready.Secret;

        var changed = await WebhookSecretEncryptionJob.EncryptLegacySecretsAsync(db, Aes(), CancellationToken.None);

        changed.Should().Be(1);
        SecretEnvelope.IsSealed(plain.Secret).Should().BeTrue();
        SecretEnvelope.Open(Aes(), plain.Secret!).Should().Be("legacy-secret");
        sealedAlready.Secret.Should().Be(sealedBefore);
        unsigned.Secret.Should().BeNull();
        (await WebhookSecretEncryptionJob.EncryptLegacySecretsAsync(db, Aes(), CancellationToken.None)).Should().Be(0, "idempotent");
    }

    // ---- delivery: the signing key is the decrypted secret ----

    private static async Task<Mock<IWebhookSender>> DeliverAsync(WebhookEndpoint endpoint)
    {
        var sender = new Mock<IWebhookSender>();
        var saved = new TaskCompletionSource();
        var outbox = new Mock<IOutboxMessageRepository>();
        outbox.SetupSequence(o => o.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage>
            {
                new() { Id = Guid.NewGuid(), Type = "UserRegisteredDomainEvent", OccurredOnUtc = DateTime.UtcNow,
                        Content = "{\"$type\":\"X, Y\",\"TenantId\":\"" + Tenant + "\"}" }
            })
            .ReturnsAsync(new List<OutboxMessage>());
        var hooks = new Mock<IWebhookEndpointRepository>();
        hooks.Setup(h => h.ListActiveByTenantAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new List<WebhookEndpoint> { endpoint });
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.OutboxMessages).Returns(outbox.Object);
        uow.Setup(u => u.WebhookEndpoints).Returns(hooks.Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => saved.TrySetResult()).ReturnsAsync(1);

        var services = new ServiceCollection()
            .AddSingleton(new Mock<IMessageBus>().Object).AddSingleton(uow.Object).AddSingleton(sender.Object)
            .AddSingleton<ISecretProtector>(Aes())
            .BuildServiceProvider();

        var processor = new OutboxProcessor(NullLogger<OutboxProcessor>.Instance, services);
        await processor.StartAsync(CancellationToken.None);
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await processor.StopAsync(CancellationToken.None);
        return sender;
    }

    [Fact]
    public async Task Delivery_SignsWithTheDecryptedSecret()
    {
        var endpoint = WebhookEndpoint.Create(Tenant, "erp", "https://hooks.example.com/in", SecretEnvelope.Seal(Aes(), "signing-key"), new List<string>());

        var sender = await DeliverAsync(endpoint);

        sender.Verify(s => s.SendAsync("https://hooks.example.com/in", "signing-key", "UserRegisteredDomainEvent", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delivery_StillSigns_WithALegacyPlaintextSecret()
    {
        var endpoint = WebhookEndpoint.Create(Tenant, "erp", "https://hooks.example.com/in", "plain-legacy", new List<string>());

        var sender = await DeliverAsync(endpoint);

        sender.Verify(s => s.SendAsync("https://hooks.example.com/in", "plain-legacy", "UserRegisteredDomainEvent", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
