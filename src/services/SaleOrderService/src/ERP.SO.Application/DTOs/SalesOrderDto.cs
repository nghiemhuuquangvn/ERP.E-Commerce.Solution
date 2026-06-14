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
