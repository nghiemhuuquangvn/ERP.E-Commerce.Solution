using MediatR;

namespace ERP.NTF.Application.EventHandlers;

// Messages for the people who must decide something (bell, and push when it is switched on). The approvers are the holders of the permission that
// decides the step, asked from IdentityService (IApproverDirectory); each message links to the record (EntityType / EntityId).

public class PurchaseOrderSubmittedIntegrationEvent : INotification
{
    public Guid PurchaseOrderId { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

public class SalesOrderSubmittedIntegrationEvent : INotification
{
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

public class LeaveRequestSubmittedIntegrationEvent : INotification
{
    public Guid LeaveRequestId { get; set; }
    public string LeaveRequestNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DaysRequested { get; set; }
    public string? Reason { get; set; }
}

public static class ApproverPermissions
{
    public const string PurchaseOrderApprove = "purchase_order:approve";
    public const string PurchaseOrderFinanceApprove = "purchase_order:finance_approve";
    public const string SalesOrderApprove = "sales_order:approve";
    public const string LeaveManage = "hrm:leave:manage";
}

/// <summary>A purchase order was submitted: the holders of purchase_order:approve decide.</summary>
public class PurchaseOrderSubmittedIntegrationEventHandler : INotificationHandler<PurchaseOrderSubmittedIntegrationEvent>
{
    private readonly ApproverNotifier _approvers;
    public PurchaseOrderSubmittedIntegrationEventHandler(ApproverNotifier approvers) => _approvers = approvers;

    public Task Handle(PurchaseOrderSubmittedIntegrationEvent e, CancellationToken ct) => _approvers.NotifyAsync(new ApproverNotifier.Message(
        ApproverPermissions.PurchaseOrderApprove, "PO_SUBMITTED", e.PurchaseOrderId,
        $"Purchase order {e.PONumber} is waiting for your approval",
        $"{e.PONumber} · {e.TotalAmount:N2} {e.CurrencyCode}".TrimEnd(),
        "PurchaseOrder", "purchase-order"), ct);
}

/// <summary>A purchase order passed the manager and is at or above the finance threshold: the holders of purchase_order:finance_approve decide.</summary>
public class PurchaseOrderPendingFinanceApprovalIntegrationEventHandler : INotificationHandler<PurchaseOrderPendingFinanceApprovalIntegrationEvent>
{
    private readonly ApproverNotifier _approvers;
    public PurchaseOrderPendingFinanceApprovalIntegrationEventHandler(ApproverNotifier approvers) => _approvers = approvers;

    public Task Handle(PurchaseOrderPendingFinanceApprovalIntegrationEvent e, CancellationToken ct) => _approvers.NotifyAsync(new ApproverNotifier.Message(
        ApproverPermissions.PurchaseOrderFinanceApprove, "PO_PENDING_FINANCE", e.PurchaseOrderId,
        $"Purchase order {e.PONumber} needs finance approval",
        string.IsNullOrWhiteSpace(e.ManagerApprovedBy) ? $"{e.PONumber} · {e.TotalAmount:N2}" : $"{e.PONumber} · {e.TotalAmount:N2} · approved by {e.ManagerApprovedBy}",
        "PurchaseOrder", "purchase-order"), ct);
}

/// <summary>A sales order was submitted: the holders of sales_order:approve decide.</summary>
public class SalesOrderSubmittedIntegrationEventHandler : INotificationHandler<SalesOrderSubmittedIntegrationEvent>
{
    private readonly ApproverNotifier _approvers;
    public SalesOrderSubmittedIntegrationEventHandler(ApproverNotifier approvers) => _approvers = approvers;

    public Task Handle(SalesOrderSubmittedIntegrationEvent e, CancellationToken ct) => _approvers.NotifyAsync(new ApproverNotifier.Message(
        ApproverPermissions.SalesOrderApprove, "SO_SUBMITTED", e.OrderId,
        $"Sales order {e.OrderNo} is waiting for your approval",
        string.IsNullOrWhiteSpace(e.CustomerName) ? $"{e.OrderNo} · {e.TotalAmount:N2}" : $"{e.OrderNo} · {e.CustomerName} · {e.TotalAmount:N2}",
        "SalesOrder", "sales-order"), ct);
}

/// <summary>A leave request was created: the holders of hrm:leave:manage decide.</summary>
public class LeaveRequestSubmittedIntegrationEventHandler : INotificationHandler<LeaveRequestSubmittedIntegrationEvent>
{
    private readonly ApproverNotifier _approvers;
    public LeaveRequestSubmittedIntegrationEventHandler(ApproverNotifier approvers) => _approvers = approvers;

    public Task Handle(LeaveRequestSubmittedIntegrationEvent e, CancellationToken ct)
    {
        var days = e.DaysRequested == 1 ? "1 day" : $"{e.DaysRequested} days";
        var body = $"{e.StartDate:yyyy-MM-dd} to {e.EndDate:yyyy-MM-dd} · {days}";
        if (!string.IsNullOrWhiteSpace(e.Reason)) body += $" · {e.Reason.Trim()}";
        return _approvers.NotifyAsync(new ApproverNotifier.Message(
            ApproverPermissions.LeaveManage, "LEAVE_SUBMITTED", e.LeaveRequestId,
            $"Leave request {e.LeaveRequestNumber} is waiting for your decision", body, "LeaveRequest", "leave-request"), ct);
    }
}
