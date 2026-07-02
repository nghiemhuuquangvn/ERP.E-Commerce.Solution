using ERP.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace ERP.CRM.Application.EventHandlers;

/// <summary>
/// Integration event consumed from PaymentService's "payment-topic".
/// PaymentProcessedEvent only carries (PaymentId, OrderId, Amount) — it does not
/// carry a CustomerId/UserId, and CRM has no repository method to resolve a
/// Contact/Opportunity by OrderId (only IContactRepository.GetByUserIdAsync exists,
/// used by SalesOrderCreatedEventHandler). Rather than force a lookup this data
/// can't support, this mirrors SalesOrderCreatedEventHandler's minimal-logging
/// style and just records the payment for correlation.
/// </summary>
public record PaymentProcessedIntegrationEvent(Guid PaymentId, Guid OrderId, decimal Amount);

public class PaymentProcessedEventHandler : IEventHandler<PaymentProcessedIntegrationEvent>
{
    private readonly ILogger<PaymentProcessedEventHandler> _logger;

    public PaymentProcessedEventHandler(ILogger<PaymentProcessedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(PaymentProcessedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Payment activity recorded: Payment {PaymentId} of {Amount:C} processed for order {OrderId}",
            @event.PaymentId, @event.Amount, @event.OrderId);

        return Task.CompletedTask;
    }
}
