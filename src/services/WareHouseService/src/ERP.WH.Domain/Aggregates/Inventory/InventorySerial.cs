namespace ERP.WH.Domain.Aggregates.Inventory;

public enum SerialStatus
{
    InStock = 0,
    Reserved = 1,
    Sold = 2,
    Damaged = 3
}

public class InventorySerial
{
    public Guid SerialId { get; private set; }
    public Guid ProductId { get; private set; }
    public string SerialNumber { get; private set; }
    public Guid? LotId { get; private set; }
    public SerialStatus Status { get; private set; }
    public DateTime ReceivedDate { get; private set; }
    public DateTime? SoldDate { get; private set; }

    public InventorySerial(Guid productId, string serialNumber, Guid? lotId = null)
    {
        SerialId = Guid.NewGuid();
        ProductId = productId;
        SerialNumber = serialNumber;
        LotId = lotId;
        Status = SerialStatus.InStock;
        ReceivedDate = DateTime.UtcNow;
    }

    public void Reserve()
    {
        if (Status != SerialStatus.InStock)
            throw new InvalidOperationException($"Serial {SerialNumber} is not in stock (status: {Status})");

        Status = SerialStatus.Reserved;
    }

    public void Sell()
    {
        if (Status != SerialStatus.InStock && Status != SerialStatus.Reserved)
            throw new InvalidOperationException($"Serial {SerialNumber} cannot be sold from status {Status}");

        Status = SerialStatus.Sold;
        SoldDate = DateTime.UtcNow;
    }

    public void MarkDamaged()
    {
        Status = SerialStatus.Damaged;
    }

    public void ReturnToStock()
    {
        Status = SerialStatus.InStock;
        SoldDate = null;
    }
}
