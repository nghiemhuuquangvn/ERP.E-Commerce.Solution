using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

public interface IInspectionPlanRepository
{
    /// <summary>A plan version with its characteristics (not deleted).</summary>
    Task<InspectionPlan?> GetAsync(Guid id, CancellationToken ct = default);

    Task<List<InspectionPlan>> ListAsync(Guid plantId, InspectionType? type, Guid? itemId, VersionStatus? status, CancellationToken ct = default);

    /// <summary>Every version (not deleted) of the series of a plant with its characteristics.</summary>
    Task<List<InspectionPlan>> ListSeriesAsync(Guid plantId, string code, CancellationToken ct = default);

    /// <summary>The highest version number of the series, deleted drafts included (a number is never reused).</summary>
    Task<int> MaxVersionAsync(Guid plantId, string code, CancellationToken ct = default);

    void Add(InspectionPlan plan);
}

/// <summary>
/// A profile or an operation names a plan series by the id of any of its versions; this returns the version that applies on a date
/// (Approved and in force), or null: a Draft or Obsolete plan, or no version in force, means no inspection (owner decision, P4-3).
/// </summary>
public interface IInspectionPlanResolver
{
    Task<InspectionPlan?> ResolveAsync(Guid? planId, DateOnly date, CancellationToken ct = default);
}

/// <summary>The filters of the inspection list (S22). The plant is always given.</summary>
public sealed record InspectionFilter(Guid PlantId, InspectionType? Type, InspectionStatus? Status, Guid? OrderId, Guid? ItemId, string? Search);

public interface IInspectionRepository
{
    /// <summary>An inspection with its results, tracked.</summary>
    Task<Inspection?> GetAsync(Guid id, CancellationToken ct = default);

    Task<(List<Inspection> Items, int Total)> ListAsync(InspectionFilter filter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Every inspection of an order (no results), newest first.</summary>
    Task<List<Inspection>> ListForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>The inspections (first ones and re-inspections) of these receipt lines, tracked.</summary>
    Task<List<Inspection>> ListForReceiptLinesAsync(IReadOnlyCollection<Guid> lineIds, CancellationToken ct = default);

    /// <summary>The inspections (first one and re-inspections) of an operation report, tracked.</summary>
    Task<List<Inspection>> ListForReportAsync(Guid reportId, CancellationToken ct = default);

    /// <summary>Inspections of the order that still decide something: Pending, or Failed and not re-inspected.</summary>
    Task<int> CountOpenAsync(Guid orderId, CancellationToken ct = default);

    void Add(Inspection inspection);
}
