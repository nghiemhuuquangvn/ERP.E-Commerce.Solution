using ERP.CRM.Application.Interfaces;
using ERP.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace ERP.CRM.Application.EventHandlers;

/// <summary>
/// Integration event consumed from SaleOrderService.
/// Logs sales activity against the CRM contact linked to the customer.
/// </summary>
public record SalesOrderCreatedIntegrationEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount);

public class SalesOrderCreatedEventHandler : IEventHandler<SalesOrderCreatedIntegrationEvent>
{
    private readonly IContactRepository _contacts;
    private readonly ILogger<SalesOrderCreatedEventHandler> _logger;

    public SalesOrderCreatedEventHandler(
        IContactRepository contacts,
        ILogger<SalesOrderCreatedEventHandler> logger)
    {
        _contacts = contacts;
        _logger = logger;
    }

    public async Task HandleAsync(SalesOrderCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received SalesOrderCreatedIntegrationEvent OrderId={OrderId} CustomerId={CustomerId} Amount={Amount}",
            @event.OrderId, @event.CustomerId, @event.TotalAmount);

        // Find the CRM contact linked to this customer (by UserId)
        var contact = await _contacts.GetByUserIdAsync(@event.CustomerId, cancellationToken);

        if (contact is null)
        {
            _logger.LogWarning(
                "No CRM contact found for CustomerId={CustomerId}. SalesOrder {OrderId} activity not logged.",
                @event.CustomerId, @event.OrderId);
            return;
        }

        // Activity logging would be stored in a future Activity entity.
        // For now we record the event in the log so it can be correlated.
        _logger.LogInformation(
            "Sales activity recorded: Contact={ContactId} placed order {OrderId} for {Amount:C}",
            contact.Id, @event.OrderId, @event.TotalAmount);
    }
}
