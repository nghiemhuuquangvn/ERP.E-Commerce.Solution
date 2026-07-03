namespace ERP.SO.Application.DTOs
{
    public class SalesOrderDto
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public List<OrderLineItemDto> LineItems { get; set; } = new();
        public AddressDto ShippingAddress { get; set; } = new();
        public string? Notes { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? TrackingNumber { get; set; }
        public string? Carrier { get; set; }
        public bool HasPriceChangeWarning { get; set; }
        public DateTime? ReturnRequestedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public string? ReturnReason { get; set; }
        public decimal? RefundAmount { get; set; }
        public List<OrderNoteDto> InternalNotes { get; set; } = new();
        public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();
    }

    public class OrderNoteDto
    {
        public string Note { get; set; } = string.Empty;
        public string? AddedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OrderStatusHistoryDto
    {
        public string FromStatus { get; set; } = string.Empty;
        public string ToStatus { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class SalesOrderSummaryDto
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
    }
}
