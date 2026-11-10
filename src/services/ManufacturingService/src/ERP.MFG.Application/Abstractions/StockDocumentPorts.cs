using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

/// <summary>Filters of the stock-document list. Dates are the creation day, both ends inclusive.</summary>
public sealed record StockDocumentFilter(Guid PlantId, StockDocumentStatus? Status, StockDocumentType? Type, Guid? OrderId, string? Search);

/// <summary>
/// One line of a stock document seen from the material it moves: used for the limits (pending quantities count), the lot caps of a return and the cost of a return.
/// Only lines of Pending (still going out) and Posted documents are returned, and only lines that are Pending or Posted.
/// </summary>
public sealed record StockFlowLine(Guid OrderMaterialId, StockDocumentType Type, StockDocumentStatus DocumentStatus, StockDocumentMode DocumentMode, StockLineStatus LineStatus,
    decimal Qty, Guid? LotId, decimal? UnitCost);

public interface IStockDocumentRepository
{
    /// <summary>The document with its lines, tracked.</summary>
    Task<MaterialTransaction?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The document that was created with this Idempotency-Key (tenant scope), if any.</summary>
    Task<MaterialTransaction?> FindByKeyAsync(string idempotencyKey, CancellationToken ct = default);

    Task<(List<MaterialTransaction> Items, int Total)> ListAsync(StockDocumentFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<List<MaterialTransaction>> ListForOrderAsync(Guid orderId, CancellationToken ct = default);

    Task<List<StockFlowLine>> FlowsAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>How many documents of the order are Pending (still to reach the Warehouse, or being reversed).</summary>
    Task<int> CountPendingAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Pending documents whose next attempt is due, across all tenants (no user, no tenant scope): only ids leave this method; the caller processes each
    /// inside <c>TenantScope.Begin(tenantId)</c>.
    /// </summary>
    Task<List<(Guid TenantId, Guid DocumentId)>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default);

    void Add(MaterialTransaction document);
}

/// <summary>How a posting round ended: <see cref="StockDocumentStatus.Posted"/>, still <see cref="StockDocumentStatus.Pending"/> (retry later), or <see cref="StockDocumentStatus.Failed"/> with the code of the refusal.</summary>
public sealed record StockPostingResult(StockDocumentStatus Status, string? ErrorCode, string? Error);

/// <summary>
/// The pending-document engine (ADR Phase 3, decisions 1-3): sends the lines of a document to the Warehouse, one movement each, under their keys; applies the effect on the
/// order once every line is posted; reverses the posted lines after a refusal or an abandon. Safe to call again at any moment: every Warehouse call is idempotent.
/// </summary>
public interface IStockPostingService
{
    Task<StockPostingResult> PostAsync(MaterialTransaction document, string by, CancellationToken ct = default);
}
