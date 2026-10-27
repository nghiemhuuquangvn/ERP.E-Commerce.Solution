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
        public string OrderNo { get; set; } = string.Empty;
        public string OrderSource { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? SalesRepId { get; set; }
        public Guid? QuotationId { get; set; }
        public Guid? PromotionId { get; set; }
        public decimal DiscountAmount { get; set; }
        /// <summary>What the customer paid for delivery; already part of TotalAmount (ROADMAP P1-11b).</summary>
        public decimal ShippingFee { get; set; }

        /// <summary>
        /// Rule-based flag (not ML): true when TotalAmount exceeds 3x this
        /// customer's average order value, and the customer has enough order
        /// history (4+ orders) for that average to be meaningful. Computed at
        /// read time in the query handler — not persisted.
        /// </summary>
        public bool IsAnomalous { get; set; }
        public string? AnomalyReason { get; set; }
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
        public string OrderNo { get; set; } = string.Empty;
        public Guid? SalesRepId { get; set; }
        public Guid? DepartmentId { get; set; }
        public bool IsAnomalous { get; set; }
        public string? AnomalyReason { get; set; }
    }
}
