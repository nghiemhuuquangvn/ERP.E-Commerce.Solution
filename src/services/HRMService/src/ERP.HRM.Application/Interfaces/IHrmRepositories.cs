using ERP.HRM.Domain.Entities;

namespace ERP.HRM.Application.Interfaces;

/// <summary>
/// Read/write access to the local EmployeeSnapshot projection (see
/// EmployeeSnapshot.cs) — not a real Employee repository; HRM no longer owns
/// that aggregate.
/// </summary>
public interface IEmployeeSnapshotRepository
{
    Task<EmployeeSnapshot?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeSnapshot>> GetActiveAsync(CancellationToken cancellationToken = default);
    void Upsert(EmployeeSnapshot snapshot);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ILeaveRequestRepository
{
    Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LeaveRequest> Items, int TotalCount)> ListAsync(
        Guid? employeeId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    void Add(LeaveRequest leaveRequest);
    void Update(LeaveRequest leaveRequest);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPaySlipRepository
{
    Task<PaySlip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PaySlip> Items, int TotalCount)> ListAsync(
        Guid? employeeId,
        int? year,
        int? month,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsForPeriodAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken = default);
    void Add(PaySlip paySlip);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
