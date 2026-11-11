using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Entities;

// The production order and what it owns (BA spec 01 §5.10-5.13, §7.3). Everything about the order is changed through methods of the aggregate;
// the lines are a snapshot of the BOM and routing taken when the order was created or regenerated (BR-PO-05).

/// <summary>The header facts of a new order. <c>Quantity</c> is in the item's base unit <c>UomId</c>.</summary>
public sealed record OrderHeaderValues(
    Guid PlantId, Guid ItemId, string ItemCode, string ItemName, decimal Quantity, Guid UomId, Guid BomId, Guid? RoutingId, int Priority, DateOnly DueDate,
    Guid OutputWarehouseId, OrderType OrderType, OrderSourceType SourceType, Guid? SourceRefId, Guid? ParentOrderId, string? Notes);

/// <summary>What a Draft may be changed to (BR-PO-05): the item itself never changes.</summary>
public sealed record OrderDraftChanges(decimal Quantity, DateOnly DueDate, int Priority, Guid BomId, Guid? RoutingId, Guid OutputWarehouseId, string? Notes);

public sealed record OrderMaterialValues(
    int LineNo, Guid ComponentItemId, string ComponentCode, string ComponentName, Guid UomId, int? OperationSeq, decimal RequiredQty, decimal ScrapPct,
    IssueMethod IssueMethod, Guid WarehouseId, string? AlternativeGroup);

public sealed record OrderOperationValues(
    int OperationSeq, string Name, Guid WorkCenterId, string WorkCenterCode, decimal SetupTimeMinutes, decimal RunTimeMinutes, RunTimeBasis RunTimeBasis,
    decimal? BatchSize, int QueueTimeMinutes, int MoveTimeMinutes, bool IsSubcontract, decimal StandardMinutes, int CalendarMinutes, Guid? IpqcInspectionPlanId = null);

public sealed record OrderOutputValues(int LineNo, BomLineType LineType, Guid ItemId, string ItemCode, string ItemName, Guid UomId, decimal PlannedQty, decimal? CostAllocationPct);

public sealed record OrderSnapshot(IReadOnlyList<OrderMaterialValues> Materials, IReadOnlyList<OrderOperationValues> Operations, IReadOnlyList<OrderOutputValues> Outputs);

/// <summary>The times of an order and of each operation (UTC), and whether the order cannot meet its due date (BR-PO-06).</summary>
public sealed record OrderScheduleValues(DateTime PlannedStart, DateTime PlannedEnd, bool IsLate, IReadOnlyDictionary<int, (DateTime Start, DateTime End)> Operations);

/// <summary>A component the order needs.</summary>
public class ProductionOrderMaterial : BaseEntity
{
    private ProductionOrderMaterial() { }

    public Guid OrderId { get; private set; }
    public int LineNo { get; private set; }
    public Guid ComponentItemId { get; private set; }
    public string ComponentCode { get; private set; } = string.Empty;
    public string ComponentName { get; private set; } = string.Empty;
    public Guid UomId { get; private set; }
    public int? OperationSeq { get; private set; }
    public decimal RequiredQty { get; private set; }
    public decimal IssuedQty { get; private set; }
    public decimal ReturnedQty { get; private set; }
    public decimal ConsumedQty { get; private set; }
    public decimal ReservedQty { get; private set; }
    public decimal ScrapPct { get; private set; }
    public IssueMethod IssueMethod { get; private set; } = IssueMethod.Manual;
    public Guid WarehouseId { get; private set; }
    public bool IsSubstitute { get; private set; }
    public int? SubstituteForLineNo { get; private set; }
    public string? AlternativeGroup { get; private set; }
    public bool IsUnplanned { get; private set; }

    /// <summary>How much of the net issued quantity was carried to consumption when the order was closed (BR-PO-16, condition 4).</summary>
    public decimal SettledIssueQty { get; private set; }

    /// <summary>Issued material that is neither consumed nor returned yet: it must be settled before the order closes.</summary>
    public decimal UnsettledIssue => Math.Max(NetIssuedQty - SettledIssueQty, 0m);

    /// <summary>Net quantity still out on the shop floor: issued minus returned (BR-MAT-02).</summary>
    public decimal NetIssuedQty => IssuedQty - ReturnedQty;

    /// <summary>What the order still needs from the Warehouse: required minus what is out on the shop floor minus what backflush has consumed (never negative).</summary>
    public decimal OpenNeed => ReservationPlanner.OpenNeed(RequiredQty, IssuedQty, ReturnedQty, ConsumedQty);

    internal void RegisterIssued(decimal qty) { IssuedQty += qty; UpdatedAt = DateTime.UtcNow; }
    internal void RegisterReturned(decimal qty) { ReturnedQty += qty; UpdatedAt = DateTime.UtcNow; }

    /// <summary>Backflush took this much out of the stock for the output reported so far (BR-MAT-08).</summary>
    internal void RegisterConsumed(decimal qty) { ConsumedQty += qty; UpdatedAt = DateTime.UtcNow; }

    /// <summary>A reversal gave consumed material back (P4-2): never below zero.</summary>
    internal void UnregisterConsumed(decimal qty) { ConsumedQty = Math.Max(0m, ConsumedQty - qty); UpdatedAt = DateTime.UtcNow; }

    /// <summary>Close with "carry the rest to consumption": what is still out becomes consumed.</summary>
    internal void Settle()
    {
        var rest = UnsettledIssue;
        ConsumedQty += rest;
        SettledIssueQty = NetIssuedQty;
        UpdatedAt = DateTime.UtcNow;
    }
    internal void SetRequired(decimal required) { RequiredQty = required; UpdatedAt = DateTime.UtcNow; }
    internal void SetReserved(decimal reserved) { ReservedQty = reserved; UpdatedAt = DateTime.UtcNow; }

    internal static ProductionOrderMaterial Create(Guid orderId, OrderMaterialValues v) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, LineNo = v.LineNo, ComponentItemId = v.ComponentItemId, ComponentCode = v.ComponentCode,
        ComponentName = v.ComponentName, UomId = v.UomId, OperationSeq = v.OperationSeq, RequiredQty = v.RequiredQty, ScrapPct = v.ScrapPct,
        IssueMethod = v.IssueMethod, WarehouseId = v.WarehouseId, AlternativeGroup = v.AlternativeGroup,
    };

    internal void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }
}

/// <summary>An operation of the order, copied from the routing (BR-PO-05).</summary>
public class ProductionOrderOperation : BaseEntity
{
    private ProductionOrderOperation() { }

    public Guid OrderId { get; private set; }
    public int OperationSeq { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid WorkCenterId { get; private set; }
    public string WorkCenterCode { get; private set; } = string.Empty;
    public decimal SetupTimeMinutes { get; private set; }
    public decimal RunTimeMinutes { get; private set; }
    public RunTimeBasis RunTimeBasis { get; private set; } = RunTimeBasis.PerUnit;
    public decimal? BatchSize { get; private set; }
    public int QueueTimeMinutes { get; private set; }
    public int MoveTimeMinutes { get; private set; }
    public bool IsSubcontract { get; private set; }
    public Guid? IpqcInspectionPlanId { get; private set; }
    public OperationStatus Status { get; private set; } = OperationStatus.Pending;
    public decimal StandardMinutes { get; private set; }
    public int CalendarMinutes { get; private set; }
    public DateTime? PlannedStart { get; private set; }
    public DateTime? PlannedEnd { get; private set; }
    public DateTime? ActualStart { get; private set; }
    public DateTime? ActualEnd { get; private set; }
    public decimal GoodQty { get; private set; }
    public decimal ScrapQty { get; private set; }
    public decimal ReworkQty { get; private set; }
    public decimal IpqcPassedQty { get; private set; }

    /// <summary>Good output that still waits for its in-process inspection or failed it (P4-3). It does not count as input of the next operation.</summary>
    public decimal IpqcHeldQty { get; private set; }

    /// <summary>What the next operation may take from this one: the good output that is not held by an in-process inspection (P4-3, BR-QC-05).</summary>
    public decimal NetOutputQty => Math.Max(0m, GoodQty - IpqcHeldQty);

    internal static ProductionOrderOperation Create(Guid orderId, OrderOperationValues v) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, OperationSeq = v.OperationSeq, Name = v.Name, WorkCenterId = v.WorkCenterId, WorkCenterCode = v.WorkCenterCode,
        SetupTimeMinutes = v.SetupTimeMinutes, RunTimeMinutes = v.RunTimeMinutes, RunTimeBasis = v.RunTimeBasis, BatchSize = v.BatchSize,
        QueueTimeMinutes = v.QueueTimeMinutes, MoveTimeMinutes = v.MoveTimeMinutes, IsSubcontract = v.IsSubcontract,
        StandardMinutes = v.StandardMinutes, CalendarMinutes = v.CalendarMinutes, IpqcInspectionPlanId = v.IpqcInspectionPlanId,
    };

    internal void SetPlan(DateTime start, DateTime end) { PlannedStart = start; PlannedEnd = end; UpdatedAt = DateTime.UtcNow; }

    internal void SetTimes(decimal standardMinutes, int calendarMinutes) { StandardMinutes = standardMinutes; CalendarMinutes = calendarMinutes; UpdatedAt = DateTime.UtcNow; }

    /// <summary>Unrelease: the order goes back to Draft, so its operations go back to Pending (not a move of the operation diagram - it only happens before any work).</summary>
    internal void ResetToPending() { Status = OperationStatus.Pending; UpdatedAt = DateTime.UtcNow; }

    /// <summary>True once any work was reported on the operation.</summary>
    public bool HasWork => Status is OperationStatus.Running or OperationStatus.Paused or OperationStatus.Completed || GoodQty != 0 || ScrapQty != 0 || ReworkQty != 0 || ActualStart != null;

    /// <summary>Changes the state through the machine of BA spec 01 §4.3.</summary>
    public void TransitionTo(OperationStatus target)
    {
        OperationStateMachine.EnsureTransition(Status, target);
        Status = target;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Everything the operation has taken in so far: good, scrap and rework (BR-OP-04, BR-OP-06).</summary>
    public decimal TakenQty => GoodQty + ScrapQty + ReworkQty;

    internal void MarkWorkStarted(DateTime now)
    {
        ActualStart ??= now;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void RegisterReport(decimal good, decimal scrap, decimal rework, DateTime now)
    {
        GoodQty += good;
        ScrapQty += scrap;
        ReworkQty += rework;
        ActualStart ??= now;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>A reversal took the quantities of a report off the operation (P4-2, BR-OP-07): never below zero.</summary>
    internal void UnregisterReport(decimal good, decimal scrap, decimal rework)
    {
        GoodQty = Math.Max(0m, GoodQty - good);
        ScrapQty = Math.Max(0m, ScrapQty - scrap);
        ReworkQty = Math.Max(0m, ReworkQty - rework);
        UpdatedAt = DateTime.UtcNow;
    }

    internal void HoldForInspection(decimal qty) { IpqcHeldQty += qty; UpdatedAt = DateTime.UtcNow; }

    /// <summary>The inspection of a report is cancelled (its report was reversed) or passed: the quantity stops being held.</summary>
    internal void ReleaseHold(decimal qty) { IpqcHeldQty = Math.Max(0m, IpqcHeldQty - qty); UpdatedAt = DateTime.UtcNow; }

    internal void PassHeld(decimal qty) { ReleaseHold(qty); IpqcPassedQty += qty; }

    internal void MarkCompleted(DateTime now)
    {
        TransitionTo(OperationStatus.Completed);
        ActualStart ??= now;
        ActualEnd = now;
    }

    /// <summary>The order is done: whatever state the operation is in, it ends Completed (Pending and Paused walk through Ready / Running first).</summary>
    internal void ForceComplete(DateTime now)
    {
        if (Status == OperationStatus.Completed) return;
        if (Status == OperationStatus.Pending) TransitionTo(OperationStatus.Ready);
        if (Status == OperationStatus.Paused) TransitionTo(OperationStatus.Running);
        MarkCompleted(now);
    }

    internal void MarkReopened()
    {
        TransitionTo(OperationStatus.Ready);
        ActualEnd = null;
    }

    /// <summary>
    /// The status follows the time logs of the operation (BR-OP-02): Running while one runs, Paused while some are open and all paused, Ready again when the last
    /// one stops. Only Running / Paused operations move.
    /// </summary>
    public void FollowLogs(int runningLogs, int openLogs)
    {
        if (Status is not (OperationStatus.Running or OperationStatus.Paused)) return;
        var target = openLogs == 0 ? OperationStatus.Ready : runningLogs > 0 ? OperationStatus.Running : OperationStatus.Paused;
        if (Status == target) return;
        if (Status == OperationStatus.Paused && target == OperationStatus.Ready) TransitionTo(OperationStatus.Running);   // the machine goes Paused -> Running -> Ready
        TransitionTo(target);
    }

    /// <summary>Hold (BR-PO-11): a Running operation pauses.</summary>
    internal void PauseForHold()
    {
        if (Status == OperationStatus.Running) TransitionTo(OperationStatus.Paused);
    }

    internal void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }
}

/// <summary>A by-product or co-product the order is expected to yield (BR-PO-05).</summary>
public class ProductionOrderOutput : BaseEntity
{
    private ProductionOrderOutput() { }

    public Guid OrderId { get; private set; }
    public int LineNo { get; private set; }
    public BomLineType LineType { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public Guid UomId { get; private set; }
    public decimal PlannedQty { get; private set; }
    public decimal ReceivedQty { get; private set; }
    public decimal? CostAllocationPct { get; private set; }

    internal static ProductionOrderOutput Create(Guid orderId, OrderOutputValues v) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, LineNo = v.LineNo, LineType = v.LineType, ItemId = v.ItemId, ItemCode = v.ItemCode, ItemName = v.ItemName,
        UomId = v.UomId, PlannedQty = v.PlannedQty, CostAllocationPct = v.CostAllocationPct,
    };

    internal void SetPlanned(decimal planned) { PlannedQty = planned; UpdatedAt = DateTime.UtcNow; }

    internal void RegisterReceived(decimal qty) { ReceivedQty += qty; UpdatedAt = DateTime.UtcNow; }

    /// <summary>A reversal took received goods out of stock again (P4-2): never below zero.</summary>
    internal void UnregisterReceived(decimal qty) { ReceivedQty = Math.Max(0m, ReceivedQty - qty); UpdatedAt = DateTime.UtcNow; }

    internal void MarkDeleted() { IsDeleted = true; UpdatedAt = DateTime.UtcNow; }
}

/// <summary>One change of status: from, to, why, who, when (BR-PO-02).</summary>
public class ProductionOrderStatusHistory : BaseEntity
{
    private ProductionOrderStatusHistory() { }

    public Guid OrderId { get; private set; }
    public OrderStatus FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public Guid? ReasonCodeId { get; private set; }
    public string? Note { get; private set; }
    public string ChangedBy { get; private set; } = string.Empty;
    public DateTime ChangedAt { get; private set; }

    internal static ProductionOrderStatusHistory Create(Guid orderId, OrderStatus from, OrderStatus to, Guid? reasonCodeId, string? note, string changedBy) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, FromStatus = from, ToStatus = to, ReasonCodeId = reasonCodeId, Note = note, ChangedBy = changedBy, ChangedAt = DateTime.UtcNow,
    };
}

/// <summary>The production order (BA spec 01 §5.10).</summary>
public class ProductionOrder : BaseEntity
{
    private readonly List<ProductionOrderMaterial> _materials = [];
    private readonly List<ProductionOrderOperation> _operations = [];
    private readonly List<ProductionOrderOutput> _outputs = [];
    private readonly List<ProductionOrderStatusHistory> _history = [];

    private ProductionOrder() { }

    public string OrderNo { get; private set; } = string.Empty;
    public Guid PlantId { get; private set; }
    public OrderType OrderType { get; private set; } = OrderType.Standard;
    public Guid ItemId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public Guid UomId { get; private set; }
    public Guid BomId { get; private set; }
    public Guid? RoutingId { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public OrderStatus? StatusBeforeHold { get; private set; }
    public int Priority { get; private set; } = 3;
    public DateOnly DueDate { get; private set; }
    public DateTime PlannedStart { get; private set; }
    public DateTime PlannedEnd { get; private set; }
    public DateTime? ActualStart { get; private set; }
    public DateTime? ActualEnd { get; private set; }
    public bool IsLate { get; private set; }
    public Guid OutputWarehouseId { get; private set; }
    public OrderSourceType SourceType { get; private set; } = OrderSourceType.Manual;
    public Guid? SourceRefId { get; private set; }
    public Guid? ParentOrderId { get; private set; }
    public decimal CompletedQty { get; private set; }
    public decimal ScrappedQty { get; private set; }
    public bool IsRework { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>The Warehouse reservation holding the materials (set when the order is released, P3-2).</summary>
    public Guid? ReservationId { get; private set; }

    /// <summary>
    /// True while the Warehouse reservation does not yet match the order (it was just released, unreleased, cancelled or its quantity changed, or the Warehouse
    /// could not be reached). <c>IOrderReservationSync</c> brings it in line and clears the flag; the sync job retries with back-off (<see cref="ReservationNextAttemptAt"/>).
    /// </summary>
    public bool ReservationSyncPending { get; private set; }
    public int ReservationSyncAttempts { get; private set; }
    public string? ReservationSyncError { get; private set; }
    public DateTime? ReservationNextAttemptAt { get; private set; }

    public IReadOnlyCollection<ProductionOrderMaterial> Materials => _materials.Where(m => !m.IsDeleted).OrderBy(m => m.LineNo).ToList();
    public IReadOnlyCollection<ProductionOrderOperation> Operations => _operations.Where(o => !o.IsDeleted).OrderBy(o => o.OperationSeq).ToList();
    public IReadOnlyCollection<ProductionOrderOutput> Outputs => _outputs.Where(o => !o.IsDeleted).OrderBy(o => o.LineNo).ToList();
    public IReadOnlyCollection<ProductionOrderStatusHistory> History => _history.OrderBy(h => h.ChangedAt).ToList();

    /// <summary>Persistence access to the loaded children (EF fills the fields).</summary>
    public IReadOnlyList<ProductionOrderMaterial> AllMaterials => _materials;
    public IReadOnlyList<ProductionOrderOperation> AllOperations => _operations;
    public IReadOnlyList<ProductionOrderOutput> AllOutputs => _outputs;
    public IReadOnlyList<ProductionOrderStatusHistory> AllHistory => _history;

    // ── Creation and Draft changes ─────────────────────────────────────────────────────────────────────────

    public static ProductionOrder Create(string orderNo, OrderHeaderValues h, OrderSnapshot snapshot, OrderScheduleValues schedule, string createdBy)
    {
        MfgRules.ThrowIf(string.IsNullOrWhiteSpace(orderNo), MfgErrors.Gen002);
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(), OrderNo = orderNo, PlantId = h.PlantId, OrderType = h.OrderType, ItemId = h.ItemId, ItemCode = h.ItemCode, ItemName = h.ItemName,
            UomId = h.UomId, SourceType = h.SourceType, SourceRefId = h.SourceRefId, ParentOrderId = h.ParentOrderId, IsRework = h.OrderType == OrderType.Rework,
            CreatedBy = createdBy, UpdatedBy = createdBy,
        };
        order.SetDraftFields(new OrderDraftChanges(h.Quantity, h.DueDate, h.Priority, h.BomId, h.RoutingId, h.OutputWarehouseId, h.Notes));
        order.ReplaceChildren(snapshot);
        order.ApplySchedule(schedule);
        order.AddDomainEvent(new ProductionOrderCreatedEvent
        {
            OrderId = order.Id, OrderNo = order.OrderNo, PlantId = order.PlantId, ItemId = order.ItemId, ItemCode = order.ItemCode, Quantity = order.Quantity,
            DueDate = order.DueDate, SourceType = order.SourceType,
        });
        return order;
    }

    /// <summary>A closed order is read only (<c>MFG_PO_014</c>); a cancelled one is final (<c>MFG_PO_002</c>).</summary>
    public void EnsureEditable()
    {
        MfgRules.ThrowIf(Status == OrderStatus.Closed, MfgErrors.Po014);
        MfgRules.ThrowIf(Status == OrderStatus.Cancelled, MfgErrors.Po002, ("from", Status), ("to", OrderStatus.Draft));
    }

    /// <summary>Only a Draft can be regenerated (BR-PO-05).</summary>
    public void EnsureDraft()
    {
        EnsureEditable();
        MfgRules.ThrowIf(Status != OrderStatus.Draft, MfgErrors.Po002, ("from", Status), ("to", OrderStatus.Draft));
    }

    /// <summary>BR-PO-05: a Draft whose quantity, BOM or routing changed gets a fresh snapshot; the old lines are dropped (nothing refers to Draft lines yet).</summary>
    public void Regenerate(OrderDraftChanges changes, OrderSnapshot snapshot, OrderScheduleValues schedule, string changedBy)
    {
        EnsureDraft();
        SetDraftFields(changes);
        ReplaceChildren(snapshot);
        ApplySchedule(schedule);
        Touch(changedBy);
    }

    /// <summary>A Draft whose only change is its due date, priority, output warehouse or notes keeps its lines and gets a new schedule.</summary>
    public void Reschedule(OrderDraftChanges changes, OrderScheduleValues schedule, string changedBy)
    {
        EnsureDraft();
        SetDraftFields(changes);
        ApplySchedule(schedule);
        Touch(changedBy);
    }

    private void SetDraftFields(OrderDraftChanges c)
    {
        MfgRules.ThrowIf(c.Quantity <= 0 || !HasAtMostDecimals(c.Quantity, 6), MfgErrors.Po001);
        if (c.Priority is < 1 or > 5) throw MfgRules.Invalid("The priority must be between 1 (highest) and 5.");
        Quantity = c.Quantity;
        DueDate = c.DueDate;
        Priority = c.Priority;
        BomId = c.BomId;
        RoutingId = c.RoutingId;
        OutputWarehouseId = c.OutputWarehouseId;
        Notes = string.IsNullOrWhiteSpace(c.Notes) ? null : c.Notes.Trim();
    }

    private void ReplaceChildren(OrderSnapshot s)
    {
        foreach (var m in _materials.Where(x => !x.IsDeleted)) m.MarkDeleted();
        foreach (var o in _operations.Where(x => !x.IsDeleted)) o.MarkDeleted();
        foreach (var o in _outputs.Where(x => !x.IsDeleted)) o.MarkDeleted();
        _materials.AddRange(s.Materials.Select(v => ProductionOrderMaterial.Create(Id, v)));
        _operations.AddRange(s.Operations.Select(v => ProductionOrderOperation.Create(Id, v)));
        _outputs.AddRange(s.Outputs.Select(v => ProductionOrderOutput.Create(Id, v)));
    }

    /// <summary>Writes the planned times of the order and of each operation (UTC) and the late flag.</summary>
    public void ApplySchedule(OrderScheduleValues schedule)
    {
        PlannedStart = schedule.PlannedStart;
        PlannedEnd = schedule.PlannedEnd;
        IsLate = schedule.IsLate;
        foreach (var op in _operations.Where(o => !o.IsDeleted))
            if (schedule.Operations.TryGetValue(op.OperationSeq, out var times)) op.SetPlan(times.Start, times.End);
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Status ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Changes the status along BA spec 01 §4.2 and writes the history (BR-PO-02). Hold remembers the status it came from; Resume goes back to exactly that
    /// status (BR-PO-11). The first move to InProgress stamps <c>ActualStart</c>, Completed stamps <c>ActualEnd</c>, a Reopen clears it.
    /// </summary>
    public void TransitionTo(OrderStatus target, string changedBy, Guid? reasonCodeId = null, string? note = null)
    {
        OrderStateMachine.EnsureTransition(Status, target);
        if (Status == OrderStatus.OnHold)
            MfgRules.ThrowIf(target != StatusBeforeHold, MfgErrors.Po002, ("from", Status), ("to", target));

        var from = Status;
        if (target == OrderStatus.OnHold) StatusBeforeHold = from;
        else if (from == OrderStatus.OnHold) StatusBeforeHold = null;

        if (target == OrderStatus.InProgress && ActualStart is null) ActualStart = DateTime.UtcNow;
        if (target == OrderStatus.Completed) ActualEnd = DateTime.UtcNow;
        if (from == OrderStatus.Completed && target == OrderStatus.InProgress) ActualEnd = null;

        Status = target;
        _history.Add(ProductionOrderStatusHistory.Create(Id, from, target, reasonCodeId, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), changedBy));
        Touch(changedBy);
        AddDomainEvent(new ProductionOrderStatusChangedEvent
        {
            OrderId = Id, OrderNo = OrderNo, PlantId = PlantId, ItemId = ItemId, Quantity = Quantity, FromStatus = from, ToStatus = target,
            ReasonCodeId = reasonCodeId, Note = note, ChangedBy = changedBy,
        });
    }

    // ── Release, unrelease, cancel, hold, resume, change of quantity (P3-2) ───────────────────────────────

    /// <summary>
    /// BR-PO-08: Draft -> Released. The first operation (lowest sequence) becomes Ready and the others stay Pending, or all become Ready when the plant does not
    /// require sequential operations. The reservation follows through the sync.
    /// </summary>
    public void Release(bool sequentialOperations, string releasedBy)
    {
        TransitionTo(OrderStatus.Released, releasedBy);
        var ops = _operations.Where(o => !o.IsDeleted).OrderBy(o => o.OperationSeq).ToList();
        for (var i = 0; i < ops.Count; i++)
            if (!sequentialOperations || i == 0) ops[i].TransitionTo(OperationStatus.Ready);
        MarkReservationSync();
        AddDomainEvent(new ProductionOrderReleasedEvent { OrderId = Id, OrderNo = OrderNo, PlantId = PlantId, ItemId = ItemId, Quantity = Quantity, DueDate = DueDate });
    }

    /// <summary>True while nothing has happened on the shop floor: no issue, return, consumption, work or completed quantity (the P3-2 part of BR-PO-09 / BR-PO-13).</summary>
    public bool IsUntouched =>
        _materials.Where(m => !m.IsDeleted).All(m => m.IssuedQty == 0 && m.ReturnedQty == 0 && m.ConsumedQty == 0)
        && _operations.Where(o => !o.IsDeleted).All(o => !o.HasWork)
        && CompletedQty == 0 && ScrappedQty == 0;

    /// <summary>BR-PO-09: Released -> Draft, only before any transaction (<c>MFG_PO_008</c>). Operations go back to Pending; the reservation is released by the sync.</summary>
    public void Unrelease(string by)
    {
        EnsureEditable();
        MfgRules.ThrowIf(Status == OrderStatus.Released && !IsUntouched, MfgErrors.Po008);
        TransitionTo(OrderStatus.Draft, by);
        foreach (var op in _operations.Where(o => !o.IsDeleted)) op.ResetToPending();
        MarkReservationSync();
    }

    /// <summary>BR-PO-13: Draft or Released-without-transactions -> Cancelled, with a reason of type Cancel (<c>MFG_PO_009</c>); the reservation is released by the sync.</summary>
    public void Cancel(Guid? reasonCodeId, string? note, string by)
    {
        EnsureEditable();
        MfgRules.ThrowIf(reasonCodeId is null || reasonCodeId == Guid.Empty, MfgErrors.Po009);
        MfgRules.ThrowIf(Status == OrderStatus.Released && !IsUntouched, MfgErrors.Po008);
        var held = Status != OrderStatus.Draft || ReservationId != null;
        TransitionTo(OrderStatus.Cancelled, by, reasonCodeId, note);
        if (held) MarkReservationSync();
    }

    /// <summary>BR-PO-11: Released or InProgress -> OnHold with a reason of type Hold (<c>MFG_PO_009</c>). The reservation stays.</summary>
    public void Hold(Guid? reasonCodeId, string? note, string by)
    {
        EnsureEditable();
        MfgRules.ThrowIf(reasonCodeId is null || reasonCodeId == Guid.Empty, MfgErrors.Po009);
        TransitionTo(OrderStatus.OnHold, by, reasonCodeId, note);
        foreach (var op in _operations.Where(o => !o.IsDeleted)) op.PauseForHold();   // the caller pauses their time logs (BR-PO-11)
    }

    /// <summary>BR-PO-11: back to the status the order was held from.</summary>
    public void Resume(string by)
    {
        EnsureEditable();
        MfgRules.ThrowIf(Status != OrderStatus.OnHold || StatusBeforeHold is null, MfgErrors.Po002, ("from", Status), ("to", OrderStatus.Released));
        TransitionTo(StatusBeforeHold!.Value, by);
    }

    /// <summary>
    /// BR-PO-12: a released order gets a new quantity (Released, InProgress or OnHold; a Draft is changed with <see cref="Regenerate"/>). It cannot go below what is
    /// completed (<c>MFG_PO_010</c>). The caller recomputes the lines from the same BOM: required quantities (issued quantities stay), planned outputs, operation
    /// minutes and the schedule. The change is written to the history and the reservation is brought in line by the sync.
    /// </summary>
    public void ChangeQuantity(decimal newQuantity, IReadOnlyDictionary<int, decimal> requiredByLine, IReadOnlyDictionary<int, decimal> plannedByOutputLine,
        IReadOnlyDictionary<int, (decimal Standard, int Calendar)> minutesBySeq, OrderScheduleValues schedule, string? note, string by)
    {
        EnsureEditable();
        if (Status == OrderStatus.Draft) throw MfgRules.Invalid("The quantity of a Draft is changed with the edit of the Draft.");
        MfgRules.ThrowIf(Status is OrderStatus.Completed, MfgErrors.Po002, ("from", Status), ("to", Status));
        MfgRules.ThrowIf(newQuantity <= 0 || !HasAtMostDecimals(newQuantity, 6), MfgErrors.Po001);
        MfgRules.ThrowIf(newQuantity < CompletedQty, MfgErrors.Po010, ("completed", CompletedQty));

        var old = Quantity;
        Quantity = newQuantity;
        foreach (var m in _materials.Where(x => !x.IsDeleted))
            if (requiredByLine.TryGetValue(m.LineNo, out var required)) m.SetRequired(required);
        foreach (var o in _outputs.Where(x => !x.IsDeleted))
            if (plannedByOutputLine.TryGetValue(o.LineNo, out var planned)) o.SetPlanned(planned);
        foreach (var op in _operations.Where(x => !x.IsDeleted))
            if (minutesBySeq.TryGetValue(op.OperationSeq, out var minutes)) op.SetTimes(minutes.Standard, minutes.Calendar);
        ApplySchedule(schedule);

        _history.Add(ProductionOrderStatusHistory.Create(Id, Status, Status, null, string.IsNullOrWhiteSpace(note) ? $"Quantity {old} -> {newQuantity}" : $"Quantity {old} -> {newQuantity}: {note.Trim()}", by));
        Touch(by);
        MarkReservationSync();
        AddDomainEvent(new ProductionOrderQuantityChangedEvent { OrderId = Id, OrderNo = OrderNo, ItemId = ItemId, OldQuantity = old, NewQuantity = newQuantity, ChangedBy = by });
    }

    /// <summary>
    /// A stock document is Posted in the Warehouse: its quantities enter the materials (issued / returned), the first issue starts the order (BR-PO-10) and the
    /// reservation is flagged to follow (the Warehouse consumed part of it). The caller saves the order together with the document.
    /// </summary>
    public void ApplyPostedDocument(MaterialTransaction document, string by)
    {
        if (document.Type == StockDocumentType.Reversal)
        {
            // P4-2: the reversal gives back what the original consumed and takes the received goods off the order; nothing starts the order and nothing completes it
            foreach (var group in document.PostedLines.Where(l => l.Kind == StockLineKind.Material).GroupBy(l => l.OrderMaterialId))
                (_materials.FirstOrDefault(m => m.Id == group.Key && !m.IsDeleted)
                    ?? throw MfgRules.Invalid($"The material line of {document.DocNo} no longer exists on order {OrderNo}.")).UnregisterConsumed(group.Sum(l => l.Qty));
            foreach (var line in document.PostedLines.Where(l => l.Kind == StockLineKind.Receipt))
            {
                if (line.OrderOutputId is null) CompletedQty = Math.Max(0m, CompletedQty - line.Qty);
                else (_outputs.FirstOrDefault(o => o.Id == line.OrderOutputId && !o.IsDeleted)
                    ?? throw MfgRules.Invalid($"The output line of {document.DocNo} no longer exists on order {OrderNo}.")).UnregisterReceived(line.Qty);
            }
            MarkReservationSync();
            Touch(by);
            return;
        }

        foreach (var group in document.PostedLines.Where(l => l.Kind == StockLineKind.Material).GroupBy(l => l.OrderMaterialId))
        {
            var material = _materials.FirstOrDefault(m => m.Id == group.Key && !m.IsDeleted)
                ?? throw MfgRules.Invalid($"The material line of {document.DocNo} no longer exists on order {OrderNo}.");
            var qty = group.Sum(l => l.Qty);
            switch (document.Type)
            {
                case StockDocumentType.Issue: material.RegisterIssued(qty); break;
                case StockDocumentType.Return: material.RegisterReturned(qty); break;
                default: material.RegisterConsumed(qty); break;                 // backflush, or the backflush part of a receipt
            }
        }

        if (document.Type == StockDocumentType.Receipt)
        {
            // BR-FG-05, BR-PO-19: the main product counts at once (also while its lot waits for quality), co- / by-products go to their output line
            foreach (var line in document.PostedLines.Where(l => l.Kind == StockLineKind.Receipt))
            {
                if (line.OrderOutputId is null) CompletedQty += line.Qty;
                else (_outputs.FirstOrDefault(o => o.Id == line.OrderOutputId && !o.IsDeleted)
                    ?? throw MfgRules.Invalid($"The output line of {document.DocNo} no longer exists on order {OrderNo}.")).RegisterReceived(line.Qty);
            }
            BeginExecution(by, $"First receipt {document.DocNo}");
        }

        if (document.Type == StockDocumentType.Issue && Status == OrderStatus.Released)
            TransitionTo(OrderStatus.InProgress, by, null, $"First material issue {document.DocNo}");
        MarkReservationSync();
        Touch(by);
    }

    // ── Complete, reopen, close (P3-5) ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// BR-PO-14a: all of the quantity is received and the plant completes by itself: InProgress -> Completed. Returns true when the order was completed.
    /// </summary>
    public bool TryAutoComplete(bool autoComplete, string by, DateTime now)
    {
        if (!autoComplete || Status != OrderStatus.InProgress || CompletedQty < Quantity) return false;
        FinishOrder(by, now, null, "All of the quantity was received", automatic: true, shortClosed: false);
        return true;
    }

    /// <summary>
    /// BR-PO-14b/c: a supervisor completes the order by hand. No operation may be Running and no stock document may be on its way (<c>MFG_PO_013</c>). At or above
    /// <c>Quantity x (1 - under-completion tolerance)</c> no reason is needed; below it only a short-close with the permission and a ShortClose reason is accepted
    /// (<c>MFG_PO_012</c>).
    /// </summary>
    public void Complete(string by, DateTime now, decimal underCompletionPct, bool canShortClose, bool reasonIsShortClose, Guid? reasonCodeId, string? note, int pendingDocuments)
    {
        EnsureEditable();
        OrderStateMachine.EnsureTransition(Status, OrderStatus.Completed);
        var running = _operations.Count(o => !o.IsDeleted && o.Status == OperationStatus.Running);
        var problems = new List<string>();
        if (running > 0) problems.Add($"{running} công đoạn đang chạy");
        if (pendingDocuments > 0) problems.Add($"{pendingDocuments} chứng từ kho đang chờ");
        MfgRules.ThrowIf(problems.Count > 0, MfgErrors.Po013, ("conditions", string.Join("; ", problems)));

        var shortClosed = !OrderCompletion.MeetsThreshold(CompletedQty, Quantity, underCompletionPct);
        MfgRules.ThrowIf(shortClosed && !(canShortClose && reasonIsShortClose && reasonCodeId is not null), MfgErrors.Po012);
        FinishOrder(by, now, shortClosed ? reasonCodeId : null, note, automatic: false, shortClosed);
    }

    private void FinishOrder(string by, DateTime now, Guid? reasonCodeId, string? note, bool automatic, bool shortClosed)
    {
        foreach (var op in _operations.Where(o => !o.IsDeleted)) op.ForceComplete(now);          // the caller closes their open time logs
        TransitionTo(OrderStatus.Completed, by, reasonCodeId, note);
        MarkReservationSync();                                                                   // what is still reserved goes back to the Warehouse
        AddDomainEvent(new ProductionOrderCompletedEvent
        {
            OrderId = Id, OrderNo = OrderNo, PlantId = PlantId, ItemId = ItemId, Quantity = Quantity, CompletedQty = CompletedQty, ScrappedQty = ScrappedQty,
            Automatic = automatic, ShortClosed = shortClosed, ReasonCodeId = reasonCodeId,
        });
    }

    /// <summary>BR-PO-15: Completed -> InProgress. The reservation is rebuilt for what is still needed; operations stay as they are.</summary>
    public void Reopen(string by)
    {
        EnsureEditable();
        TransitionTo(OrderStatus.InProgress, by, null, "Reopened");
        MarkReservationSync();
    }

    /// <summary>
    /// The conditions of BR-PO-16 for the close checklist. (1) open material requests and (5) costing belong to later phases and are met; (4) is met when nothing issued is
    /// left unsettled or the user confirms that the rest is carried to consumption.
    /// </summary>
    public IReadOnlyList<CloseCondition> CloseConditions(int openTimeLogs, int heldLots, int pendingDocuments, bool confirmConsumeRemaining)
    {
        var unsettled = _materials.Where(m => !m.IsDeleted && m.UnsettledIssue > 0).ToList();
        var detail = string.Join(", ", unsettled.Select(m => $"{m.ComponentCode} {m.UnsettledIssue:0.######}"));
        return
        [
            new("OPEN_MATERIAL_REQUESTS", true, "Không còn yêu cầu cấp vật tư đang mở."),
            new("OPEN_TIME_LOGS", openTimeLogs == 0, openTimeLogs == 0 ? "Không còn phiên làm việc đang mở." : $"Còn {openTimeLogs} phiên làm việc đang mở."),
            new("QUALITY_HOLD", heldLots == 0, heldLots == 0 ? "Không còn lô thành phẩm chờ kiểm." : $"Còn {heldLots} lô thành phẩm đang chờ kiểm."),
            new("UNSETTLED_MATERIAL", unsettled.Count == 0 || confirmConsumeRemaining,
                unsettled.Count == 0 ? "Vật tư đã xuất đã được quyết toán." : confirmConsumeRemaining ? $"Phần còn lại sẽ kết chuyển tiêu hao: {detail}." : $"Vật tư đã xuất chưa quyết toán: {detail}."),
            new("COSTING", true, "Giá thành chưa áp dụng (Giai đoạn 6)."),
            new("PENDING_DOCUMENTS", pendingDocuments == 0, pendingDocuments == 0 ? "Không còn chứng từ kho đang chờ." : $"Còn {pendingDocuments} chứng từ kho / báo cáo đang chờ."),
        ];
    }

    /// <summary>BR-PO-16: Completed -> Closed when every condition is met; with <paramref name="confirmConsumeRemaining"/> the unsettled issued material is carried to consumption.</summary>
    public void Close(string by, int openTimeLogs, int heldLots, int pendingDocuments, bool confirmConsumeRemaining)
    {
        EnsureEditable();
        OrderStateMachine.EnsureTransition(Status, OrderStatus.Closed);
        var unmet = CloseConditions(openTimeLogs, heldLots, pendingDocuments, confirmConsumeRemaining).Where(c => !c.Met).Select(c => c.Message).ToList();
        MfgRules.ThrowIf(unmet.Count > 0, MfgErrors.Po013, ("conditions", string.Join("; ", unmet)));

        var carried = confirmConsumeRemaining && _materials.Any(m => !m.IsDeleted && m.UnsettledIssue > 0);
        if (confirmConsumeRemaining) foreach (var m in _materials.Where(x => !x.IsDeleted)) m.Settle();
        TransitionTo(OrderStatus.Closed, by, null, carried ? "Closed; the rest of the issued material was carried to consumption" : "Closed");
        AddDomainEvent(new ProductionOrderClosedEvent { OrderId = Id, OrderNo = OrderNo, PlantId = PlantId, CompletedQty = CompletedQty, UnsettledMaterialCarriedToConsumption = carried, ClosedBy = by });
    }

    // ── Execution of operations (P3-4) ────────────────────────────────────────────────────────────────────────

    public ProductionOrderOperation OperationOf(int operationSeq)
        => _operations.FirstOrDefault(o => o.OperationSeq == operationSeq && !o.IsDeleted)
            ?? throw MfgRules.Invalid($"Order {OrderNo} has no operation {operationSeq}.");

    /// <summary>
    /// Shop-floor work needs a Released or InProgress order: a closed or cancelled order is final, an order on hold has its own code (BR-PO-11), any other status
    /// is "not ready" (<c>MFG_OP_001</c>).
    /// </summary>
    public void EnsureOpenForWork()
    {
        EnsureEditable();
        MfgRules.ThrowIf(Status == OrderStatus.OnHold, MfgErrors.Po011);
        MfgRules.ThrowIf(Status is not (OrderStatus.Released or OrderStatus.InProgress), MfgErrors.Op001);
    }

    /// <summary>BR-PO-10: the first transaction (issue, start, report, receipt) moves a Released order to InProgress.</summary>
    private void BeginExecution(string by, string note)
    {
        if (Status == OrderStatus.Released) TransitionTo(OrderStatus.InProgress, by, null, note);
    }

    /// <summary>
    /// BR-OP-02: somebody starts working on the operation. It must be Ready, Paused or already Running (several people may share it); a Pending operation, or an order
    /// that is not open, answers <c>MFG_OP_001</c>; a finished operation <c>MFG_OP_009</c>. The first start also starts the order.
    /// </summary>
    public void StartOperationWork(int operationSeq, string by, DateTime now)
    {
        EnsureEditable();
        var op = OperationOf(operationSeq);
        MfgRules.ThrowIf(Status is not (OrderStatus.Released or OrderStatus.InProgress), MfgErrors.Op001);
        MfgRules.ThrowIf(op.Status == OperationStatus.Completed, MfgErrors.Op009);
        MfgRules.ThrowIf(op.Status == OperationStatus.Pending, MfgErrors.Op001);
        if (op.Status != OperationStatus.Running) op.TransitionTo(OperationStatus.Running);
        op.MarkWorkStarted(now);
        BeginExecution(by, $"First operation start {op.OperationSeq}");
        Touch(by);
    }

    /// <summary>The operation before <paramref name="operationSeq"/> (null for the first).</summary>
    public ProductionOrderOperation? PreviousOperation(int operationSeq)
        => Operations.Where(o => o.OperationSeq < operationSeq).OrderByDescending(o => o.OperationSeq).FirstOrDefault();

    /// <summary>
    /// A report is Posted (its backflush, if any, reached the Warehouse): its figures enter the operation and the order (BR-OP-03, BR-OP-06, BR-PO-19), the next
    /// operation becomes Ready (BR-OP-01), operations that now have taken all their input complete (cascading down the route), the first transaction starts the
    /// order (BR-PO-10) and the event leaves with the same save.
    /// </summary>
    public void ApplyOperationReport(OperationReport report, string by, DateTime now, bool holdForInspection = false)
    {
        EnsureEditable();
        var op = OperationOf(report.OperationSeq);
        op.RegisterReport(report.GoodQty, report.ScrapQty, report.ReworkQty, now);
        if (holdForInspection && report.GoodQty > 0) op.HoldForInspection(report.GoodQty);   // P4-3: the Good output waits for its IPQC before the next operation may take it
        ScrappedQty += report.ScrapQty;
        BeginExecution(by, $"First operation report {report.DocNo}");

        var next = Operations.FirstOrDefault(o => o.OperationSeq > op.OperationSeq);
        if (next is { Status: OperationStatus.Pending } && op.NetOutputQty > 0) next.TransitionTo(OperationStatus.Ready);   // BR-OP-01
        CompleteSatisfiedOperations(now);
        AddDomainEvent(report.ToEvent());
        Touch(by);
    }

    /// <summary>
    /// A reversal of <paramref name="original"/> is Posted (its backflush, if any, is back in the Warehouse): the quantities leave the operation and the order again (BR-OP-07).
    /// The reversed operation goes back from Completed to Ready when it no longer has all its input taken (BR-OP-06); the next operation goes back from Ready to Pending when this one
    /// has no good output left, nothing was done there and the plant is sequential (BR-OP-01). Operations after that keep their status.
    /// </summary>
    public void ApplyOperationReportReversal(OperationReport original, OperationReport reversal, bool sequentialOperations, string by, DateTime now, decimal cancelledInspectionHold = 0m)
    {
        EnsureEditable();
        var ops = Operations.ToList();
        var index = ops.FindIndex(o => o.OperationSeq == original.OperationSeq);
        var op = index >= 0 ? ops[index] : throw MfgRules.Invalid($"Order {OrderNo} has no operation {original.OperationSeq}.");
        op.ReleaseHold(cancelledInspectionHold);   // P4-3: a Pending in-process inspection of the report is cancelled with it
        op.UnregisterReport(original.GoodQty, original.ScrapQty, original.ReworkQty);
        ScrappedQty = Math.Max(0m, ScrappedQty - original.ScrapQty);

        var previous = index == 0 ? null : ops[index - 1];
        if (op.Status == OperationStatus.Completed
            && !OperationInput.IsComplete(index == 0, previous?.Status == OperationStatus.Completed && previous.IpqcHeldQty == 0m, Quantity, previous?.NetOutputQty ?? 0m, op.TakenQty))
            op.MarkReopened();

        var next = index + 1 < ops.Count ? ops[index + 1] : null;
        if (sequentialOperations && next is { Status: OperationStatus.Ready, HasWork: false } && op.NetOutputQty == 0m) next.ResetToPending();   // BR-OP-01 the other way round

        AddDomainEvent(reversal.ToReversedEvent(original));
        Touch(by);
    }

    /// <summary>
    /// BR-FG-07: a receipt was reversed and the order is Completed: when the completed quantity no longer meets <c>Quantity x (1 - under-completion tolerance)</c> the order goes back to InProgress.
    /// Returns true when it was reopened.
    /// </summary>
    public bool ReopenAfterReceiptReversal(string reversalDocNo, string by, decimal underCompletionPct)
    {
        if (Status != OrderStatus.Completed || OrderCompletion.MeetsThreshold(CompletedQty, Quantity, underCompletionPct)) return false;
        TransitionTo(OrderStatus.InProgress, by, null, $"Reopened: receipt reversed by {reversalDocNo}");
        MarkReservationSync();
        return true;
    }

    /// <summary>BR-OP-06: walks the route and completes every open operation whose input is all taken, so finishing one can finish the next.</summary>
    private void CompleteSatisfiedOperations(DateTime now)
    {
        var ops = Operations.ToList();
        for (var i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            if (op.Status is not (OperationStatus.Ready or OperationStatus.Running)) continue;
            var previous = i == 0 ? null : ops[i - 1];
            if (OperationInput.IsComplete(i == 0, previous?.Status == OperationStatus.Completed && previous.IpqcHeldQty == 0m, Quantity, previous?.NetOutputQty ?? 0m, op.TakenQty))   // P4-3: while an in-process inspection still holds output of the operation before, this one is not done
                op.MarkCompleted(now);
        }
    }

    /// <summary>
    /// An in-process inspection passed (P4-3, BR-QC-05): its Good quantity is no longer held, so the next operation may take it - it becomes Ready and operations that now
    /// have all their input complete. Needs an order that is not Closed or Cancelled.
    /// </summary>
    public void ApplyInspectionPassed(int operationSeq, decimal lotQty, string by, DateTime now)
    {
        EnsureEditable();
        var op = OperationOf(operationSeq);
        op.PassHeld(lotQty);
        var next = Operations.FirstOrDefault(o => o.OperationSeq > op.OperationSeq);
        if (next is { Status: OperationStatus.Pending } && op.NetOutputQty > 0) next.TransitionTo(OperationStatus.Ready);
        CompleteSatisfiedOperations(now);
        Touch(by);
    }

    /// <summary>The in-process inspection of an operation was cancelled while Pending (its report was reversed). Not used by Fail: a failed inspection keeps the hold.</summary>
    public void CancelInspectionHold(int operationSeq, decimal lotQty) => OperationOf(operationSeq).ReleaseHold(lotQty);

    /// <summary>The operations that finished: the caller closes their open time logs.</summary>
    public IEnumerable<ProductionOrderOperation> CompletedOperations => Operations.Where(o => o.Status == OperationStatus.Completed);

    /// <summary>A supervisor completes an operation by hand (BR-OP-06): it must be Ready or Running. The route is walked again, so later operations may complete too.</summary>
    public void CompleteOperation(int operationSeq, string by, DateTime now)
    {
        EnsureOpenForWork();
        var op = OperationOf(operationSeq);
        MfgRules.ThrowIf(op.Status == OperationStatus.Completed, MfgErrors.Op009);
        MfgRules.ThrowIf(op.Status is not (OperationStatus.Ready or OperationStatus.Running), MfgErrors.Op001);
        op.MarkCompleted(now);
        CompleteSatisfiedOperations(now);
        Touch(by);
    }

    /// <summary>
    /// A supervisor reopens a completed operation: it is Ready again. Refused (<c>MFG_OP_007</c>) when the next operation already has work, because that work was fed
    /// by the output of this one.
    /// </summary>
    public void ReopenOperation(int operationSeq, string by)
    {
        EnsureOpenForWork();
        var op = OperationOf(operationSeq);
        MfgRules.ThrowIf(op.Status != OperationStatus.Completed, MfgErrors.Po002, ("from", op.Status), ("to", OperationStatus.Ready));
        var next = Operations.FirstOrDefault(o => o.OperationSeq > operationSeq);
        MfgRules.ThrowIf(next is { HasWork: true }, MfgErrors.Op007, ("detail", ""));
        op.MarkReopened();
        Touch(by);
    }

    /// <summary>The Warehouse reservation now matches the order: store its id and what each line holds, clear the flag.</summary>
    public void ApplyReservation(Guid? reservationId, IReadOnlyDictionary<int, decimal> reservedByLine)
    {
        ReservationId = reservationId;
        foreach (var m in _materials.Where(x => !x.IsDeleted)) m.SetReserved(reservedByLine.GetValueOrDefault(m.LineNo));
        ReservationSyncPending = false;
        ReservationSyncAttempts = 0;
        ReservationSyncError = null;
        ReservationNextAttemptAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReservationSync()
    {
        ReservationSyncPending = true;
        ReservationSyncAttempts = 0;
        ReservationSyncError = null;
        ReservationNextAttemptAt = null;
    }

    /// <summary>The sync could not finish (Warehouse unreachable or refused): keep the flag, remember why, try again at <paramref name="nextAttempt"/>.</summary>
    public void RecordReservationSyncFailure(string error, DateTime nextAttempt)
    {
        ReservationSyncAttempts++;
        ReservationSyncError = error.Length > 500 ? error[..500] : error;
        ReservationNextAttemptAt = nextAttempt;
        UpdatedAt = DateTime.UtcNow;
    }

    private void Touch(string changedBy)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = changedBy;
    }

    private static bool HasAtMostDecimals(decimal value, int places) => Math.Round(value, places) == value;
}
