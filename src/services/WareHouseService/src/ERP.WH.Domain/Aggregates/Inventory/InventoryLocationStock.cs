using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Inventory;

/// <summary>
/// Bin-level stock breakdown for a product — additive alongside <see cref="Inventory"/>
/// (which keeps serving as the existing cross-location rollup). A product only gets a
/// row here once it's actually received/reserved/transferred at a specific location;
/// callers that never pass a LocationId keep working exactly as before against the
/// product-level rollup only.
/// </summary>
public class InventoryLocationStock : ITenantOwned
{
    public Guid Id { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid LocationId { get; private set; }
    public int Quantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int LockedQuantity { get; private set; }
    public int AvailableQuantity => Quantity - ReservedQuantity - LockedQuantity;
    public DateTime UpdatedAt { get; private set; }

    private InventoryLocationStock() { }

    public InventoryLocationStock(Guid productId, Guid locationId)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        LocationId = locationId;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanReserve(int quantity) => AvailableQuantity >= quantity;

    public void Receive(int quantity)
    {
        Quantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReserveStock(int quantity)
    {
        if (!CanReserve(quantity))
            throw new InvalidOperationException($"Insufficient stock at this location. Available: {AvailableQuantity}, Requested: {quantity}");

        ReservedQuantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReleaseReservation(int quantity)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SellStock(int quantity)
    {
        Quantity = Math.Max(0, Quantity - quantity);
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AdjustStock(int delta)
    {
        Quantity += delta;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Moves quantity out of this location (transfer source side) — does not touch ReservedQuantity.</summary>
    public void TransferOut(int quantity)
    {
        if (Quantity < quantity)
            throw new InvalidOperationException($"Insufficient stock at this location to transfer. Available: {Quantity}, Requested: {quantity}");

        Quantity -= quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Moves quantity into this location (transfer destination side).</summary>
    public void TransferIn(int quantity)
    {
        Quantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
