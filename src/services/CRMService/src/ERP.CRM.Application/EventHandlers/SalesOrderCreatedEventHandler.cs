using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Domain.Enums;
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
    private readonly IActivityRepository _activities;
    private readonly ILogger<SalesOrderCreatedEventHandler> _logger;

    public SalesOrderCreatedEventHandler(
        IContactRepository contacts,
        IActivityRepository activities,
        ILogger<SalesOrderCreatedEventHandler> logger)
    {
        _contacts = contacts;
        _activities = activities;
        _logger = logger;
    }

    public async Task HandleAsync(SalesOrderCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received SalesOrderCreatedIntegrationEvent OrderId={OrderId} CustomerId={CustomerId} Amount={Amount}",
            @event.OrderId, @event.CustomerId, @event.TotalAmount);

        var contact = await _contacts.GetByUserIdAsync(@event.CustomerId, cancellationToken);

        if (contact is null)
        {
            _logger.LogWarning(
                "No CRM contact found for CustomerId={CustomerId}. SalesOrder {OrderId} activity not logged.",
                @event.CustomerId, @event.OrderId);
            return;
        }

        var activity = Activity.Create(
            contactId: contact.Id,
            activityType: ActivityType.Note,
            subject: $"Sales order placed — Order #{@event.OrderId:N}",
            notes: $"Order total: {(@event.TotalAmount):C}. Order ID: {@event.OrderId}",
            source: "SalesOrderService");

        await _activities.AddAsync(activity, cancellationToken);

        _logger.LogInformation(
            "Activity {ActivityId} created for Contact={ContactId}, Order={OrderId}",
            activity.Id, contact.Id, @event.OrderId);
    }
}
