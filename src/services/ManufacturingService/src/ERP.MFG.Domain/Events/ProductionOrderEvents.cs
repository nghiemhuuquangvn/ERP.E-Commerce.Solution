using ERP.MFG.Domain.Common;
using ERP.Shared.Contracts;
using ERP.Shared.Core.Domain;

namespace ERP.MFG.Domain.Events;

// Integration events of the production order (BA spec 01 §8.1). They leave the service through the outbox on Kafka topic "manufacturing-topic";
// the writer stamps TenantId before serialising. Plain classes with settable properties so Newtonsoft restores every value (EventId survives).

public class ProductionOrderCreatedEvent : IntegrationEventBase, ITenantEvent
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public Guid PlantId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateOnly DueDate { get; set; }
    public OrderSourceType SourceType { get; set; }
}

public class ProductionOrderStatusChangedEvent : IntegrationEventBase, ITenantEvent
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public Guid PlantId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public Guid? ReasonCodeId { get; set; }
    public string? Note { get; set; }
    public string? ChangedBy { get; set; }
}

public class ProductionOrderReleasedEvent : IntegrationEventBase, ITenantEvent
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public Guid PlantId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly DueDate { get; set; }
}

public class ProductionOrderQuantityChangedEvent : IntegrationEventBase, ITenantEvent
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public decimal OldQuantity { get; set; }
    public decimal NewQuantity { get; set; }
    public string? ChangedBy { get; set; }
}

/// <summary>One line of a posted stock document: the item, the lot, how much and at what cost.</summary>
public class StockMovementLine
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public Guid? LotId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? Amount { get; set; }
}

public class MaterialIssuedEvent : IntegrationEventBase, ITenantEvent
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocNo { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public Guid PlantId { get; set; }
    public DateOnly PostingDate { get; set; }
    public Guid? ReasonCodeId { get; set; }
    public List<StockMovementLine> Lines { get; set; } = [];
}

public class MaterialReturnedEvent : IntegrationEventBase, ITenantEvent
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocNo { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public Guid PlantId { get; set; }
    public DateOnly PostingDate { get; set; }
    public Guid? ReasonCodeId { get; set; }
    public List<StockMovementLine> Lines { get; set; } = [];
}

public class MaterialBackflushedEvent : IntegrationEventBase, ITenantEvent
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocNo { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public Guid PlantId { get; set; }
    public DateOnly PostingDate { get; set; }
    public Guid? SourceOperationReportId { get; set; }
    public List<StockMovementLine> Lines { get; set; } = [];
}

/// <summary>An operation report is applied to the order (BA spec 01 §8.1): the figures of one report, after its backflush (if any) reached the Warehouse.</summary>
public class OperationReportedEvent : IntegrationEventBase, ITenantEvent
{
    public Guid TenantId { get; set; }
    public Guid ReportId { get; set; }
    public string DocNo { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public Guid PlantId { get; set; }
    public int OperationSeq { get; set; }
    public decimal GoodQty { get; set; }
    public decimal ScrapQty { get; set; }
    public decimal ReworkQty { get; set; }
    public Guid? ScrapReasonId { get; set; }
    public Guid? ReworkReasonId { get; set; }
    public Guid? MachineId { get; set; }
    public string? ReportedBy { get; set; }
}
