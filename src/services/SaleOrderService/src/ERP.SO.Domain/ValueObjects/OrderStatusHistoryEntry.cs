using ERP.SO.Domain.Entities;

namespace ERP.SO.Domain.ValueObjects
{
    /// <summary>
    /// One row of the order's audit trail — every status transition appends one of
    /// these, recorded automatically by SalesOrder's transition methods rather than
    /// relying on callers to remember to log it.
    /// </summary>
    public class OrderStatusHistoryEntry
    {
        public OrderStatus FromStatus { get; set; }
        public OrderStatus ToStatus { get; set; }
        public string? Reason { get; set; }
        public string? ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }

        public OrderStatusHistoryEntry() { }

        public OrderStatusHistoryEntry(OrderStatus fromStatus, OrderStatus toStatus, string? reason, string? changedBy)
        {
            FromStatus = fromStatus;
            ToStatus = toStatus;
            Reason = reason;
            ChangedBy = changedBy;
            ChangedAt = DateTime.UtcNow;
        }
    }
}
