namespace ERP.PO.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Ordered = 3,
    PartiallyReceived = 4,
    Received = 5,
    Cancelled = 6
}
