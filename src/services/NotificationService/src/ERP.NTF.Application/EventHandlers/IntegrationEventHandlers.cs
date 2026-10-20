using ERP.NTF.Application.Commands;
using ERP.NTF.Domain.Enums;
using MediatR;

namespace ERP.NTF.Application.EventHandlers;

// Integration events consumed from other services

public class UserCreatedIntegrationEvent : INotification
    {
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class SalesOrderCreatedIntegrationEvent : INotification
    {
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
}

public class SalesOrderApprovedIntegrationEvent : INotification
    {
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
}

public class PaymentProcessedIntegrationEvent : INotification
    {
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class ShipmentCreatedIntegrationEvent : INotification
    {
    public Guid ShipmentId { get; set; }
    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
}

public class EmployeeHiredIntegrationEvent : INotification
    {
    public Guid EmployeeId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class TwoFactorCodeGeneratedIntegrationEvent : INotification
{
    public Guid UserId { get; set; }
    public string Method { get; set; } = string.Empty; // "Sms" | "Email"
    public string Destination { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; }
}

public class PasswordResetRequestedIntegrationEvent : INotification
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string ResetLink { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; }
}

public class EmailConfirmationRequestedIntegrationEvent : INotification
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string ConfirmLink { get; set; } = string.Empty;
    public int ExpiryHours { get; set; }
}

public class PurchaseOrderPendingFinanceApprovalIntegrationEvent : INotification
{
    public Guid PurchaseOrderId { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string ManagerApprovedBy { get; set; } = string.Empty;
}

public class PurchaseOrderApprovedIntegrationEvent : INotification
{
    public Guid PurchaseOrderId { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
}

public class LeaveApprovedIntegrationEvent : INotification
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class LeaveRejectedIntegrationEvent : INotification
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Bell notifications for IdentityService's role/permission changes — see
// IdentityEventConsumer for how these are populated off the raw
// RoleAssignedDomainEvent/PermissionGrantedDomainEvent JSON. ChangedByUserId is
// null when the system (not another user) made the change — e.g. self-registration's
// default-role assignment — and the handlers below no-op in that case.
public class RoleAssignedIntegrationEvent : INotification
{
    public Guid UserId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
}

public class PermissionGrantedIntegrationEvent : INotification
{
    public Guid RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public List<Guid> AffectedUserIds { get; set; } = new();
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

public class TwoFactorCodeGeneratedIntegrationEventHandler : INotificationHandler<TwoFactorCodeGeneratedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public TwoFactorCodeGeneratedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(TwoFactorCodeGeneratedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        var isSms = string.Equals(@event.Method, "Sms", StringComparison.OrdinalIgnoreCase);

        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.UserId.ToString(),
            RecipientEmail = isSms ? string.Empty : @event.Destination,
            RecipientPhone = isSms ? @event.Destination : null,
            Channel = isSms ? NotificationChannel.Sms : NotificationChannel.Email,
            TemplateCode = "TWO_FACTOR_CODE",
            TemplateVariables = new()
            {
                ["code"] = @event.Code,
                ["expiryMinutes"] = @event.ExpiryMinutes.ToString()
            },
            Priority = NotificationPriority.High
        }, cancellationToken);
    }
}

// The reset/confirmation link embeds a single-use plaintext token — it is the only
// copy outside the user's mailbox (IdentityService stores just its hash).
public class PasswordResetRequestedIntegrationEventHandler : INotificationHandler<PasswordResetRequestedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public PasswordResetRequestedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(PasswordResetRequestedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.UserId.ToString(),
            RecipientEmail = @event.Email,
            Channel = NotificationChannel.Email,
            TemplateCode = "PASSWORD_RESET",
            TemplateVariables = new()
            {
                ["resetLink"] = @event.ResetLink,
                ["expiryMinutes"] = @event.ExpiryMinutes.ToString()
            },
            Priority = NotificationPriority.High
        }, cancellationToken);
    }
}

public class EmailConfirmationRequestedIntegrationEventHandler : INotificationHandler<EmailConfirmationRequestedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public EmailConfirmationRequestedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(EmailConfirmationRequestedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.UserId.ToString(),
            RecipientEmail = @event.Email,
            Channel = NotificationChannel.Email,
            TemplateCode = "EMAIL_CONFIRMATION",
            TemplateVariables = new()
            {
                ["confirmLink"] = @event.ConfirmLink,
                ["expiryHours"] = @event.ExpiryHours.ToString()
            },
            Priority = NotificationPriority.High
        }, cancellationToken);
    }
}

// Purchasing has no local "Finance team" mailbox/roster to resolve, so these
// route to a fixed demo address — this is enough to prove the approval-chain
// -> Kafka -> Notification wiring end-to-end; a real deployment would resolve
// actual approver contacts (e.g. via IdentityService) before sending.
public class PurchaseOrderPendingFinanceApprovalIntegrationEventHandler : INotificationHandler<PurchaseOrderPendingFinanceApprovalIntegrationEvent>
{
    private readonly IMediator _mediator;
    public PurchaseOrderPendingFinanceApprovalIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(PurchaseOrderPendingFinanceApprovalIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.PurchaseOrderId.ToString(),
            RecipientEmail = "finance-team@erp-demo.local",
            Channel = NotificationChannel.Email,
            TemplateCode = "PO_PENDING_FINANCE_APPROVAL",
            TemplateVariables = new()
            {
                ["poNumber"] = @event.PONumber,
                ["totalAmount"] = @event.TotalAmount.ToString("C"),
                ["managerApprovedBy"] = @event.ManagerApprovedBy
            },
            Priority = NotificationPriority.High
        }, cancellationToken);
    }
}

public class PurchaseOrderApprovedIntegrationEventHandler : INotificationHandler<PurchaseOrderApprovedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public PurchaseOrderApprovedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(PurchaseOrderApprovedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.PurchaseOrderId.ToString(),
            RecipientEmail = "purchasing-team@erp-demo.local",
            Channel = NotificationChannel.Email,
            TemplateCode = "PO_APPROVED",
            TemplateVariables = new()
            {
                ["poNumber"] = @event.PONumber,
                ["approvedBy"] = @event.ApprovedBy
            },
            Priority = NotificationPriority.Normal
        }, cancellationToken);
    }
}

public class LeaveApprovedIntegrationEventHandler : INotificationHandler<LeaveApprovedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public LeaveApprovedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(LeaveApprovedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.EmployeeId.ToString(),
            RecipientEmail = $"employee-{@event.EmployeeId:N}@erp-demo.local",
            Channel = NotificationChannel.Email,
            TemplateCode = "LEAVE_APPROVED",
            TemplateVariables = new()
            {
                ["startDate"] = @event.StartDate.ToString("dd/MM/yyyy"),
                ["endDate"] = @event.EndDate.ToString("dd/MM/yyyy")
            },
            Priority = NotificationPriority.Normal
        }, cancellationToken);
    }
}

public class LeaveRejectedIntegrationEventHandler : INotificationHandler<LeaveRejectedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public LeaveRejectedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(LeaveRejectedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.EmployeeId.ToString(),
            RecipientEmail = $"employee-{@event.EmployeeId:N}@erp-demo.local",
            Channel = NotificationChannel.Email,
            TemplateCode = "LEAVE_REJECTED",
            TemplateVariables = new()
            {
                ["reason"] = @event.Reason
            },
            Priority = NotificationPriority.Normal
        }, cancellationToken);
    }
}

public class RoleAssignedIntegrationEventHandler : INotificationHandler<RoleAssignedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public RoleAssignedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(RoleAssignedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        // No human actor (e.g. self-registration's default-role assignment) — nothing
        // for the recipient to be told about "who changed it," so skip the bell entirely.
        if (@event.ChangedByUserId is null)
            return;

        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = @event.UserId.ToString(),
            Channel = NotificationChannel.InApp,
            Subject = "Role changed",
            Body = $"Your role was changed to '{@event.RoleCode}' by {@event.ChangedByName ?? "an administrator"}.",
            Priority = NotificationPriority.Normal
        }, cancellationToken);
    }
}

public class PermissionGrantedIntegrationEventHandler : INotificationHandler<PermissionGrantedIntegrationEvent>
{
    private readonly IMediator _mediator;
    public PermissionGrantedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(PermissionGrantedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        if (@event.ChangedByUserId is null)
            return;

        foreach (var userId in @event.AffectedUserIds)
        {
            await _mediator.Send(new SendNotificationCommand
            {
                RecipientId = userId.ToString(),
                Channel = NotificationChannel.InApp,
                Subject = "Permission changed",
                Body = $"Your role '{@event.RoleCode}' was granted the '{@event.PermissionCode}' permission by {@event.ChangedByName ?? "an administrator"}.",
                Priority = NotificationPriority.Normal
            }, cancellationToken);
        }
    }
}

