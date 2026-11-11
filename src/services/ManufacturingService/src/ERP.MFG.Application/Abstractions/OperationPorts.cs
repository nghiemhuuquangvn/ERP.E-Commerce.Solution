using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Abstractions;

public interface IOperationReportRepository
{
    /// <summary>The report, tracked.</summary>
    Task<OperationReport?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The report created with this Idempotency-Key (tenant scope), if any.</summary>
    Task<OperationReport?> FindByKeyAsync(string idempotencyKey, CancellationToken ct = default);

    Task<List<OperationReport>> ListForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Reports of the order that are still waiting for their backflush document: their quantities already count toward the limits of the next report. Reversal reports are not among them (see <see cref="ListPendingReversalsForOrderAsync"/>).</summary>
    Task<List<OperationReport>> ListPendingForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Reversal reports of the order that wait for the Warehouse to take the consumed material back (P4-2): their quantities are about to leave the operations.</summary>
    Task<List<OperationReport>> ListPendingReversalsForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>The reversal of this report that is Pending or Posted (a Failed one does not count), tracked; null when none.</summary>
    Task<OperationReport?> FindReversalOfAsync(Guid originalReportId, CancellationToken ct = default);

    void Add(OperationReport report);
}

public interface ITimeLogRepository
{
    Task<LaborTimeLog?> GetAsync(Guid id, CancellationToken ct = default);

    Task<List<LaborTimeLog>> ListForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Logs of the order that are not stopped, tracked.</summary>
    Task<List<LaborTimeLog>> ListOpenForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Logs of the operation that are not stopped, tracked.</summary>
    Task<List<LaborTimeLog>> ListOpenForOperationAsync(Guid operationId, CancellationToken ct = default);

    /// <summary>BR-OP-05: does the employee have a log that shares time with [start, end)? An open log runs to the end of time; <paramref name="end"/> null = open ended.</summary>
    Task<bool> HasOverlapAsync(Guid employeeId, DateTime start, DateTime? end, CancellationToken ct = default);

    void Add(LaborTimeLog log);
}
