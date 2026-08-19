using ERP.NTF.Application.Commands;
using ERP.NTF.Domain.Enums;
using MediatR;

namespace ERP.NTF.Application.EventHandlers;

// Example integration event + handler, replacing the source monorepo's ~13
// ERP-specific event/handler pairs (SalesOrderCreated, PaymentProcessed,
// EmployeeHired, etc.) — those assumed sibling services (SalesOrderService,
// PaymentService, ...) that don't exist in a standalone deployment.
//
// This demonstrates the wiring: an upstream service publishes a message, a
// consumer (see ExampleKafkaConsumer) deserializes it into a strongly-typed
// event, and mediator.Publish(event) dispatches it to whichever
// INotificationHandler<T> classes are registered — this one included. Add one
// event + handler pair like this per upstream event you actually need to react
// to, following the same shape.
public class ExampleIntegrationEvent : INotification
{
    public Guid RecipientId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
}

public class ExampleIntegrationEventHandler : INotificationHandler<ExampleIntegrationEvent>
{
    private readonly IMediator _mediator;
    public ExampleIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(ExampleIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.RecipientId.ToString(),
            RecipientEmail = @event.RecipientEmail,
            Channel = NotificationChannel.Email,
            TemplateCode = "WELCOME_EMAIL",
            TemplateVariables = new() { ["firstName"] = @event.RecipientName },
            Priority = NotificationPriority.Normal
        }, cancellationToken);
    }
}
