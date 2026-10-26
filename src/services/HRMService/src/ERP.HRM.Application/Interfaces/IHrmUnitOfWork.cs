namespace ERP.HRM.Application.Interfaces;

public interface IHrmUnitOfWork : IDisposable
{
    ILeaveRequestRepository LeaveRequests { get; }
    IPaySlipRepository PaySlips { get; }
    IAttendanceRepository Attendance { get; }
    IWorkShiftRepository WorkShifts { get; }
    IEmployeeSnapshotRepository EmployeeSnapshots { get; }
    IPayrollRepository Payroll { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
