using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Operations;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Common;

namespace ERP.MFG.UnitTests.Orders;

internal sealed class ReportRepoFake : IOperationReportRepository
{
    public readonly List<OperationReport> Items = [];

    public Task<OperationReport?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(r => r.Id == id && !r.IsDeleted));
    public Task<OperationReport?> FindByKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(r => r.IdempotencyKey == key));
    public Task<List<OperationReport>> ListForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult(Items.Where(r => r.OrderId == orderId).OrderBy(r => r.ReportedAt).ToList());
    public Task<List<OperationReport>> ListPendingForOrderAsync(Guid orderId, CancellationToken ct = default)
        => Task.FromResult(Items.Where(r => r.OrderId == orderId && r.Status == OperationReportStatus.Pending).ToList());
    public void Add(OperationReport report) => Items.Add(report);
}

internal sealed class TimeLogRepoFake : ITimeLogRepository
{
    public readonly List<LaborTimeLog> Items = [];

    public Task<LaborTimeLog?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(l => l.Id == id && !l.IsDeleted));
    public Task<List<LaborTimeLog>> ListForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult(Items.Where(l => l.OrderId == orderId).OrderBy(l => l.StartAt).ToList());
    public Task<List<LaborTimeLog>> ListOpenForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult(Items.Where(l => l.OrderId == orderId && l.EndAt == null).ToList());
    public Task<List<LaborTimeLog>> ListOpenForOperationAsync(Guid operationId, CancellationToken ct = default) => Task.FromResult(Items.Where(l => l.OrderOperationId == operationId && l.EndAt == null).ToList());
    public Task<bool> HasOverlapAsync(Guid employeeId, DateTime start, DateTime? end, CancellationToken ct = default) => Task.FromResult(Items.Any(l => l.EmployeeId == employeeId && l.Overlaps(start, end)));
    public void Add(LaborTimeLog log) => Items.Add(log);
}

/// <summary>DS-01, a released order, a fake Warehouse and the P3-4 handlers (test cases TC-OP-*, TC-BF-*).</summary>
internal sealed class OpsCtx : StockCtx
{
    public Guid Emp1 { get; }
    public Guid Emp2 { get; }
    public ReasonCode ScrapReason { get; }
    public ReasonCode ReworkReason { get; }

    public OpsCtx()
    {
        Emp1 = F.AddEmployee("NV001");
        Emp2 = F.AddEmployee("NV002");
        ScrapReason = ReasonCode.Create(ReasonType.Scrap, "SCRAP-01", "Scratched", null);
        ReworkReason = ReasonCode.Create(ReasonType.Rework, "RWK-01", "Repaint", null);
        F.Reasons.Add(ScrapReason);
        F.Reasons.Add(ReworkReason);
    }

    public BackflushPlanner Planner => new(Stock, F.ProductClient, F.UnitClient);
    public OperationLogCloser Closer => new(TimeLogs, PlantTime);
    public ILaborRateResolver Rates => new LaborRateResolver(F.CrewRepo, F.Catalog);

    public Task<LaborTimeLogDto> StartAsync(ProductionOrder order, int seq, Guid? employee = null, Guid? machine = null)
        => new StartOperationHandler(Access, Repo, TimeLogs, F.EmployeeClient, F.Catalog, Rates, PlantTime, User, F.Uow)
            .Handle(new StartOperationCommand(order.Id, seq, employee ?? Emp1, machine), default);

    public Task<LaborTimeLogDto> PauseLogAsync(Guid logId) => new PauseTimeLogHandler(Access, Repo, TimeLogs, PlantTime, F.Uow).Handle(new PauseTimeLogCommand(logId), default);
    public Task<LaborTimeLogDto> ResumeLogAsync(Guid logId) => new ResumeTimeLogHandler(Access, Repo, TimeLogs, PlantTime, F.Uow).Handle(new ResumeTimeLogCommand(logId), default);
    public Task<LaborTimeLogDto> StopLogAsync(Guid logId) => new StopTimeLogHandler(Access, Repo, TimeLogs, PlantTime, F.Uow).Handle(new StopTimeLogCommand(logId), default);

    public Task<LaborTimeLogDto> ManualTimeAsync(ProductionOrder order, int seq, DateTime start, DateTime end, Guid? employee = null)
        => new AddManualTimeHandler(Access, Repo, TimeLogs, F.EmployeeClient, F.Catalog, Rates, PlantTime, User, F.Uow)
            .Handle(new AddManualTimeCommand(order.Id, seq, employee ?? Emp1, null, start, end), default);

    public Task<OperationReportResultDto> ReportAsync(ProductionOrder order, int seq, decimal good, decimal scrap = 0, decimal rework = 0, string? key = null,
        IStockPostingService? posting = null, Guid? machine = null, bool omitReasons = false)
        => new ReportOperationHandler(Access, Repo, Reports, Documents, PlantSettings, F.ReasonRepo, F.UnitClient, F.Catalog, Planner, Closer, Numbers, PlantTime, posting ?? Posting, User, F.Uow)
            .Handle(new ReportOperationCommand(order.Id, seq, good, scrap, rework, scrap > 0 && !omitReasons ? ScrapReason.Id : null, rework > 0 && !omitReasons ? ReworkReason.Id : null, machine, null, key), default);

    public Task<OrderOperationDto> CompleteOperationAsync(ProductionOrder order, int seq)
        => new CompleteOperationHandler(Access, Repo, Closer, PlantTime, User, F.Uow).Handle(new CompleteOperationCommand(order.Id, seq), default);

    public Task<OrderOperationDto> ReopenOperationAsync(ProductionOrder order, int seq)
        => new ReopenOperationHandler(Access, Repo, User, F.Uow).Handle(new ReopenOperationCommand(order.Id, seq), default);

    public ProductionOrderOperation Op(ProductionOrder order, int seq) => order.OperationOf(seq);

    /// <summary>The Warehouse movements booked for the item's backflush documents.</summary>
    public IEnumerable<FakeStock.Booked> BackflushMovements(string component)
        => Stock.Movements.Where(m => m.ProductId == Items[component].Id && m.Key.StartsWith("BF-"));
}
