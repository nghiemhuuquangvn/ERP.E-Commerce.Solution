using ERP.PO.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.PO.Application.Procurement;

/// <summary>Everything the RFQ, supplier-invoice and vendor-return handlers read and write (tenant-filtered by the DbContext).</summary>
public interface IProcurementStore
{
    Task<RequestForQuotation?> GetRfqAsync(Guid id, CancellationToken ct);
    Task<List<RequestForQuotation>> ListRfqsAsync(RfqStatus? status, CancellationToken ct);
    void Add(RequestForQuotation rfq);

    Task<PurchaseOrder?> GetOrderAsync(Guid id, CancellationToken ct);
    void Add(PurchaseOrder order);
    Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken ct);
    Task<Dictionary<Guid, string>> GetSupplierNamesAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task<SupplierInvoice?> GetInvoiceAsync(Guid id, CancellationToken ct);
    Task<List<SupplierInvoice>> ListInvoicesAsync(SupplierInvoiceStatus? status, Guid? purchaseOrderId, CancellationToken ct);
    Task<bool> InvoiceNumberExistsAsync(Guid supplierId, string invoiceNumber, CancellationToken ct);
    /// <summary>Quantity per product already billed on the order's Matched and Approved invoices (the invoice being matched excluded).</summary>
    Task<Dictionary<string, decimal>> InvoicedQuantitiesAsync(Guid purchaseOrderId, Guid? excludeInvoiceId, CancellationToken ct);
    void Add(SupplierInvoice invoice);

    Task<VendorReturn?> GetReturnAsync(Guid id, CancellationToken ct);
    Task<List<VendorReturn>> ListReturnsAsync(Guid? purchaseOrderId, VendorReturnStatus? status, CancellationToken ct);
    /// <summary>Quantity per product on the order's draft, shipped and credited returns (a draft reserves its quantity).</summary>
    Task<Dictionary<string, decimal>> ReturnedQuantitiesAsync(Guid purchaseOrderId, CancellationToken ct);
    void Add(VendorReturn vendorReturn);

    Task<PurchasingSettings> GetSettingsAsync(Guid tenantId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}

/// <summary>Turns the domain's refusals into 400s with the business message.</summary>
public static class Rules
{
    public static T Run<T>(Func<T> action)
    {
        try { return action(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new BusinessRuleException("Purchasing", ex.Message);
        }
    }

    public static void Run(Action action) => Run<object?>(() => { action(); return null; });
}
