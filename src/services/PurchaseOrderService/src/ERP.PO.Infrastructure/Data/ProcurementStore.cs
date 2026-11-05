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

    public async Task<(List<RequestForQuotation> Items, int Total)> ListRfqsAsync(RfqStatus? status, ListWindow window, int pageNumber, int pageSize, CancellationToken ct)
    {
        var (start, before, text) = (window.Checked().Start, window.Before, window.Text);
        var matching = _db.RequestsForQuotation
            .Where(r => !r.IsDeleted && (status == null || r.Status == status)
                        && (start == null || r.CreatedAt >= start) && (before == null || r.CreatedAt < before)
                        && (text == null || r.RfqNumber.ToLower().Contains(text) || r.Title.ToLower().Contains(text)));
        var total = await matching.CountAsync(ct);
        var items = await matching.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.RfqNumber)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

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

    public async Task<(List<SupplierInvoice> Items, int Total)> ListInvoicesAsync(SupplierInvoiceStatus? status, Guid? purchaseOrderId, ListWindow window, int pageNumber, int pageSize, CancellationToken ct)
    {
        // An invoice is judged on its invoice date (the day on the paper), not on the day it was typed in.
        var (start, before, text) = (window.Checked().Start, window.Before, window.Text);
        var matching = _db.SupplierInvoices
            .Where(i => !i.IsDeleted && (status == null || i.Status == status) && (purchaseOrderId == null || i.PurchaseOrderId == purchaseOrderId)
                        && (start == null || i.InvoiceDate >= start) && (before == null || i.InvoiceDate < before)
                        && (text == null || i.InvoiceNumber.ToLower().Contains(text) || i.PONumber.ToLower().Contains(text)));
        var total = await matching.CountAsync(ct);
        var items = await matching.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.InvoiceNumber)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

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

    public async Task<(List<VendorReturn> Items, int Total)> ListReturnsAsync(Guid? purchaseOrderId, VendorReturnStatus? status, ListWindow window, int pageNumber, int pageSize, CancellationToken ct)
    {
        var (start, before, text) = (window.Checked().Start, window.Before, window.Text);
        var matching = _db.VendorReturns
            .Where(r => !r.IsDeleted && (purchaseOrderId == null || r.PurchaseOrderId == purchaseOrderId) && (status == null || r.Status == status)
                        && (start == null || r.CreatedAt >= start) && (before == null || r.CreatedAt < before)
                        && (text == null || r.ReturnNumber.ToLower().Contains(text) || r.PONumber.ToLower().Contains(text)));
        var total = await matching.CountAsync(ct);
        var items = await matching.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReturnNumber)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

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

    private PurchasingSettings? _settings;

    /// <summary>One settings row per tenant: a default created in this request is remembered, so asking twice before the save does not add it twice.</summary>
    public async Task<PurchasingSettings> GetSettingsAsync(Guid tenantId, CancellationToken ct)
    {
        if (_settings != null) return _settings;
        _settings = await _db.PurchasingSettings.FirstOrDefaultAsync(ct);
        if (_settings != null) return _settings;
        _settings = PurchasingSettings.CreateDefault(tenantId);
        _db.PurchasingSettings.Add(_settings);
        return _settings;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
