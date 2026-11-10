using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MFG.Infrastructure.Repositories;

public sealed class OperationReportRepository(MfgDbContext db) : IOperationReportRepository
{
    public Task<OperationReport?> GetAsync(Guid id, CancellationToken ct = default)
        => db.OperationReports.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    public Task<OperationReport?> FindByKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => db.OperationReports.FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey && !r.IsDeleted, ct);

    public Task<List<OperationReport>> ListForOrderAsync(Guid orderId, CancellationToken ct = default)
        => db.OperationReports.AsNoTracking().Where(r => r.OrderId == orderId && !r.IsDeleted).OrderBy(r => r.ReportedAt).ThenBy(r => r.DocNo).ToListAsync(ct);

    public Task<List<OperationReport>> ListPendingForOrderAsync(Guid orderId, CancellationToken ct = default)
        => db.OperationReports.Where(r => r.OrderId == orderId && !r.IsDeleted && r.Status == OperationReportStatus.Pending).ToListAsync(ct);

    public void Add(OperationReport report) => db.OperationReports.Add(report);
}

public sealed class TimeLogRepository(MfgDbContext db) : ITimeLogRepository
{
    public Task<LaborTimeLog?> GetAsync(Guid id, CancellationToken ct = default)
        => db.LaborTimeLogs.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct);

    public Task<List<LaborTimeLog>> ListForOrderAsync(Guid orderId, CancellationToken ct = default)
        => db.LaborTimeLogs.AsNoTracking().Where(l => l.OrderId == orderId && !l.IsDeleted).OrderBy(l => l.StartAt).ToListAsync(ct);

    public Task<List<LaborTimeLog>> ListOpenForOrderAsync(Guid orderId, CancellationToken ct = default)
        => db.LaborTimeLogs.Where(l => l.OrderId == orderId && !l.IsDeleted && l.EndAt == null).ToListAsync(ct);

    public Task<List<LaborTimeLog>> ListOpenForOperationAsync(Guid operationId, CancellationToken ct = default)
        => db.LaborTimeLogs.Where(l => l.OrderOperationId == operationId && !l.IsDeleted && l.EndAt == null).ToListAsync(ct);

    public Task<bool> HasOverlapAsync(Guid employeeId, DateTime start, DateTime? end, CancellationToken ct = default)
        => db.LaborTimeLogs.AnyAsync(l => l.EmployeeId == employeeId && !l.IsDeleted
            && (end == null || l.StartAt < end) && (l.EndAt == null || l.EndAt > start), ct);

    public void Add(LaborTimeLog log) => db.LaborTimeLogs.Add(log);
}
