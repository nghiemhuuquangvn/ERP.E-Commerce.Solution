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
    decimal? BatchSize, int QueueTimeMinutes, int MoveTimeMinutes, bool IsSubcontract, decimal StandardMinutes, int CalendarMinutes);

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

    /// <summary>Net quantity still out on the shop floor: issued minus returned (BR-MAT-02).</summary>
    public decimal NetIssuedQty => IssuedQty - ReturnedQty;

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

    internal static ProductionOrderOperation Create(Guid orderId, OrderOperationValues v) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, OperationSeq = v.OperationSeq, Name = v.Name, WorkCenterId = v.WorkCenterId, WorkCenterCode = v.WorkCenterCode,
        SetupTimeMinutes = v.SetupTimeMinutes, RunTimeMinutes = v.RunTimeMinutes, RunTimeBasis = v.RunTimeBasis, BatchSize = v.BatchSize,
        QueueTimeMinutes = v.QueueTimeMinutes, MoveTimeMinutes = v.MoveTimeMinutes, IsSubcontract = v.IsSubcontract,
        StandardMinutes = v.StandardMinutes, CalendarMinutes = v.CalendarMinutes,
    };

    internal void SetPlan(DateTime start, DateTime end) { PlannedStart = start; PlannedEnd = end; UpdatedAt = DateTime.UtcNow; }

    /// <summary>Changes the state through the machine of BA spec 01 §4.3.</summary>
    public void TransitionTo(OperationStatus target)
    {
        OperationStateMachine.EnsureTransition(Status, target);
        Status = target;
        UpdatedAt = DateTime.UtcNow;
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
    public bool ReservationReleasePending { get; private set; }

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

    private void Touch(string changedBy)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = changedBy;
    }

    private static bool HasAtMostDecimals(decimal value, int places) => Math.Round(value, places) == value;
}
