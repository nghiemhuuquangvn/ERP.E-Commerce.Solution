using ERP.Identity.Application.Interfaces;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.BackgroundJobs;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Identity.Infrastructure.Security;
using ERP.Shared.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ERP.Identity.UnitTests.Infrastructure;

/// <summary>ROADMAP P0-8: a 2FA code / reset link / confirmation link must not sit in OutboxMessages in plaintext.</summary>
public class OutboxSecretsTests
{
    private const string Code = "482913";
    private static readonly Guid Tenant = Guid.NewGuid();

    private static AesSecretProtector Aes() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Security:SettingsEncryptionKey"] = "unit-test-key" }).Build());

    private static AuthDbContext NewContext(OutboxPayloadProtector? protector) =>
        new(new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Mock<IDomainEventDispatcher>().Object, protector);

    private static TwoFactorChallenge NewChallenge() =>
        TwoFactorChallenge.Create(Guid.NewGuid(), Tenant, "hash", "Email", "ad***@acme.test", "admin@acme.test", 5, 3, Code);

    [Fact]
    public async Task TheOutboxRow_ForATwoFactorCode_HoldsTheCodeEncrypted_AndEverythingElseReadable()
    {
        var protector = Aes();
        await using var db = NewContext(new OutboxPayloadProtector(protector));
        db.TwoFactorChallenges.Add(NewChallenge());

        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().SingleAsync();
        row.Content.Should().NotContain(Code);
        var json = JObject.Parse(row.Content);
        json["Code"]!.Value<string>().Should().StartWith(SecretEnvelope.Prefix);
        SecretEnvelope.Open(protector, json["Code"]!.Value<string>()!).Should().Be(Code);
        json["$type"]!.Value<string>().Should().Contain("TwoFactorCodeGeneratedDomainEvent");
        json["TenantId"]!.Value<string>().Should().Be(Tenant.ToString());
        json["Destination"]!.Value<string>().Should().Be("admin@acme.test", "Notification needs the address to deliver to");
        json["Method"]!.Value<string>().Should().Be("Email");
    }

    [Fact]
    public async Task WithoutAProtector_ASecretEventIsNotStoredAtAll()
    {
        await using var db = NewContext(protector: null);
        db.TwoFactorChallenges.Add(NewChallenge());

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Seal_ResetAndConfirmationLinks_AreEncrypted_OtherEventsUntouched()
    {
        var protector = new OutboxPayloadProtector(Aes());
        var reset = "{\"$type\":\"X, Y\",\"TenantId\":\"t\",\"Email\":\"a@b.test\",\"ResetLink\":\"https://s.test/reset?token=abc\",\"OccurredOn\":\"2026-10-23T07:12:10.5000000Z\"}";
        var confirm = "{\"$type\":\"X, Y\",\"ConfirmLink\":\"https://s.test/confirm?token=abc\"}";
        var other = "{\"$type\":\"X, Y\",\"Email\":\"a@b.test\"}";

        var sealedReset = protector.Seal("PasswordResetRequestedDomainEvent", reset);
        var sealedConfirm = protector.Seal("EmailConfirmationRequestedDomainEvent", confirm);

        sealedReset.Should().NotContain("token=abc").And.Contain("2026-10-23T07:12:10.5000000Z", "dates must not be re-formatted");
        JObject.Parse(sealedReset)["ResetLink"]!.Value<string>().Should().StartWith(SecretEnvelope.Prefix);
        sealedConfirm.Should().NotContain("token=abc");
        protector.Seal("UserRegisteredDomainEvent", other).Should().Be(other);
    }

    [Fact]
    public void Redact_ReplacesOnlyTheSecretField()
    {
        var json = "{\"$type\":\"X, Y\",\"Code\":\"enc:v1:abc\",\"TenantId\":\"t\",\"Destination\":\"a@b.test\"}";

        var redacted = JObject.Parse(OutboxPayloadProtector.Redact("TwoFactorCodeGeneratedDomainEvent", json));

        redacted["Code"]!.Value<string>().Should().Be(SecretEnvelope.Redacted);
        redacted["TenantId"]!.Value<string>().Should().Be("t");
        redacted["$type"]!.Value<string>().Should().Be("X, Y");
        OutboxPayloadProtector.Redact("UserRegisteredDomainEvent", json).Should().Be(json);
    }

    // ---- the processor: redact once published, keep the secret if publishing failed ----

    private static async Task<OutboxMessage> RunProcessorOnceAsync(OutboxMessage message, Exception? publishFailure)
    {
        var bus = new Mock<IMessageBus>();
        var setup = bus.Setup(b => b.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
        if (publishFailure != null) setup.ThrowsAsync(publishFailure); else setup.Returns(Task.CompletedTask);

        var saved = new TaskCompletionSource();
        var outbox = new Mock<IOutboxMessageRepository>();
        outbox.SetupSequence(o => o.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message })
            .ReturnsAsync(new List<OutboxMessage>());
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.OutboxMessages).Returns(outbox.Object);
        uow.Setup(u => u.WebhookEndpoints).Returns(new Mock<IWebhookEndpointRepository>().Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => saved.TrySetResult()).ReturnsAsync(1);

        var services = new ServiceCollection()
            .AddSingleton(bus.Object).AddSingleton(uow.Object).AddSingleton(new Mock<IWebhookSender>().Object)
            .BuildServiceProvider();

        var processor = new OutboxProcessor(NullLogger<OutboxProcessor>.Instance, services);
        await processor.StartAsync(CancellationToken.None);
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await processor.StopAsync(CancellationToken.None);
        return message;
    }

    private static OutboxMessage SealedCodeMessage() => new()
    {
        Id = Guid.NewGuid(),
        Type = "TwoFactorCodeGeneratedDomainEvent",
        OccurredOnUtc = DateTime.UtcNow,
        Content = new OutboxPayloadProtector(Aes()).Seal("TwoFactorCodeGeneratedDomainEvent",
            "{\"$type\":\"X, Y\",\"TenantId\":\"" + Tenant + "\",\"Code\":\"" + Code + "\"}")
    };

    [Fact]
    public async Task AfterAPublish_TheSecretIsRemovedFromTheRow()
    {
        var message = await RunProcessorOnceAsync(SealedCodeMessage(), publishFailure: null);

        message.ProcessedOnUtc.Should().NotBeNull();
        JObject.Parse(message.Content)["Code"]!.Value<string>().Should().Be(SecretEnvelope.Redacted);
    }

    [Fact]
    public async Task WhenThePublishFails_TheRowKeepsTheSealedSecret_ForTheRetry()
    {
        var original = SealedCodeMessage();
        var content = original.Content;

        var message = await RunProcessorOnceAsync(original, new InvalidOperationException("kafka down"));

        message.ProcessedOnUtc.Should().BeNull();
        message.Content.Should().Be(content);
        message.Error.Should().Contain("kafka down");
    }
}
