using ERP.Shared.Core.Domain;
using ERP.PAY.Domain.Enums;

namespace ERP.PAY.Domain.Entities;

public class Refund : BaseEntity
{
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public RefundStatus Status { get; private set; }
    public string? GatewayRefundId { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Refund() { }

    public static Refund Create(Guid paymentId, decimal amount, string reason)
    {
        return new Refund
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            Amount = amount,
            Reason = reason,
            Status = RefundStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsCompleted(string gatewayRefundId)
    {
        Status = RefundStatus.Completed;
        GatewayRefundId = gatewayRefundId;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string reason)
    {
        Status = RefundStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }
}
