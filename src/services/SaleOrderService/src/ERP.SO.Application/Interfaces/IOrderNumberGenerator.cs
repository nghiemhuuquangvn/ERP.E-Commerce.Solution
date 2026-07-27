namespace ERP.SO.Application.Interfaces;

/// <summary>Generates human-readable, per-tenant, per-month sequential numbers (e.g. SO-202607-00001) backing SalesOrder.OrderNo/SalesQuotation.QuotationNo.</summary>
public interface IOrderNumberGenerator
{
    Task<string> NextAsync(Guid tenantId, string prefix, CancellationToken ct = default);
}
