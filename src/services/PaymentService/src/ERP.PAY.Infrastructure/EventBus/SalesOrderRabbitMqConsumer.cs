using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediatR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Enums;

namespace ERP.PAY.Infrastructure.EventBus;

/// <summary>
/// Consumes SaleOrderService's "sales_order_events" topic exchange (RabbitMQ — that
/// service publishes via RabbitMqMessageBus, not Kafka/Outbox like most other
/// services). Deserializes (plain System.Text.Json — no "$type" discriminator, since
/// SaleOrderService's publisher uses plain JsonSerializer.Serialize).
///
/// 2026-07-14: payment initiation is now driven by OrderFulfillmentSaga's own
/// "sales_order.InitiatePaymentCommand" (published right after order creation, same
/// as before, just via the saga instead of a direct SalesOrderCreatedEvent binding —
/// retired that binding so payment isn't initiated twice once the saga also exists).
/// Also handles "sales_order.RefundPaymentCommand" (saga compensation when inventory
/// turns out to be insufficient after payment already succeeded) — looks up the
/// Payment by PaymentId and refunds its full Amount via the existing internal
/// RefundPaymentCommand; the saga's own message intentionally doesn't carry an amount.
/// </summary>
public class SalesOrderRabbitMqConsumer : IHostedService
{
    private const string ExchangeName = "sales_order_events";
    private const string QueueName = "payment-service.sales-order-events";

    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SalesOrderRabbitMqConsumer> _logger;
    private IModel? _channel;

    public SalesOrderRabbitMqConsumer(
        IConnection connection,
        IServiceProvider serviceProvider,
        ILogger<SalesOrderRabbitMqConsumer> logger)
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);

        _channel.QueueBind(QueueName, ExchangeName, "sales_order.InitiatePaymentCommand");
        _channel.QueueBind(QueueName, ExchangeName, "sales_order.RefundPaymentCommand");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;

        _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation("SalesOrderRabbitMqConsumer subscribed to {Exchange} via {Queue}", ExchangeName, QueueName);

        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());

            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            switch (ea.RoutingKey)
            {
                case "sales_order.InitiatePaymentCommand":
                    var initiate = JsonSerializer.Deserialize<SagaInitiatePaymentCommand>(json);
                    if (initiate != null)
                    {
                        await mediator.Send(new InitiatePaymentCommand
                        {
                            OrderId = initiate.OrderId,
                            Amount = initiate.Amount,
                            Currency = "USD",
                            Method = PaymentMethod.CreditCard
                        });
                    }
                    break;

                case "sales_order.RefundPaymentCommand":
                    var refund = JsonSerializer.Deserialize<SagaRefundPaymentCommand>(json);
                    if (refund != null)
                    {
                        var uow = scope.ServiceProvider.GetRequiredService<IPaymentUnitOfWork>();
                        var payment = await uow.Payments.GetByIdAsync(refund.PaymentId);
                        if (payment != null)
                        {
                            await mediator.Send(new RefundPaymentCommand
                            {
                                PaymentId = refund.PaymentId,
                                Amount = payment.Amount,
                                Reason = refund.Reason
                            });
                        }
                        else
                        {
                            _logger.LogWarning("RefundPaymentCommand for unknown Payment {PaymentId} (Order {OrderId})", refund.PaymentId, refund.OrderId);
                        }
                    }
                    break;

                default:
                    _logger.LogWarning("Unhandled routing key {RoutingKey} on {Exchange}", ea.RoutingKey, ExchangeName);
                    break;
            }

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message with routing key {RoutingKey} from {Exchange}", ea.RoutingKey, ExchangeName);
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _channel?.Dispose();
        return Task.CompletedTask;
    }
}

// Local mirrors of OrderFulfillmentSaga's own cross-service messages
// (ERP.SO.Application.Sagas) — only the fields Payment needs.
public class SagaInitiatePaymentCommand
{
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
}

public class SagaRefundPaymentCommand
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
