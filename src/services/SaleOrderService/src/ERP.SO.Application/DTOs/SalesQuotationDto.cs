namespace ERP.SO.Application.DTOs
{
    public class SalesQuotationDto
    {
        public Guid QuotationId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string QuotationNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ValidUntil { get; set; }
        public decimal TotalAmount { get; set; }
        public Guid? ConvertedOrderId { get; set; }
        public List<QuotationLineItemDto> LineItems { get; set; } = new();
    }

    public class QuotationLineItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class SalesQuotationSummaryDto
    {
        public Guid QuotationId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string QuotationNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime ValidUntil { get; set; }
    }

    public class CreateQuotationRequest
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<QuotationLineItemDto> LineItems { get; set; } = new();
        public DateTime ValidUntil { get; set; }
    }
}
