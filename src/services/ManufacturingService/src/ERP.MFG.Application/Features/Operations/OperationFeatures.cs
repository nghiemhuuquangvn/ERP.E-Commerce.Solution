using System.Security.Cryptography;
using System.Text;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Features.Reversals;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MFG.Application.Features.Operations;

// ── DTOs ─────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record LaborTimeLogDto(
    Guid Id, Guid OrderId, int OperationSeq, Guid EmployeeId, string EmployeeNumber, string EmployeeName, Guid? MachineId, DateTime StartAt, DateTime? EndAt, DateTime? PausedAt,
    decimal PausedMinutes, decimal? DurationMinutes, decimal? LaborRatePerHour, bool IsManual, string State)
{
    public static LaborTimeLogDto From(LaborTimeLog l) => new(l.Id, l.OrderId, l.OperationSeq, l.EmployeeId, l.EmployeeNumber, l.EmployeeName, l.MachineId, l.StartAt, l.EndAt, l.PausedAt,
        l.PausedMinutes, l.DurationMinutes, l.LaborRatePerHour, l.IsManual, l.IsRunning ? "Running" : l.IsPaused ? "Paused" : "Stopped");
}

public sealed record OperationReportDto(
    Guid Id, string DocNo, Guid PlantId, Guid OrderId, string OrderNo, int OperationSeq, DateTime ReportedAt, DateOnly PostingDate, decimal GoodQty, decimal ScrapQty, decimal ReworkQty,
    Guid? ScrapReasonId, Guid? ReworkReasonId, Guid? MachineId, string ReportedBy, OperationReportStatus Status, Guid? BackflushDocumentId, string? FailureCode, string? FailureMessage,
    string? Note, DateTime? PostedAt, Guid? ReversalOfId = null, bool IsReversed = false)
{
    public static OperationReportDto From(OperationReport r) => new(r.Id, r.DocNo, r.PlantId, r.OrderId, r.OrderNo, r.OperationSeq, r.ReportedAt, r.PostingDate, r.GoodQty, r.ScrapQty,
        r.ReworkQty, r.ScrapReasonId, r.ReworkReasonId, r.MachineId, r.ReportedBy, r.Status, r.BackflushDocumentId, r.FailureCode, r.FailureMessage, r.Note, r.PostedAt, r.ReversalOfId, r.IsReversed);
}

/// <summary>The answer of a report: 201 when it counts, 202 when it waits for its backflush document to reach the Warehouse.</summary>
public sealed record OperationReportResultDto(OperationReportDto Report, bool IsPending, StockDocumentDto? Backflush);

public sealed record OperationDetailDto(OrderOperationDto Operation, IReadOnlyList<LaborTimeLogDto> TimeLogs);

// ── Shared checks ────────────────────────────────────────────────────────────────────────────────────────────

internal static class OperationWork
{
    /// <summary>An active employee (MasterData), optionally on a usable machine of the operation's work center (BR-MCH-02, <c>MFG_OP_010</c>), with the labor rate of the day.</summary>
    public static async Task<TimeLogWorker> WorkerAsync(Plant plant, ProductionOrderOperation op, Guid employeeId, Guid? machineId, DateOnly day,
        IEmployeeLookupClient employees, IPlantCatalog catalog, ILaborRateResolver rates, CancellationToken ct)
    {
        var employee = (await employees.LookupAsync([employeeId], ct)).FirstOrDefault() ?? throw MfgRules.Invalid("The employee does not exist.");
        if (!employee.IsActive) throw MfgRules.Invalid($"The employee {employee.EmployeeNumber} is not active.");
        await EnsureMachineAsync(plant, op, machineId, catalog, ct);
        var rate = await rates.GetRateAsync(employeeId, op.WorkCenterId, day, ct);
        return new TimeLogWorker(employee.Id, employee.EmployeeNumber, employee.FullName, machineId == Guid.Empty ? null : machineId, rate);
    }

    public static async Task EnsureMachineAsync(Plant plant, ProductionOrderOperation op, Guid? machineId, IPlantCatalog catalog, CancellationToken ct)
    {
        if (machineId is null || machineId == Guid.Empty) return;
        var machine = await catalog.GetAsync<Machine>(machineId.Value, ct);
        if (machine is null || machine.PlantId != plant.Id || machine.WorkCenterId != op.WorkCenterId)
            throw MfgRules.Invalid("The machine does not belong to the work center of this operation.");
        machine.AssertUsable();                                                // Retired -> MFG_OP_010; under maintenance is allowed
    }

    /// <summary>The operation follows its logs after one of them changed (the tracked log objects carry the new state).</summary>
    public static async Task FollowLogsAsync(ITimeLogRepository logs, ProductionOrderOperation op, CancellationToken ct)
    {
        var open = (await logs.ListOpenForOperationAsync(op.Id, ct)).Where(l => l.IsOpen).ToList();
        op.FollowLogs(open.Count(l => l.IsRunning), open.Count);
    }

    public static async Task<(LaborTimeLog Log, ProductionOrder Order)> LoadLogAsync(Guid id, ITimeLogRepository logs, IProductionOrderRepository orders, IPlantAccessService access, CancellationToken ct)
    {
        var log = await logs.GetAsync(id, ct) ?? throw new EntityNotFoundException(nameof(LaborTimeLog), id);
        var order = await OrderScope.LoadAsync(orders, access, log.OrderId, ct);
        return (log, order);
    }

    public static string Hash(Guid orderId, int seq, decimal good, decimal scrap, decimal rework, Guid? scrapReason, Guid? reworkReason, Guid? machine, string? note)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{orderId}|{seq}|{good:0.######}|{scrap:0.######}|{rework:0.######}|{scrapReason}|{reworkReason}|{machine}|{note?.Trim()}")));
}

// ── Time logs (BR-OP-02, BR-OP-05) ───────────────────────────────────────────────────────────────────────────

public sealed record StartOperationCommand(Guid OrderId, int OperationSeq, Guid EmployeeId, Guid? MachineId) : IRequest<LaborTimeLogDto>;

public sealed class StartOperationHandler(
    IPlantAccessService access, IProductionOrderRepository orders, ITimeLogRepository logs, IEmployeeLookupClient employees, IPlantCatalog catalog, ILaborRateResolver rates,
    IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<StartOperationCommand, LaborTimeLogDto>
{
    public async Task<LaborTimeLogDto> Handle(StartOperationCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var op = order.OperationOf(r.OperationSeq);
        var now = clock.UtcNow;

        order.StartOperationWork(r.OperationSeq, user.GetUserId(), now);        // BR-OP-02 / BR-PO-10: state checks first, nothing is saved yet
        var worker = await OperationWork.WorkerAsync(plant, op, r.EmployeeId, r.MachineId, clock.Today(plant), employees, catalog, rates, ct);
        MfgRules.ThrowIf(await logs.HasOverlapAsync(worker.EmployeeId, now, null, ct), MfgErrors.Op006, ("employee", worker.EmployeeNumber));

        var log = LaborTimeLog.StartNow(order.Id, op.Id, op.OperationSeq, worker, now, user.GetUserId());
        logs.Add(log);
        await uow.SaveChangesAsync(ct);
        return LaborTimeLogDto.From(log);
    }
}

public sealed record PauseTimeLogCommand(Guid LogId) : IRequest<LaborTimeLogDto>;

public sealed class PauseTimeLogHandler(IPlantAccessService access, IProductionOrderRepository orders, ITimeLogRepository logs, IPlantClock clock, IMfgUnitOfWork uow)
    : IRequestHandler<PauseTimeLogCommand, LaborTimeLogDto>
{
    public async Task<LaborTimeLogDto> Handle(PauseTimeLogCommand r, CancellationToken ct)
    {
        var (log, order) = await OperationWork.LoadLogAsync(r.LogId, logs, orders, access, ct);
        order.EnsureEditable();
        log.Pause(clock.UtcNow);
        await OperationWork.FollowLogsAsync(logs, order.OperationOf(log.OperationSeq), ct);
        await uow.SaveChangesAsync(ct);
        return LaborTimeLogDto.From(log);
    }
}

public sealed record ResumeTimeLogCommand(Guid LogId) : IRequest<LaborTimeLogDto>;

public sealed class ResumeTimeLogHandler(IPlantAccessService access, IProductionOrderRepository orders, ITimeLogRepository logs, IPlantClock clock, IMfgUnitOfWork uow)
    : IRequestHandler<ResumeTimeLogCommand, LaborTimeLogDto>
{
    public async Task<LaborTimeLogDto> Handle(ResumeTimeLogCommand r, CancellationToken ct)
    {
        var (log, order) = await OperationWork.LoadLogAsync(r.LogId, logs, orders, access, ct);
        order.EnsureOpenForWork();                                              // an order on hold stays paused (BR-PO-11)
        log.Resume(clock.UtcNow);
        await OperationWork.FollowLogsAsync(logs, order.OperationOf(log.OperationSeq), ct);
        await uow.SaveChangesAsync(ct);
        return LaborTimeLogDto.From(log);
    }
}

public sealed record StopTimeLogCommand(Guid LogId) : IRequest<LaborTimeLogDto>;

public sealed class StopTimeLogHandler(IPlantAccessService access, IProductionOrderRepository orders, ITimeLogRepository logs, IPlantClock clock, IMfgUnitOfWork uow)
    : IRequestHandler<StopTimeLogCommand, LaborTimeLogDto>
{
    public async Task<LaborTimeLogDto> Handle(StopTimeLogCommand r, CancellationToken ct)
    {
        var (log, order) = await OperationWork.LoadLogAsync(r.LogId, logs, orders, access, ct);
        order.EnsureEditable();
        log.Stop(clock.UtcNow);
        await OperationWork.FollowLogsAsync(logs, order.OperationOf(log.OperationSeq), ct);
        await uow.SaveChangesAsync(ct);
        return LaborTimeLogDto.From(log);
    }
}

/// <summary>BR-OP-05: a finished session entered afterwards.</summary>
public sealed record AddManualTimeCommand(Guid OrderId, int OperationSeq, Guid EmployeeId, Guid? MachineId, DateTime StartAt, DateTime EndAt) : IRequest<LaborTimeLogDto>;

public sealed class AddManualTimeHandler(
    IPlantAccessService access, IProductionOrderRepository orders, ITimeLogRepository logs, IEmployeeLookupClient employees, IPlantCatalog catalog, ILaborRateResolver rates,
    IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<AddManualTimeCommand, LaborTimeLogDto>
{
    public async Task<LaborTimeLogDto> Handle(AddManualTimeCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        order.EnsureEditable();
        MfgRules.ThrowIf(order.Status is OrderStatus.Draft, MfgErrors.Op001);
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var op = order.OperationOf(r.OperationSeq);
        var start = AsUtc(r.StartAt);
        var end = AsUtc(r.EndAt);

        var worker = await OperationWork.WorkerAsync(plant, op, r.EmployeeId, r.MachineId, DateOnly.FromDateTime(clock.ToLocal(plant, start)), employees, catalog, rates, ct);
        var log = LaborTimeLog.Manual(order.Id, op.Id, op.OperationSeq, worker, start, end, clock.UtcNow, user.GetUserId());   // End > Start, Start <= now: MFG_OP_005
        MfgRules.ThrowIf(await logs.HasOverlapAsync(worker.EmployeeId, start, end, ct), MfgErrors.Op006, ("employee", worker.EmployeeNumber));
        logs.Add(log);
        await uow.SaveChangesAsync(ct);
        return LaborTimeLogDto.From(log);
    }

    /// <summary>A time with an offset arrives as Local, one without as Unspecified (taken as UTC).</summary>
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

// ── Operation report (BR-OP-03, 04, 06; BR-MAT-08) ───────────────────────────────────────────────────────────

public sealed record ReportOperationCommand(
    Guid OrderId, int OperationSeq, decimal GoodQty, decimal ScrapQty, decimal ReworkQty, Guid? ScrapReasonId, Guid? ReworkReasonId, Guid? MachineId, string? Note, string? IdempotencyKey)
    : IRequest<OperationReportResultDto>;

public sealed class ReportOperationHandler(
    IPlantAccessService access, IProductionOrderRepository orders, IOperationReportRepository reports, IStockDocumentRepository documents, IProductionSettingProvider settings,
    IReasonCodeRepository reasons, IUnitOfMeasureClient units, IPlantCatalog catalog, BackflushPlanner planner, OperationLogCloser logCloser, IDocumentNumberService numbers,
    IPlantClock clock, IStockPostingService posting, IUserContextService user, IMfgUnitOfWork uow) : IRequestHandler<ReportOperationCommand, OperationReportResultDto>
{
    public async Task<OperationReportResultDto> Handle(ReportOperationCommand r, CancellationToken ct)
    {
        var hash = OperationWork.Hash(r.OrderId, r.OperationSeq, r.GoodQty, r.ScrapQty, r.ReworkQty, r.ScrapReasonId, r.ReworkReasonId, r.MachineId, r.Note);
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var repeat = await FindRepeatAsync(r.IdempotencyKey, hash, order.Id, ct);
        if (repeat is not null) return await AnswerAsync(repeat, ct);

        order.EnsureOpenForWork();                                                 // order open (OnHold: MFG_PO_011, other: MFG_OP_001)
        await ReversalGuards.EnsureNoPendingReversalAsync(documents, reports, order.Id, ct);   // P4-2: a reversal of this order is waiting for the Warehouse (MFG_REV_002)
        var plant = await access.GetVisiblePlantAsync(order.PlantId, ct);
        var op = order.OperationOf(r.OperationSeq);
        MfgRules.ThrowIf(op.Status == OperationStatus.Pending, MfgErrors.Op001);
        var by = user.GetUserId();
        var now = clock.UtcNow;
        var today = clock.Today(plant);

        // BR-OP-03: quantities and reasons
        MfgRules.ThrowIf(r.GoodQty < 0 || r.ScrapQty < 0 || r.ReworkQty < 0 || r.GoodQty + r.ScrapQty + r.ReworkQty <= 0, MfgErrors.Op002);
        var places = (await units.LookupAsync([order.UomId], ct)).FirstOrDefault()?.DecimalPlaces ?? 6;
        foreach (var qty in new[] { r.GoodQty, r.ScrapQty, r.ReworkQty })
            MfgRules.ThrowIf(Math.Round(qty, places) != qty, MfgErrors.Op002);
        if (r.ScrapQty > 0) await EnsureReasonAsync(r.ScrapReasonId, ReasonType.Scrap, ct);
        if (r.ReworkQty > 0) await EnsureReasonAsync(r.ReworkReasonId, ReasonType.Rework, ct);
        await OperationWork.EnsureMachineAsync(plant, op, r.MachineId, catalog, ct);

        // BR-OP-04: the input the operation may take, counting reports that are still waiting for their backflush
        var setting = await settings.GetAsync(plant.Id, ct);
        var pending = await reports.ListPendingForOrderAsync(order.Id, ct);
        var previous = order.PreviousOperation(op.OperationSeq);
        var previousGood = previous is null ? 0m : previous.GoodQty + pending.Where(p => p.OperationSeq == previous.OperationSeq).Sum(p => p.GoodQty);
        var available = OperationInput.Available(previous is null, order.Quantity, setting.OverProductionTolerancePct, previousGood);
        var taken = op.TakenQty + pending.Where(p => p.OperationSeq == op.OperationSeq).Sum(p => p.TotalQty);
        var remaining = available - taken;
        MfgRules.ThrowIf(r.GoodQty + r.ScrapQty + r.ReworkQty > remaining, MfgErrors.Op004, ("available", Math.Max(remaining, 0m)));
        MfgRules.ThrowIf(op.Status == OperationStatus.Completed, MfgErrors.Op009);  // a finished operation takes no more (an amount beyond its input is still MFG_OP_004: TC-OP-013)

        // BR-MAT-08: the materials to consume for the cumulative output; a shortage refuses the whole report before anything is stored (MFG_MAT_008)
        var cumulativeOutput = op.GoodQty + op.ScrapQty + pending.Where(p => p.OperationSeq == op.OperationSeq).Sum(p => p.OutputQty) + r.GoodQty + r.ScrapQty;
        var lines = new List<NewStockLine>();
        if (r.GoodQty + r.ScrapQty > 0 && BackflushPlanner.TargetsOf(order, op).Count > 0)
        {
            var flows = await documents.FlowsAsync(order.Id, ct);
            lines = await planner.PlanAsync(order, op, cumulativeOutput, flows, setting.AllowNegativeStockOnBackflush, now, ct);
        }

        var reportNo = await numbers.NextAsync(DocumentTypes.OperationReport, plant.Id, today.ToDateTime(TimeOnly.MinValue), ct);   // BR-GEN-04: one counter per plant and month, so the number is unique per plant
        var report = OperationReport.Create(reportNo, plant.Id, order.Id, order.OrderNo, op.Id, op.OperationSeq, today, r.GoodQty, r.ScrapQty, r.ReworkQty, r.ScrapReasonId, r.ReworkReasonId,
            r.MachineId == Guid.Empty ? null : r.MachineId, r.Note, r.IdempotencyKey, hash, now, by);

        if (lines.Count == 0)
        {
            // nothing to take out of the Warehouse: the report counts at once, in the same save that stores it
            order.ApplyOperationReport(report, by, now);
            report.MarkPosted(now, by);
            await logCloser.CloseForCompletedAsync(order, ct);
            reports.Add(report);
            report = await StoreAsync(report, hash, order.Id, ct);
            return await AnswerAsync(report, ct);
        }

        var documentNo = await numbers.NextAsync(DocumentTypes.Backflush, Guid.Empty, today.ToDateTime(TimeOnly.MinValue), ct);   // per tenant: the number is the Warehouse key
        var document = MaterialTransaction.Create(documentNo, StockDocumentType.Backflush, plant.Id, order.Id, order.OrderNo, today, null, $"Backflush of {reportNo}", null, null, lines, by, report.Id);
        document.Claim(now + MaterialDocuments.FirstAttemptDelay);                  // if this request dies, the job takes the document up after a short while
        report.WaitFor(document.Id);
        reports.Add(report);
        documents.Add(document);
        report = await StoreAsync(report, hash, order.Id, ct);
        if (report.BackflushDocumentId != document.Id) return await AnswerAsync(report, ct);   // a parallel request with the same key won: its document is the one

        await posting.PostAsync(document, by, ct);
        return await AnswerAsync(report, ct);
    }

    private async Task<OperationReport?> FindRepeatAsync(string? key, string hash, Guid orderId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(key)) return null;
        MfgRules.ThrowIf(key.Length > MaterialDocuments.MaxKeyLength, MfgErrors.Gen007);
        var existing = await reports.FindByKeyAsync(key, ct);
        if (existing is null) return null;
        MfgRules.ThrowIf(existing.RequestHash != hash || existing.OrderId != orderId, MfgErrors.Gen007);
        return existing;
    }

    private async Task<OperationReport> StoreAsync(OperationReport report, string hash, Guid orderId, CancellationToken ct)
    {
        try
        {
            await uow.SaveChangesAsync(ct);
            return report;
        }
        catch (ConcurrencyException) when (!string.IsNullOrEmpty(report.IdempotencyKey))
        {
            return await FindRepeatAsync(report.IdempotencyKey, hash, orderId, ct) ?? throw new ConcurrencyException(MfgErrors.Format(MfgErrors.Gen001));
        }
    }

    private async Task EnsureReasonAsync(Guid? id, ReasonType type, CancellationToken ct)
    {
        var reason = id is null || id == Guid.Empty ? null : await reasons.GetAsync(id.Value, ct);
        MfgRules.ThrowIf(reason is not { IsActive: true } || reason.Type != type, MfgErrors.Op003);
    }

    /// <summary>201 when the report counts, 202 while its backflush is on its way, 400 with the Warehouse's refusal when it failed.</summary>
    private async Task<OperationReportResultDto> AnswerAsync(OperationReport report, CancellationToken ct)
    {
        var document = report.BackflushDocumentId is { } id ? await documents.GetAsync(id, ct) : null;
        if (report.Status == OperationReportStatus.Failed)
            throw new BusinessRuleException(report.FailureCode ?? MfgErrors.Mat008, report.FailureMessage ?? "The backflush was refused.");
        return new OperationReportResultDto(OperationReportDto.From(report), report.Status == OperationReportStatus.Pending, document is null ? null : StockDocumentDto.From(document));
    }
}

// ── Complete / reopen by hand (BR-OP-06) ─────────────────────────────────────────────────────────────────────

public sealed record CompleteOperationCommand(Guid OrderId, int OperationSeq) : IRequest<OrderOperationDto>;

public sealed class CompleteOperationHandler(IPlantAccessService access, IProductionOrderRepository orders, OperationLogCloser logCloser, IPlantClock clock, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<CompleteOperationCommand, OrderOperationDto>
{
    public async Task<OrderOperationDto> Handle(CompleteOperationCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        order.CompleteOperation(r.OperationSeq, user.GetUserId(), clock.UtcNow);
        await logCloser.CloseForCompletedAsync(order, ct);
        await uow.SaveChangesAsync(ct);
        return OrderOperationDto.From(order.OperationOf(r.OperationSeq));
    }
}

public sealed record ReopenOperationCommand(Guid OrderId, int OperationSeq) : IRequest<OrderOperationDto>;

public sealed class ReopenOperationHandler(IPlantAccessService access, IProductionOrderRepository orders, IUserContextService user, IMfgUnitOfWork uow)
    : IRequestHandler<ReopenOperationCommand, OrderOperationDto>
{
    public async Task<OrderOperationDto> Handle(ReopenOperationCommand r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        order.ReopenOperation(r.OperationSeq, user.GetUserId());
        await uow.SaveChangesAsync(ct);
        return OrderOperationDto.From(order.OperationOf(r.OperationSeq));
    }
}

// ── Reads ─────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetOrderOperationsQuery(Guid OrderId) : IRequest<IReadOnlyList<OperationDetailDto>>;

public sealed class GetOrderOperationsHandler(IPlantAccessService access, IProductionOrderRepository orders, ITimeLogRepository logs)
    : IRequestHandler<GetOrderOperationsQuery, IReadOnlyList<OperationDetailDto>>
{
    public async Task<IReadOnlyList<OperationDetailDto>> Handle(GetOrderOperationsQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        var all = (await logs.ListForOrderAsync(order.Id, ct)).ToLookup(l => l.OperationSeq);
        return order.Operations.Select(o => new OperationDetailDto(OrderOperationDto.From(o), all[o.OperationSeq].OrderBy(l => l.StartAt).Select(LaborTimeLogDto.From).ToList())).ToList();
    }
}

public sealed record GetOperationReportsQuery(Guid OrderId) : IRequest<IReadOnlyList<OperationReportDto>>;

public sealed class GetOperationReportsHandler(IPlantAccessService access, IProductionOrderRepository orders, IOperationReportRepository reports)
    : IRequestHandler<GetOperationReportsQuery, IReadOnlyList<OperationReportDto>>
{
    public async Task<IReadOnlyList<OperationReportDto>> Handle(GetOperationReportsQuery r, CancellationToken ct)
    {
        var order = await OrderScope.LoadAsync(orders, access, r.OrderId, ct);
        return (await reports.ListForOrderAsync(order.Id, ct)).Select(OperationReportDto.From).ToList();
    }
}

public sealed record GetOperationReportQuery(Guid Id) : IRequest<OperationReportDto>;

public sealed class GetOperationReportHandler(IPlantAccessService access, IOperationReportRepository reports) : IRequestHandler<GetOperationReportQuery, OperationReportDto>
{
    public async Task<OperationReportDto> Handle(GetOperationReportQuery r, CancellationToken ct)
    {
        var report = await reports.GetAsync(r.Id, ct) ?? throw new EntityNotFoundException(nameof(OperationReport), r.Id);
        await access.GetVisiblePlantAsync(report.PlantId, ct);
        return OperationReportDto.From(report);
    }
}
