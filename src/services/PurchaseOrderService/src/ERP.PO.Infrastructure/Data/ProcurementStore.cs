using ERP.PO.Application.Procurement;
using ERP.PO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.PO.Infrastructure.Data;

public class ProcurementStore : IProcurementStore
{
    private readonly PurchasingDbContext _db;

    public ProcurementStore(PurchasingDbContext db) => _db = db;

    public Task<RequestForQuotation?> GetRfqAsync(Guid id, CancellationToken ct) =>
        _db.RequestsForQuotation.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    public Task<List<RequestForQuotation>> ListRfqsAsync(RfqStatus? status, CancellationToken ct) =>
        _db.RequestsForQuotation.Where(r => !r.IsDeleted && (status == null || r.Status == status))
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public void Add(RequestForQuotation rfq) => _db.RequestsForQuotation.Add(rfq);

    public Task<PurchaseOrder?> GetOrderAsync(Guid id, CancellationToken ct) =>
        _db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);

    public void Add(PurchaseOrder order) => _db.PurchaseOrders.Add(order);

    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken ct) =>
        _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

    public async Task<Dictionary<Guid, string>> GetSupplierNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await _db.Suppliers.Where(s => list.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
    }

    public Task<SupplierInvoice?> GetInvoiceAsync(Guid id, CancellationToken ct) =>
        _db.SupplierInvoices.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);

    public Task<List<SupplierInvoice>> ListInvoicesAsync(SupplierInvoiceStatus? status, Guid? purchaseOrderId, CancellationToken ct) =>
        _db.SupplierInvoices
            .Where(i => !i.IsDeleted && (status == null || i.Status == status) && (purchaseOrderId == null || i.PurchaseOrderId == purchaseOrderId))
            .OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

    public Task<bool> InvoiceNumberExistsAsync(Guid supplierId, string invoiceNumber, CancellationToken ct) =>
        _db.SupplierInvoices.AnyAsync(i => !i.IsDeleted && i.SupplierId == supplierId && i.InvoiceNumber == invoiceNumber, ct);

    public async Task<Dictionary<string, decimal>> InvoicedQuantitiesAsync(Guid purchaseOrderId, Guid? excludeInvoiceId, CancellationToken ct)
    {
        var invoices = await _db.SupplierInvoices
            .Where(i => !i.IsDeleted && i.PurchaseOrderId == purchaseOrderId && i.Id != excludeInvoiceId
                        && (i.Status == SupplierInvoiceStatus.Matched || i.Status == SupplierInvoiceStatus.Approved))
            .ToListAsync(ct);
        return invoices.SelectMany(i => i.Lines)
            .GroupBy(l => l.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity), StringComparer.OrdinalIgnoreCase);
    }

    public void Add(SupplierInvoice invoice) => _db.SupplierInvoices.Add(invoice);

    public Task<VendorReturn?> GetReturnAsync(Guid id, CancellationToken ct) =>
        _db.VendorReturns.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    public Task<List<VendorReturn>> ListReturnsAsync(Guid? purchaseOrderId, VendorReturnStatus? status, CancellationToken ct) =>
        _db.VendorReturns
            .Where(r => !r.IsDeleted && (purchaseOrderId == null || r.PurchaseOrderId == purchaseOrderId) && (status == null || r.Status == status))
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<Dictionary<string, decimal>> ReturnedQuantitiesAsync(Guid purchaseOrderId, CancellationToken ct)
    {
        var returns = await _db.VendorReturns
            .Where(r => !r.IsDeleted && r.PurchaseOrderId == purchaseOrderId && r.Status != VendorReturnStatus.Cancelled)
            .ToListAsync(ct);
        return returns.SelectMany(r => r.Lines)
            .GroupBy(l => l.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity), StringComparer.OrdinalIgnoreCase);
    }

    public void Add(VendorReturn vendorReturn) => _db.VendorReturns.Add(vendorReturn);

    public async Task<PurchasingSettings> GetSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _db.PurchasingSettings.FirstOrDefaultAsync(ct);
        if (settings != null) return settings;
        settings = PurchasingSettings.CreateDefault(tenantId);
        _db.PurchasingSettings.Add(settings);
        return settings;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
