using ERP.Shared.Core.Domain;
namespace ERP.WH.Domain.Aggregates.Inventory;

/// <summary>Resolves a scanned barcode to a Product (optionally a specific Lot/Serial) — did not exist before; scanner-critical per the business doc.</summary>
public class BarcodeMapping : ITenantOwned
{
    public Guid Id { get; private set; }
    /// <summary>Owning tenant — stamped by WarehouseDbContext on insert (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string Barcode { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Guid? LotId { get; private set; }
    public string? SerialNumber { get; private set; }

    private BarcodeMapping() { }

    public BarcodeMapping(string barcode, Guid productId, Guid? lotId = null, string? serialNumber = null)
    {
        Id = Guid.NewGuid();
        Barcode = barcode;
        ProductId = productId;
        LotId = lotId;
        SerialNumber = serialNumber;
    }
}
