using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Transfers;

public enum TransferOrderStatus
{
    Draft = 0,
    InTransit = 1,
    Completed = 2,
    Cancelled = 3
}

public class TransferOrderLine
{
    public Guid LineId { get; private set; }
    public Guid TransferOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    private TransferOrderLine() { }

    public TransferOrderLine(Guid transferOrderId, Guid productId, int quantity)
    {
        LineId = Guid.NewGuid();
        TransferOrderId = transferOrderId;
        ProductId = productId;
        Quantity = quantity;
    }
}

public class TransferOrder : ITenantOwned
{
    public Guid TransferOrderId { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string TransferNumber { get; private set; } = null!;
    public Guid FromLocationId { get; private set; }
    public Guid ToLocationId { get; private set; }
    public TransferOrderStatus Status { get; private set; }
    public List<TransferOrderLine> Lines { get; private set; } = new();
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private TransferOrder() { }

    public TransferOrder(string transferNumber, Guid fromLocationId, Guid toLocationId, string? notes = null)
    {
        if (fromLocationId == toLocationId)
            throw new InvalidOperationException("Transfer source and destination locations must differ");

        TransferOrderId = Guid.NewGuid();
        TransferNumber = transferNumber;
        FromLocationId = fromLocationId;
        ToLocationId = toLocationId;
        Notes = notes;
        Status = TransferOrderStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public void AddLine(Guid productId, int quantity)
    {
        if (Status != TransferOrderStatus.Draft)
            throw new InvalidOperationException("Cannot add lines once a transfer order has shipped");
        if (quantity <= 0)
            throw new InvalidOperationException("Transfer quantity must be positive");

        Lines.Add(new TransferOrderLine(TransferOrderId, productId, quantity));
    }

    public void Ship()
    {
        if (Status != TransferOrderStatus.Draft)
            throw new InvalidOperationException($"Cannot ship transfer order in status {Status}");
        if (Lines.Count == 0)
            throw new InvalidOperationException("Cannot ship a transfer order with no lines");

        Status = TransferOrderStatus.InTransit;
        ShippedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status != TransferOrderStatus.InTransit)
            throw new InvalidOperationException($"Cannot complete transfer order in status {Status}");

        Status = TransferOrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == TransferOrderStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed transfer order");

        Status = TransferOrderStatus.Cancelled;
    }
}
