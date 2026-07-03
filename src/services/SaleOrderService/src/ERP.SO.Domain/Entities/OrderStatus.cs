namespace ERP.SO.Domain.Entities
{
    public enum OrderStatus
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        Picked = 3,
        Shipped = 4,
        Delivered = 5,
        Rejected = 6,
        Cancelled = 7,
        PartiallyShipped = 8,
        ReturnRequested = 9,
        Returned = 10
    }
}
