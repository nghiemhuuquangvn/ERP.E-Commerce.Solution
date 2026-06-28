using ERP.NTF.Application.Commands;
using ERP.NTF.Domain.Enums;
using MediatR;

namespace ERP.NTF.Application.EventHandlers;

// Integration events consumed from other services

public class UserCreatedIntegrationEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class SalesOrderCreatedIntegrationEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
}

public class SalesOrderApprovedIntegrationEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
}

public class PaymentProcessedIntegrationEvent
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class ShipmentCreatedIntegrationEvent
{
    public Guid ShipmentId { get; set; }
    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
}

public class EmployeeHiredIntegrationEvent
{
    public Guid EmployeeId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

// Event handlers — dispatch SendNotificationCommand via MediatR

public class UserCreatedIntegrationEventHandler : INotificationHandler<UserCreatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public UserCreatedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(UserCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.UserId.ToString(),
            RecipientEmail = @event.Email,
            Channel = NotificationChannel.Email,
            TemplateCode = "WELCOME_EMAIL",
            TemplateVariables = new() { ["firstName"] = @event.FirstName, ["lastName"] = @event.LastName },
            Priority = NotificationPriority.Normal
        }, cancellationToken);
    }
}

public class SalesOrderCreatedIntegrationEventHandler : INotificationHandler<SalesOrderCreatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public SalesOrderCreatedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(SalesOrderCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.CustomerId.ToString(),
            RecipientEmail = @event.CustomerEmail,
            Channel = NotificationChannel.Email,
            TemplateCode = "ORDER_CONFIRMATION",
            TemplateVariables = new()
            {
                ["customerName"] = @event.CustomerName,
                ["orderId"] = @event.OrderId.ToString(),
                ["totalAmount"] = @event.TotalAmount.ToString("C"),
                ["orderDate"] = @event.OrderDate.ToString("dd/MM/yyyy")
            },
            Priority = NotificationPriority.High
        }, cancellationToken);
    }
}

public class PaymentProcessedIntegrationEventHandler : INotificationHandler<PaymentProcessedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public PaymentProcessedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(PaymentProcessedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.OrderId.ToString(),
            RecipientEmail = @event.RecipientEmail,
            Channel = NotificationChannel.Email,
            TemplateCode = "PAYMENT_RECEIPT",
            TemplateVariables = new()
            {
                ["recipientName"] = @event.RecipientName,
                ["orderId"] = @event.OrderId.ToString(),
                ["amount"] = @event.Amount.ToString("C"),
                ["currency"] = @event.Currency
            },
            Priority = NotificationPriority.High
        }, cancellationToken);
    }
}

public class ShipmentCreatedIntegrationEventHandler : INotificationHandler<ShipmentCreatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public ShipmentCreatedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(ShipmentCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.OrderId.ToString(),
            RecipientEmail = @event.CustomerEmail,
            Channel = NotificationChannel.Email,
            TemplateCode = "SHIPMENT_NOTIFICATION",
            TemplateVariables = new()
            {
                ["customerName"] = @event.CustomerName,
                ["trackingNumber"] = @event.TrackingNumber,
                ["carrier"] = @event.CarrierName
            },
            Priority = NotificationPriority.Normal
        }, cancellationToken);
    }
}

public class EmployeeHiredIntegrationEventHandler : INotificationHandler<EmployeeHiredIntegrationEvent>
{
    private readonly IMediator _mediator;
    public EmployeeHiredIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(EmployeeHiredIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.EmployeeId.ToString(),
            RecipientEmail = @event.Email,
            Channel = NotificationChannel.Email,
            TemplateCode = "EMPLOYEE_WELCOME",
            TemplateVariables = new()
            {
                ["firstName"] = @event.FirstName,
                ["lastName"] = @event.LastName,
                ["department"] = @event.Department
            },
            Priority = NotificationPriority.Normal
        }, cancellationToken);
    }
}
