using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Confluent.Kafka;
using ERP.NTF.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.NTF.Infrastructure.BackgroundJobs;

/// <summary>
/// Consumes payment-topic (PaymentProcessed), shipping-topic (ShipmentCreated),
/// mst-topic (EmployeeHired), purchasing-topic (PurchaseOrder approval-chain events),
/// and hrm-topic (Leave approval events) from Kafka. All topics publish via the Outbox
/// pattern with Newtonsoft TypeNameHandling.All ($type discriminator). Follows
/// IdentityEventConsumer's dispatch pattern — mediator.Publish so existing
/// INotificationHandler implementations in IntegrationEventHandlers.cs are triggered.
/// </summary>
public class NotificationKafkaConsumer : IHostedService
{
    private const string PaymentTopic = "payment-topic";
    private const string ShippingTopic = "shipping-topic";
    private const string MstTopic = "mst-topic";
    private const string PurchasingTopic = "purchasing-topic";
    private const string HrmTopic = "hrm-topic";
    private const string PromotionTopic = "promotion-topic";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationKafkaConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private CancellationTokenSource? _cts;
    private Task? _executingTask;

    public NotificationKafkaConsumer(
        IServiceProvider serviceProvider,
        ILogger<NotificationKafkaConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
            GroupId = "notification-service-events-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumer.Subscribe(new[] { PaymentTopic, ShippingTopic, MstTopic, PurchasingTopic, HrmTopic, PromotionTopic });
        _executingTask = Task.Run(() => ExecuteAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null) return;
        _cts?.Cancel();
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
                if (result?.Message?.Value == null) continue;

                await DispatchWithRetryAsync(result.Message.Value, cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Kafka consume error"); }
            catch (Exception ex) { _logger.LogError(ex, "Error handling Kafka message"); }
        }
    }

    private const int MaxAttempts = 5;

    /// <summary>
    /// A handler error used to lose the message (auto-commit). It is now tried up to 5 times with a growing pause and then logged with
    /// its payload (ROADMAP P1-6). The handlers are idempotent (notification dedup keys), so a retry never sends twice.
    /// </summary>
    private async Task DispatchWithRetryAsync(string json, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await DispatchAsync(json, cancellationToken);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} of {Max} failed for a Kafka message; retrying", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Giving up on a Kafka message after {Max} attempts; it is dropped. Payload: {Payload}", MaxAttempts, json);
                return;
            }
        }
    }

    /// <summary>
    /// JToken.Value&lt;Guid?&gt;() throws InvalidCastException on a plain JSON string
    /// value in this environment (Newtonsoft's nullable-target conversion path goes
    /// through System.Convert.ChangeType, which doesn't know how to target Guid) —
    /// silently breaking every branch below that used it, including the pre-existing
    /// ones, since none of their outcomes were ever verified against real flowing
    /// data before now. Parse explicitly instead.
    /// </summary>
    private static Guid GuidOrEmpty(JObject raw, string key) =>
        Guid.TryParse(raw[key]?.ToString(), out var value) ? value : Guid.Empty;

    private async Task DispatchAsync(string json, CancellationToken cancellationToken)
    {
        var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

        // Every business event carries its tenant (stamped by the publishing service's Outbox); without one nothing is
        // changed (fail closed).
        if (!EventTenant.TryRead(json, out var tenantId))
        {
            _logger.LogWarning("{EventType} without TenantId ignored", typeName);
            return;
        }
        using var tenantScope = TenantScope.Begin(tenantId);

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        if (typeName.Contains("PaymentProcessedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new PaymentProcessedIntegrationEvent
            {
                PaymentId = GuidOrEmpty(raw, "PaymentId"),
                OrderId = GuidOrEmpty(raw, "OrderId"),
                RecipientEmail = raw.Value<string>("CustomerEmail") ?? raw.Value<string>("Email") ?? string.Empty,
                RecipientName = raw.Value<string>("CustomerName") ?? string.Empty,
                Amount = raw.Value<decimal?>("Amount") ?? 0m,
                Currency = raw.Value<string>("Currency") ?? "VND"
            };
            if (evt.PaymentId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("LoyaltyPointsEarnedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new LoyaltyPointsEarnedIntegrationEvent
            {
                AccountId = GuidOrEmpty(raw, "AccountId"),
                UserId = GuidOrEmpty(raw, "UserId"),
                PointsEarned = raw.Value<int?>("PointsEarned") ?? 0,
                TotalAvailable = raw.Value<int?>("TotalAvailable") ?? 0,
                Reason = raw.Value<string>("Reason") ?? string.Empty
            };
            if (evt.UserId != Guid.Empty && evt.PointsEarned > 0)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("CouponRedeemedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new CouponRedeemedIntegrationEvent
            {
                CouponId = GuidOrEmpty(raw, "CouponId"),
                Code = raw.Value<string>("Code") ?? string.Empty,
                UserId = GuidOrEmpty(raw, "UserId"), // a string on the wire: not a user id (e.g. a guest) is skipped
                DiscountAmount = raw.Value<decimal?>("DiscountAmount") ?? 0m,
                OccurredOn = raw.Value<DateTime?>("OccurredOn") ?? DateTime.UtcNow
            };
            if (evt.UserId != Guid.Empty && evt.CouponId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("PaymentFailedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new PaymentFailedIntegrationEvent
            {
                PaymentId = GuidOrEmpty(raw, "PaymentId"),
                OrderId = GuidOrEmpty(raw, "OrderId"),
                Amount = raw.Value<decimal?>("Amount") ?? 0m,
                FailureReason = raw.Value<string>("FailureReason") ?? string.Empty
            };
            if (evt.PaymentId != Guid.Empty && evt.OrderId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("PaymentRefundedEvent"))
        {
            var raw = JObject.Parse(json);
            var refundId = GuidOrEmpty(raw, "RefundId");
            var evt = new PaymentRefundedIntegrationEvent
            {
                PaymentId = GuidOrEmpty(raw, "PaymentId"),
                OrderId = GuidOrEmpty(raw, "OrderId"),
                RefundId = refundId == Guid.Empty ? null : refundId,
                RefundAmount = raw.Value<decimal?>("RefundAmount") ?? 0m,
                Currency = raw.Value<string>("Currency") ?? string.Empty,
                Reason = raw.Value<string>("Reason") ?? string.Empty
            };
            if (evt.PaymentId != Guid.Empty && evt.OrderId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("ShipmentCreated") || typeName.Contains("ShipmentCreatedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new ShipmentCreatedIntegrationEvent
            {
                ShipmentId = GuidOrEmpty(raw, "ShipmentId"),
                OrderId = GuidOrEmpty(raw, "OrderId"),
                CustomerEmail = raw.Value<string>("CustomerEmail") ?? string.Empty,
                CustomerName = raw.Value<string>("CustomerName") ?? string.Empty,
                TrackingNumber = raw.Value<string>("TrackingNumber") ?? string.Empty,
                CarrierName = raw.Value<string>("CarrierName") ?? raw.Value<string>("Carrier") ?? string.Empty
            };
            if (evt.ShipmentId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("EmployeeHired"))
        {
            var raw = JObject.Parse(json);
            var evt = new EmployeeHiredIntegrationEvent
            {
                EmployeeId = GuidOrEmpty(raw, "EmployeeId"),
                Email = raw.Value<string>("Email") ?? string.Empty,
                FirstName = raw.Value<string>("FirstName") ?? string.Empty,
                LastName = raw.Value<string>("LastName") ?? string.Empty,
                Department = raw.Value<string>("Department") ?? raw.Value<string>("DepartmentName") ?? string.Empty
            };
            if (evt.EmployeeId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("PurchaseOrderPendingFinanceApprovalDomainEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new PurchaseOrderPendingFinanceApprovalIntegrationEvent
            {
                PurchaseOrderId = GuidOrEmpty(raw, "PurchaseOrderId"),
                PONumber = raw.Value<string>("PONumber") ?? string.Empty,
                TotalAmount = raw.Value<decimal?>("TotalAmount") ?? 0m,
                ManagerApprovedBy = raw.Value<string>("ManagerApprovedBy") ?? string.Empty
            };
            if (evt.PurchaseOrderId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("PurchaseOrderApprovedDomainEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new PurchaseOrderApprovedIntegrationEvent
            {
                PurchaseOrderId = GuidOrEmpty(raw, "PurchaseOrderId"),
                PONumber = raw.Value<string>("PONumber") ?? string.Empty,
                ApprovedBy = raw.Value<string>("ApprovedBy") ?? string.Empty
            };
            if (evt.PurchaseOrderId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("LeaveApprovedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new LeaveApprovedIntegrationEvent
            {
                LeaveRequestId = GuidOrEmpty(raw, "LeaveRequestId"),
                EmployeeId = GuidOrEmpty(raw, "EmployeeId"),
                StartDate = raw.Value<DateTime?>("StartDate") ?? DateTime.UtcNow,
                EndDate = raw.Value<DateTime?>("EndDate") ?? DateTime.UtcNow
            };
            if (evt.EmployeeId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
        else if (typeName.Contains("LeaveRejectedEvent"))
        {
            var raw = JObject.Parse(json);
            var evt = new LeaveRejectedIntegrationEvent
            {
                LeaveRequestId = GuidOrEmpty(raw, "LeaveRequestId"),
                EmployeeId = GuidOrEmpty(raw, "EmployeeId"),
                Reason = raw.Value<string>("Reason") ?? string.Empty
            };
            if (evt.EmployeeId != Guid.Empty)
                await mediator.Publish(evt, cancellationToken);
        }
    }
}
