using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities;

public class ShipmentItem : BaseEntity
{
    public Guid ShipmentId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal Weight { get; private set; }

    private ShipmentItem() { }

    public static ShipmentItem Create(Guid shipmentId, Guid productId, string productName, int quantity, decimal weight)
    {
        return new ShipmentItem
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            Weight = weight,
            CreatedAt = DateTime.UtcNow
        };
    }
}
