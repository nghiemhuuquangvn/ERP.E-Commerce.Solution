namespace ERP.SO.Domain.ValueObjects
{
    /// <summary>Snapshot at quote time — same discipline as OrderLineItem, no ShippedQuantity since a quotation is never fulfilled directly.</summary>
    public class QuotationLineItem
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public decimal LineTotal => Quantity * UnitPrice;

        public QuotationLineItem() { }

        public QuotationLineItem(Guid productId, string productName, int quantity, decimal unitPrice)
        {
            ProductId = productId;
            ProductName = productName;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
    }
}
