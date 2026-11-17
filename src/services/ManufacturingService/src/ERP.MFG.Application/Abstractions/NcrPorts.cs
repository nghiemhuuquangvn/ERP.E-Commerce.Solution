using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

/// <summary>The filters of the NCR list (S23). The plant is always given; <c>OpenOnly</c> = Open or InDisposition.</summary>
public sealed record NcrFilter(Guid PlantId, NcrStatus? Status, NcrSourceType? Source, Guid? OrderId, string? Search, bool OpenOnly);

public interface INcrRepository
{
    /// <summary>An NCR with its disposition lines, tracked.</summary>
    Task<NonConformance?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The NCR a Failed inspection created (null when none yet), tracked.</summary>
    Task<NonConformance?> GetByInspectionAsync(Guid inspectionId, CancellationToken ct = default);

    /// <summary>The NCR with a disposition line that points at this rework order, tracked.</summary>
    Task<NonConformance?> GetByReworkOrderAsync(Guid orderId, CancellationToken ct = default);

    Task<List<NonConformance>> ListByInspectionsAsync(IReadOnlyCollection<Guid> inspectionIds, CancellationToken ct = default);

    Task<(List<NonConformance> Items, int Total)> ListAsync(NcrFilter filter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Every NCR of an order (lines included), newest first.</summary>
    Task<List<NonConformance>> ListForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>NCRs of the order that are still Open or InDisposition (they keep the order from closing).</summary>
    Task<int> CountOpenForOrderAsync(Guid orderId, CancellationToken ct = default);

    void Add(NonConformance ncr);
}
