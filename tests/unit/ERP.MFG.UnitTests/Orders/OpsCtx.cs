using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Operations;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Features.Receipts;
using ERP.MFG.Application.Features.StockDocuments;
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
        => Task.FromResult(Items.Where(r => r.OrderId == orderId && r.Status == OperationReportStatus.Pending && r.ReversalOfId == null).ToList());
    public Task<List<OperationReport>> ListPendingReversalsForOrderAsync(Guid orderId, CancellationToken ct = default)
        => Task.FromResult(Items.Where(r => r.OrderId == orderId && r.Status == OperationReportStatus.Pending && r.ReversalOfId != null).ToList());
    public Task<OperationReport?> FindReversalOfAsync(Guid originalReportId, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(r => r.ReversalOfId == originalReportId && r.Status != OperationReportStatus.Failed));
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
internal class OpsCtx : StockCtx
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
        ShortCloseReason = ReasonCode.Create(ReasonType.ShortClose, "SC-01", "Customer accepts less", null);
        F.Reasons.Add(ShortCloseReason);
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
        => new ReportOperationHandler(Access, Repo, Reports, Documents, PlantSettings, F.ReasonRepo, F.UnitClient, F.Catalog, Planner, Closer, Numbers, Quality, PlantTime, posting ?? Posting, User, F.Uow)
            .Handle(new ReportOperationCommand(order.Id, seq, good, scrap, rework, scrap > 0 && !omitReasons ? ScrapReason.Id : null, rework > 0 && !omitReasons ? ReworkReason.Id : null, machine, null, key), default);

    public Task<OrderOperationDto> CompleteOperationAsync(ProductionOrder order, int seq)
        => new CompleteOperationHandler(Access, Repo, Closer, PlantTime, User, F.Uow).Handle(new CompleteOperationCommand(order.Id, seq), default);

    public Task<OrderOperationDto> ReopenOperationAsync(ProductionOrder order, int seq)
        => new ReopenOperationHandler(Access, Repo, User, F.Uow).Handle(new ReopenOperationCommand(order.Id, seq), default);

    // ── Finished goods, complete, reopen, close (P3-5) ──────────────────────────────────────────────────────

    public Task<StockDocumentResultDto> ReceiveAsync(ProductionOrder order, IReadOnlyList<ReceiptLineBody> lines, string? key = null, IStockPostingService? posting = null)
        => new ReceiveFinishedGoodsHandler(Access, Repo, Documents, Reports, Stock, F.ProductClient, F.UnitClient, F.ProfileRepo, PlantSettings, Planner, Numbers, PlantTime, posting ?? Posting, User, F.Uow)
            .Handle(new ReceiveFinishedGoodsCommand(order.Id, lines, null, key), default);

    public Task<StockDocumentResultDto> ReceiveMainAsync(ProductionOrder order, decimal quantity, string? lot = null, string? key = null)
        => ReceiveAsync(order, [new ReceiptLineBody(null, quantity, lot)], key);

    public Task<OrderActionResultDto> CompleteOrderAsync(ProductionOrder order, Guid? reason = null, params string[] permissions)
        => new CompleteOrderHandler(Access, Repo, Documents, Reports, F.ReasonRepo, PlantSettings, Closer, Sync, PlantTime, permissions.Length == 0 ? User : UserWith(permissions), F.Uow)
            .Handle(new CompleteOrderCommand(order.Id, reason, "done"), default);

    public Task<OrderActionResultDto> ReopenOrderAsync(ProductionOrder order)
        => new ReopenOrderHandler(Access, Repo, Sync, User, F.Uow).Handle(new ReopenOrderCommand(order.Id), default);

    public Task<CloseChecklistDto> ChecklistAsync(ProductionOrder order, bool confirm = false)
        => new GetCloseChecklistHandler(Access, Repo, TimeLogs, Documents, Reports).Handle(new GetCloseChecklistQuery(order.Id, confirm), default);

    public Task<OrderDto> CloseOrderAsync(ProductionOrder order, bool confirm = false)
        => new CloseOrderHandler(Access, Repo, TimeLogs, Documents, Reports, User, F.Uow).Handle(new CloseOrderCommand(order.Id, confirm), default);

    public Task<StockDocumentDto> QcReleaseAsync(Guid documentId)
        => new QcReleaseReceiptHandler(Access, Documents, Repo, F.InspectionRepo, Quarantine, PlantTime, F.Uow).Handle(new QcReleaseReceiptCommand(documentId), default);

    public ReasonCode ShortCloseReason { get; set; } = null!;

    // ── Reversal (P4-2) ─────────────────────────────────────────────────────────────────────────────────────

    public Task<OperationReportResultDto> ReverseReportAsync(ProductionOrder order, Guid reportId, string? note = "wrong quantity", string? key = null, IStockPostingService? posting = null)
        => new ERP.MFG.Application.Features.Reversals.ReverseOperationReportHandler(Access, Repo, Reports, Documents, PlantSettings, Quality, Numbers, PlantTime, posting ?? Posting, User, F.Uow)
            .Handle(new ERP.MFG.Application.Features.Reversals.ReverseOperationReportCommand(order.Id, reportId, note, key), default);

    public Task<StockDocumentResultDto> ReverseReceiptAsync(ProductionOrder order, Guid documentId, string? note = "wrong receipt", string? key = null, IStockPostingService? posting = null)
        => new ERP.MFG.Application.Features.Reversals.ReverseReceiptHandler(Access, Repo, Documents, Reports, Stock, Quality, Numbers, PlantTime, posting ?? Posting, User, F.Uow)
            .Handle(new ERP.MFG.Application.Features.Reversals.ReverseReceiptCommand(order.Id, documentId, note, key), default);

    /// <summary>Makes TP-BAN01 a lot-controlled finished good that may need final inspection, with a shelf life.</summary>
    public void FinishedGoodIs(bool lotControlled, bool fqc, int shelfLifeDays = 0, string code = "TP-BAN01")
    {
        Replace(code, p => p with { IsLotControlled = lotControlled });
        var item = Items[code];
        F.Profiles.RemoveAll(p => p.ItemId == item.Id);
        F.Profiles.Add(ItemManufacturingProfile.Create(item.Id, Plant.Id, item.Sku, item.Name, ItemManufacturingProfile.DefaultValues with { RequiresFqc = fqc, ShelfLifeDays = shelfLifeDays }));
    }

    /// <summary>Reports Good on every operation of the DS-01 route so that the order can receive that quantity.</summary>
    public async Task ProduceAsync(ProductionOrder order, decimal good)
    {
        foreach (var seq in new[] { 10, 20, 30 }) await ReportAsync(order, seq, good);
    }

    public ProductionOrderOperation Op(ProductionOrder order, int seq) => order.OperationOf(seq);

    /// <summary>The Warehouse movements booked for the item's backflush documents.</summary>
    public IEnumerable<FakeStock.Booked> BackflushMovements(string component)
        => Stock.Movements.Where(m => m.ProductId == Items[component].Id && m.Key.StartsWith("BF-"));
}
