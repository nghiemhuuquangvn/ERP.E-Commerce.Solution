using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

/// <summary>Filters of the order list (BA spec 01 T3.2.4). Dates are due dates, both ends inclusive.</summary>
public sealed record OrderFilter(Guid PlantId, OrderStatus? Status, Guid? ItemId, DateOnly? DueFrom, DateOnly? DueTo, bool? IsLate, int? Priority, string? Search);

public interface IProductionOrderRepository
{
    /// <summary>The order with its materials, operations, outputs and history (not deleted), tracked.</summary>
    Task<ProductionOrder?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>A page of order headers (no lines), newest due date first within the filter.</summary>
    Task<(List<ProductionOrder> Items, int Total)> ListAsync(OrderFilter filter, int page, int pageSize, CancellationToken ct = default);

    void Add(ProductionOrder order);

    /// <summary>
    /// Orders whose Warehouse reservation must be brought in line, across all tenants (no user, no tenant scope): the flag is set and the next attempt is due.
    /// The caller processes each inside <c>TenantScope.Begin(tenantId)</c>.
    /// </summary>
    Task<List<(Guid TenantId, Guid OrderId)>> ListPendingReservationSyncAsync(DateTime nowUtc, int take, CancellationToken ct = default);
}

/// <summary>"Today" and "now" in the time zone of a plant (BR-GEN-05): due dates, document numbers and the schedule all speak plant time.</summary>
public interface IPlantClock
{
    DateTime UtcNow { get; }

    /// <summary>The plant's wall-clock time now (Kind unspecified).</summary>
    DateTime LocalNow(Plant plant);

    DateOnly Today(Plant plant);

    /// <summary>A UTC time as plant wall-clock time (Kind unspecified).</summary>
    DateTime ToLocal(Plant plant, DateTime utc);

    /// <summary>A plant wall-clock time as UTC.</summary>
    DateTime ToUtc(Plant plant, DateTime local);
}
