using ERP.HRM.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.HRM.Infrastructure.Persistence;

public class HrmUnitOfWork : IHrmUnitOfWork
{
    private readonly HrmDbContext _dbContext;
    private IDbContextTransaction? _currentTrans;

    public HrmUnitOfWork(
        HrmDbContext dbContext,
        ILeaveRequestRepository leaveRequests,
        IPaySlipRepository paySlips,
        IAttendanceRepository attendance,
        IWorkShiftRepository workShifts,
        IEmployeeSnapshotRepository employeeSnapshots)
    {
        _dbContext = dbContext;
        LeaveRequests = leaveRequests;
        PaySlips = paySlips;
        Attendance = attendance;
        WorkShifts = workShifts;
        EmployeeSnapshots = employeeSnapshots;
    }

    public ILeaveRequestRepository LeaveRequests { get; }
    public IPaySlipRepository PaySlips { get; }
    public IAttendanceRepository Attendance { get; }
    public IWorkShiftRepository WorkShifts { get; }
    public IEmployeeSnapshotRepository EmployeeSnapshots { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await _dbContext.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _currentTrans = await _dbContext.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            if (_currentTrans != null) await _currentTrans.CommitAsync(ct);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
        finally
        {
            if (_currentTrans != null)
            {
                _currentTrans.Dispose();
                _currentTrans = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTrans != null)
        {
            await _currentTrans.RollbackAsync(ct);
            _currentTrans.Dispose();
            _currentTrans = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbContext.Dispose();
            _currentTrans?.Dispose();
        }
    }
}
