using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

public interface IRoutingRepository
{
    /// <summary>A routing with its operations (not deleted).</summary>
    Task<Routing?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<Routing>> ListAsync(Guid plantId, Guid? itemId, VersionStatus? status, CancellationToken ct = default);

    /// <summary>The approved versions of the plant, with operations.</summary>
    Task<List<Routing>> ListApprovedAsync(Guid plantId, CancellationToken ct = default);

    Task<int> MaxVersionAsync(Guid itemId, Guid plantId, CancellationToken ct = default);

    /// <summary>Is a work center used by an operation of any routing (BR-GEN-02)?</summary>
    Task<bool> WorkCenterIsUsedAsync(Guid workCenterId, CancellationToken ct = default);

    void Add(Routing routing);
}

public interface IRoutingResolver
{
    /// <summary>The routing that applies to an item on a date, or null (an item may have none, BR-RT-07).</summary>
    Task<Routing?> GetEffectiveRoutingAsync(Guid itemId, Guid plantId, DateOnly date, CancellationToken ct = default);
}

public interface IOperationTimeService
{
    /// <summary>BR-RT-04 / -05: the lead time of a routing for a quantity, scheduled backwards from the end of <paramref name="endDate"/>'s work.</summary>
    Task<RoutingLeadTime> LeadTimeAsync(Routing routing, decimal quantity, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// The operations of a routing for a quantity with their work-center efficiency and calendar (the input of both the backward lead time and the forward
    /// scheduling of a production order). <c>MFG_RT_003</c> when a work center no longer exists.
    /// </summary>
    Task<IReadOnlyList<ScheduledOperationInput>> BuildInputsAsync(Routing routing, decimal quantity, CancellationToken ct = default);
}
