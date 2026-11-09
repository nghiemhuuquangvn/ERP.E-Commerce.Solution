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
