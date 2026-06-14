namespace ERP.SO.Domain.ValueObjects
{
    public class OrderLineItem
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public decimal LineTotal => Quantity * UnitPrice;

        public OrderLineItem()
        {
        }

        public OrderLineItem(Guid productId, string productName, int quantity, decimal unitPrice)
        {
            ProductId = productId;
            ProductName = productName;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
    }
}
