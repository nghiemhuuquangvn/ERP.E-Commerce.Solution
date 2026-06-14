namespace ERP.SO.Application.DTOs
{
    public class CreateOrderRequest
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<OrderLineItemDto> LineItems { get; set; } = new();
        public AddressDto ShippingAddress { get; set; } = new();
        public string? Notes { get; set; }
    }

    public class OrderLineItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class AddressDto
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = "US";
    }
}
