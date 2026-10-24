using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
using Confluent.Kafka;
using ERP.ACC.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ERP.ACC.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Consumes PurchaseOrderService's "purchasing-topic", PaymentService's "payment-topic", POSService's "pos-topic"
    /// and HRMService's "hrm-topic" — all publish via the Outbox pattern with Newtonsoft
    /// TypeNameHandling.All, so the "$type" discriminator holds the publisher's fully
    /// qualified CLR type name. Dispatch is by substring match against that, mirroring
    /// HRMService's MasterDataEventConsumer. Accounting's existing event handlers are
    /// real MediatR INotificationHandler implementations, so dispatch is via
    /// IMediator.Publish.
    /// </summary>
    public class AccountingKafkaConsumer : IHostedService
    {
        private const string PurchasingTopic = "purchasing-topic";
        private const string PaymentTopic = "payment-topic";
        private const string PosTopic = "pos-topic";
        private const string HrmTopic = "hrm-topic";

        /// <summary>Attempts per message before it is given up on (logged as an error with its position).</summary>
        private const int MaxAttempts = 3;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AccountingKafkaConsumer> _logger;
        private readonly IConsumer<string, string> _consumer;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public AccountingKafkaConsumer(
            IServiceProvider serviceProvider,
            ILogger<AccountingKafkaConsumer> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
                GroupId = "accounting-service-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                // The offset is committed only after the message was handled: with auto-commit a failed posting was lost.
                EnableAutoCommit = false,
                // A topic nobody has published to yet (hrm-topic on a fresh stack) is created by its first message; the default 5-minute
                // metadata refresh would leave the subscription idle until then.
                TopicMetadataRefreshIntervalMs = 30000
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer.Subscribe(new[] { PurchasingTopic, PaymentTopic, PosTopic, HrmTopic });
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

                    await HandleWithRetryAsync(result, cancellationToken);
                    _consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming from the subscribed topics");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling a message from the subscribed topics");
                }
            }
        }

        /// <summary>
        /// Handlers are idempotent (entries are keyed by source document), so a message that fails is simply tried again. After
        /// <see cref="MaxAttempts"/> failures it is logged with its topic / partition / offset and skipped, so one poison
        /// message cannot block the partition for ever.
        /// </summary>
        private async Task HandleWithRetryAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await DispatchAsync(result.Message.Value, cancellationToken);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < MaxAttempts)
                {
                    _logger.LogWarning(ex, "Attempt {Attempt} of {Max} failed for {TopicPartitionOffset}; retrying", attempt, MaxAttempts, result.TopicPartitionOffset);
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Giving up on {TopicPartitionOffset} after {Max} attempts; the message is skipped. Payload: {Payload}",
                        result.TopicPartitionOffset, MaxAttempts, Truncate(result.Message.Value));
                    return;
                }
            }
        }

        private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000] + "...";

        /// <summary>PaymentService serialises its PaymentMethod enum as a number (CreditCard 0 ... Cash 4); accept the name too.</summary>
        public static int ParsePaymentMethod(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return 0;
            if (token.Type == JTokenType.Integer)
                return token.Value<int>();
            return token.ToString().ToLowerInvariant() switch
            {
                "creditcard" => 0,
                "debitcard" => 1,
                "banktransfer" => 2,
                "ewallet" => 3,
                "cash" => 4,
                "cryptopayment" or "crypto" => 5,
                _ => 0
            };
        }

        /// <summary>POSService's TransactionCreatedEvent. Amounts is null on events published before ROADMAP P1-1c.</summary>
        public static PosSaleCompletedIntegrationEvent ParsePosSale(JObject raw)
        {
            var amounts = raw["Amounts"] as JObject;
            var tenders = amounts?.ArrayValue("Tenders")?
                .Select(t => new PosTenderLine(t.Value<string>("Method") ?? string.Empty, t.Value<decimal?>("Amount") ?? 0m))
                .ToList() ?? new List<PosTenderLine>();

            return new PosSaleCompletedIntegrationEvent
            {
                TransactionId = raw.GuidValue("TransactionId") ?? Guid.Empty,
                HasAmounts = amounts != null,
                SubTotal = amounts?.Value<decimal?>("SubTotal") ?? 0m,
                DiscountAmount = amounts?.Value<decimal?>("DiscountAmount") ?? 0m,
                TaxAmount = amounts?.Value<decimal?>("TaxAmount") ?? 0m,
                TotalAmount = raw.Value<decimal?>("TotalAmount") ?? 0m,
                Tenders = tenders,
                CompletedAt = raw.Value<DateTime?>("CompletedAt") ?? default,
                ExternalPaymentId = raw.GuidValue("ExternalPaymentId"),
            };
        }

        public static PosSaleVoidedIntegrationEvent ParsePosVoid(JObject raw) => new()
        {
            TransactionId = raw.GuidValue("TransactionId") ?? Guid.Empty,
            Reason = raw.Value<string?>("Reason"),
            VoidedAt = raw.Value<DateTime?>("VoidedAt") ?? default,
        };

        public static PosShiftClosedIntegrationEvent ParsePosShift(JObject raw) => new()
        {
            ShiftId = raw.GuidValue("ShiftId") ?? Guid.Empty,
            CashVariance = raw["CashVariance"]?.Type is JTokenType.Null or null ? null : raw.Value<decimal?>("CashVariance"),
            ClosedAt = raw.Value<DateTime?>("ClosedAt") ?? default,
        };

        /// <summary>HRMService's PaySlipGeneratedEvent. The amounts are 0 on events published before ROADMAP P1-1d.</summary>
        public static PaySlipGeneratedIntegrationEvent ParsePaySlipGenerated(JObject raw) => new()
        {
            PaySlipId = raw.GuidValue("PaySlipId") ?? Guid.Empty,
            PayPeriod = raw.Value<string?>("PayPeriod") ?? string.Empty,
            GrossSalary = raw.Value<decimal?>("GrossSalary") ?? 0m,
            OvertimePay = raw.Value<decimal?>("OvertimePay") ?? 0m,
            Deductions = raw.Value<decimal?>("Deductions") ?? 0m,
            NetPay = raw.Value<decimal?>("NetPay") ?? 0m,
            PeriodEnd = raw["PeriodEnd"]?.Type is JTokenType.Null or null ? null : raw.Value<DateTime?>("PeriodEnd"),
            OccurredAt = raw.Value<DateTime?>("OccurredOn") ?? default,
        };

        public static PaySlipPaidIntegrationEvent ParsePaySlipPaid(JObject raw) => new()
        {
            PaySlipId = raw.GuidValue("PaySlipId") ?? Guid.Empty,
            PayPeriod = raw.Value<string?>("PayPeriod") ?? string.Empty,
            NetPay = raw.Value<decimal?>("NetPay") ?? 0m,
            PaidOn = raw.Value<DateTime?>("PaidOn") ?? default,
        };

        public static PaySlipVoidedIntegrationEvent ParsePaySlipVoided(JObject raw) => new()
        {
            PaySlipId = raw.GuidValue("PaySlipId") ?? Guid.Empty,
            Reason = raw.Value<string?>("Reason"),
            OccurredAt = raw.Value<DateTime?>("OccurredOn") ?? default,
        };

        private async Task DispatchAsync(string json, CancellationToken cancellationToken)
        {
            var typeName = JObject.Parse(json)["$type"]?.ToString() ?? string.Empty;

            // Ledger entries belong to the tenant of the source document; an event without
            // one is dropped (fail closed), never posted to some other tenant's books.
            if (!EventTenant.TryRead(json, out var tenantId))
            {
                _logger.LogWarning("{EventType} without TenantId ignored", typeName);
                return;
            }
            using var tenantScope = TenantScope.Begin(tenantId);

            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            if (typeName.Contains("PurchaseOrderApprovedDomainEvent"))
            {
                // Final approval (manager, or finance above the threshold) carries the order
                // total; the bill is booked then. PurchaseOrderCreated has no lines yet.
                var raw = JObject.Parse(json);
                var evt = new PurchaseOrderApprovedIntegrationEvent
                {
                    POId = raw.GuidValue("PurchaseOrderId") ?? Guid.Empty,
                    PONumber = raw.Value<string?>("PONumber") ?? string.Empty,
                    SupplierId = raw.GuidValue("SupplierId") ?? Guid.Empty,
                    TotalAmount = raw.Value<decimal?>("TotalAmount") ?? 0m
                };

                if (evt.POId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("PaymentProcessedEvent"))
            {
                // PaymentService's PaymentProcessedEvent(PaymentId, OrderId, Amount, ...)
                // maps 1:1 onto the local PaymentProcessedIntegrationEvent.
                var raw = JObject.Parse(json);
                var evt = new PaymentProcessedIntegrationEvent
                {
                    PaymentId = raw.GuidValue("PaymentId") ?? Guid.Empty,
                    OrderId = raw.GuidValue("OrderId") ?? Guid.Empty,
                    Amount = raw.Value<decimal?>("Amount") ?? 0m,
                    Method = ParsePaymentMethod(raw["Method"]),
                    ProcessedAt = raw["ProcessedAt"]?.Type == JTokenType.Null ? null : raw.Value<DateTime?>("ProcessedAt")
                };

                if (evt.PaymentId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("TransactionCreatedEvent"))
            {
                var evt = ParsePosSale(JObject.Parse(json));
                if (evt.TransactionId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("TransactionVoidedEvent"))
            {
                var evt = ParsePosVoid(JObject.Parse(json));
                if (evt.TransactionId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("CashShiftClosedEvent"))
            {
                var evt = ParsePosShift(JObject.Parse(json));
                if (evt.ShiftId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("PaySlipGeneratedEvent"))
            {
                var evt = ParsePaySlipGenerated(JObject.Parse(json));
                if (evt.PaySlipId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("PaySlipPaidEvent"))
            {
                var evt = ParsePaySlipPaid(JObject.Parse(json));
                if (evt.PaySlipId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            else if (typeName.Contains("PaySlipVoidedEvent"))
            {
                var evt = ParsePaySlipVoided(JObject.Parse(json));
                if (evt.PaySlipId != Guid.Empty)
                    await mediator.Publish(evt, cancellationToken);
            }
            // Other topic events are not yet consumed by Accounting.
        }
    }
}
