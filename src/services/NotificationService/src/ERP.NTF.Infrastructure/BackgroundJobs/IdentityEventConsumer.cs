using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Confluent.Kafka;
using ERP.NTF.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ERP.NTF.Infrastructure.BackgroundJobs;

/// <summary>
/// Consumes IdentityService's "identity-topic" — the outbox there serializes raw domain
/// events (via Newtonsoft TypeNameHandling.All), so the "$type" discriminator holds the
/// publisher's fully-qualified CLR type name (e.g. ...UserRegisteredDomainEvent,
/// ERP.Identity.Domain). Dispatch is by substring match against that, mirroring
/// HRMService's MasterDataEventConsumer pattern — but here the local contracts are real
/// MediatR INotificationHandler implementations, so we mediator.Publish(evt) rather than
/// calling a handler class's HandleAsync directly.
/// </summary>
public class IdentityEventConsumer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdentityEventConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _executingTask;

    public IdentityEventConsumer(
        IServiceProvider serviceProvider,
        ILogger<IdentityEventConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "notification-service-identity-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe("identity-topic");
        _executingTask = Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null) return;

        _cancellationTokenSource?.Cancel();
        await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));

        _consumer.Close();
        _consumer.Dispose();
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromSeconds(5));
                if (result?.Message?.Value == null)
                    continue;

                await DispatchAsync(result.Message.Value, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming from identity-topic");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from identity-topic");
            }
        }
    }

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

        // Identity events carry the user's tenant (OTP, reset links, welcome mails go out with that tenant's templates/settings); without one nothing is
        // changed (fail closed).
        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("{EventType} without TenantId ignored", typeName);
            return;
        }
        using var tenantScope = TenantScope.Begin(tenantId);

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        if (typeName.Contains("TwoFactorCodeGeneratedDomainEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new TwoFactorCodeGeneratedIntegrationEvent
            {
                UserId = raw.GuidValue("UserId") ?? Guid.Empty,
                Method = raw.Value<string>("Method") ?? string.Empty,
                Destination = raw.Value<string>("Destination") ?? string.Empty,
                Code = raw.Value<string>("Code") ?? string.Empty,
                ExpiryMinutes = raw.Value<int?>("ExpiryMinutes") ?? 0
            };

            if (evt.UserId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("PasswordResetRequestedDomainEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new PasswordResetRequestedIntegrationEvent
            {
                UserId = raw.GuidValue("UserId") ?? Guid.Empty,
                Email = raw.Value<string>("Email") ?? string.Empty,
                ResetLink = raw.Value<string>("ResetLink") ?? string.Empty,
                ExpiryMinutes = raw.Value<int?>("ExpiryMinutes") ?? 0
            };
            if (evt.UserId != Guid.Empty && !string.IsNullOrWhiteSpace(evt.Email))
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("EmailConfirmationRequestedDomainEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new EmailConfirmationRequestedIntegrationEvent
            {
                UserId = raw.GuidValue("UserId") ?? Guid.Empty,
                Email = raw.Value<string>("Email") ?? string.Empty,
                ConfirmLink = raw.Value<string>("ConfirmLink") ?? string.Empty,
                ExpiryHours = raw.Value<int?>("ExpiryHours") ?? 0
            };
            if (evt.UserId != Guid.Empty && !string.IsNullOrWhiteSpace(evt.Email))
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("UserRegisteredDomainEvent"))
        {
            // IdentityService's UserRegisteredDomainEvent only carries
            // (UserId, TenantId, Email, UserType, OccurredOn) — no first/last name split —
            // so FirstName/LastName are left blank rather than guessed.
            var raw = JObject.Parse(json);
            var evt = new UserCreatedIntegrationEvent
            {
                UserId = raw.GuidValue("UserId") ?? Guid.Empty,
                Email = raw.Value<string>("Email") ?? string.Empty,
                FirstName = string.Empty,
                LastName = string.Empty
            };

            if (evt.UserId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("RoleAssignedDomainEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new RoleAssignedIntegrationEvent
            {
                UserId = raw.GuidValue("UserId") ?? Guid.Empty,
                RoleCode = raw.Value<string>("RoleCode") ?? string.Empty,
                ChangedByUserId = raw.GuidValue("ChangedByUserId"),
                ChangedByName = raw.Value<string>("ChangedByName")
            };

            if (evt.UserId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("PermissionGrantedDomainEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new PermissionGrantedIntegrationEvent
            {
                RoleId = raw.GuidValue("RoleId") ?? Guid.Empty,
                RoleCode = raw.Value<string>("RoleCode") ?? string.Empty,
                PermissionCode = raw.Value<string>("PermissionCode") ?? string.Empty,
                ChangedByUserId = raw.GuidValue("ChangedByUserId"),
                ChangedByName = raw.Value<string>("ChangedByName"),
                AffectedUserIds = raw["AffectedUserIds"] is JArray arr
                    ? arr.Select(t => t.ToObject<Guid>()).ToList()
                    : new List<Guid>()
            };

            if (evt.RoleId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        // Other identity-topic events are not yet consumed by Notification — no local
        // template/handler exists for them today.
    }
}
