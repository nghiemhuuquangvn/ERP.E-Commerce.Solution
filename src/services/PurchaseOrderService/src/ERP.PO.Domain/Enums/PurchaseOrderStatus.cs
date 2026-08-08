namespace ERP.PO.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Ordered = 3,
    PartiallyReceived = 4,
    Received = 5,
    Cancelled = 6,
    Rejected = 7,

    /// <summary>
    /// Manager has approved but the order's total exceeds the finance
    /// approval threshold, so a second-level Finance approval is required
    /// before the order becomes Approved. Appended at the end (not inserted)
    /// so the existing 0-7 values keep their meaning for any code that
    /// pattern-matches on the raw int.
    /// </summary>
    PendingFinanceApproval = 8
}
