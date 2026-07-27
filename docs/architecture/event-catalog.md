# Event Catalog

Complete reference of all integration events in the ERP E-Commerce Solution. This document defines event contracts, publishers, subscribers, and usage patterns.

## Event Naming Convention

**Format:** `{AggregateRoot}{Action}Event`

Examples:
- `UserCreatedEvent`
- `SalesOrderApprovedEvent`
- `InventoryReservedEvent`

## Identity Service Events

### UserCreatedEvent

**Description:** Published when a new user is created in the system.

**Publisher:** Identity Service  
**Subscribers:** Sales Service, Purchasing Service, Warehouse Service

**Event Structure:**
```csharp
public class UserCreatedEvent : IntegrationEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public List<string> Roles { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed (Outbox Pattern)

**Handling:**
- Sales Service: Initialize customer profile
- Purchasing Service: Initialize supplier/buyer profile
- Warehouse Service: Assign warehouse permissions

---

### UserUpdatedEvent

**Description:** Published when user information is updated.

**Publisher:** Identity Service  
**Subscribers:** All services (optional)

**Event Structure:**
```csharp
public class UserUpdatedEvent : IntegrationEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public List<string> Roles { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed (Outbox Pattern)

---

### RoleAssignedEvent

**Description:** Published when a role is assigned to a user.

**Publisher:** Identity Service  
**Subscribers:** Authorization cache services

**Event Structure:**
```csharp
public class RoleAssignedEvent : IntegrationEvent
{
    public Guid UserId { get; set; }
    public string RoleName { get; set; }
    public DateTime AssignedAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

### TwoFactorCodeGeneratedEvent

**Description:** Published when a user with 2FA enabled/required completes password
verification during login — carries the plaintext OTP for delivery. The DB only ever
stores the hash (`TwoFactorChallenge.CodeHash`); this event is the one place the
plaintext code exists outside memory, and only transiently, in flight to Kafka.

**Publisher:** Identity Service (`identity-topic`, via the Outbox pattern —
`AuthDbContext.SaveChangesAsync` serializes the raw domain event, not a wrapped
integration-event contract)
**Subscribers:** Notification Service (`IdentityEventConsumer`) — sends the code via
SMS or Email per `Method`

**Event Structure (domain event, as actually serialized):**
```csharp
public record TwoFactorCodeGeneratedDomainEvent(
    Guid UserId,
    Guid TenantId,
    string Method,        // "Sms" | "Email"
    string Destination,   // masked phone/email for display
    string Code,          // plaintext OTP, one-shot
    int ExpiryMinutes,
    DateTime OccurredOn) : IDomainEvent;
```

**Routing:** Kafka (`identity-topic`)
**Delivery:** Outbox pattern (at-least-once)

---

## Sales Service Events

### SalesOrderCreatedEvent

**Description:** Published when a new sales order is created.

**Publisher:** Sales Service  
**Subscribers:** Warehouse Service, Accounting Service (optional)

**Event Structure:**
```csharp
public class SalesOrderCreatedEvent : IntegrationEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderLineItem> LineItems { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderLineItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed (Outbox Pattern)

**Handling:**
- Warehouse Service: Trigger inventory reservation process
- Accounting Service: Record sales transaction

---

### SalesOrderApprovedEvent

**Description:** Published when a sales order is approved.

**Publisher:** Sales Service  
**Subscribers:** Warehouse Service, Accounting Service

**Event Structure:**
```csharp
public class SalesOrderApprovedEvent : IntegrationEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime ApprovedAt { get; set; }
    public decimal TotalAmount { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

**Handling:**
- Warehouse Service: Confirm inventory reservation and trigger fulfillment
- Accounting Service: Finalize invoice

---

### SalesOrderShippedEvent

**Description:** Published when a sales order is shipped.

**Publisher:** Sales Service  
**Subscribers:** Warehouse Service, Accounting Service, Customer Notifications

**Event Structure:**
```csharp
public class SalesOrderShippedEvent : IntegrationEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string TrackingNumber { get; set; }
    public DateTime ShippedAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

### SalesOrderCancelledEvent

**Description:** Published when a sales order is cancelled.

**Publisher:** Sales Service  
**Subscribers:** Warehouse Service, Accounting Service

**Event Structure:**
```csharp
public class SalesOrderCancelledEvent : IntegrationEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CancellationReason { get; set; }
    public DateTime CancelledAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

**Handling:**
- Warehouse Service: Release reserved inventory
- Accounting Service: Issue refund

---

### SalesOrderPartiallyShippedEvent

> Added 2026-07-10 — existed in code (`ERP.SO.Domain/Events/SalesOrderPartiallyShippedEvent.cs`)
> since the 2026-07-03 partial-fulfillment work but was never added to this catalog.

**Description:** Published when a subset of an order's line items is shipped
(`SalesOrder.ShipLineItems()`, when at least one line still has a remaining
quantity after the shipment).

**Publisher:** Sales Service  
**Subscribers:** None confirmed today (see `docs/business/Sales/SalesWorkflow.md`)

**Event Structure (actual code):**
```csharp
public class SalesOrderPartiallyShippedEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string TrackingNumber { get; set; }
    public string Carrier { get; set; }
    public List<ShippedLine> ShippedLines { get; set; }
    public DateTime ShippedAt { get; set; }
}

public class ShippedLine
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

### SalesOrderDeliveredEvent

> Added 2026-07-10 — existed in code since 2026-07-03 but was never added to this catalog.

**Description:** Published when `SalesOrder.MarkAsDelivered()` moves the order
from `Shipped` to `Delivered`.

**Publisher:** Sales Service  
**Subscribers:** None confirmed today

**Event Structure (actual code):**
```csharp
public class SalesOrderDeliveredEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime DeliveredAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

### SalesOrderReturnedEvent

> Added 2026-07-10 — existed in code since 2026-07-03 but was never added to this catalog.

**Description:** Published when `SalesOrder.CompleteReturn(refundAmount)` moves
the order from `ReturnRequested` to `Returned`. Recording the refund amount on
this event is the *only* thing that happens synchronously — PaymentService/
AccountingService are expected to react to this event to actually process the
refund/reverse the journal entry, but no consumer for it has been confirmed
yet (see `docs/business/Sales/SalesWorkflow.md`).

**Publisher:** Sales Service  
**Subscribers:** None confirmed today (intended: Payment Service, Accounting Service)

**Event Structure (actual code):**
```csharp
public class SalesOrderReturnedEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Reason { get; set; }
    public decimal RefundAmount { get; set; }
    public DateTime ReturnedAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

### SalesQuotationCreatedEvent

> Added 2026-07-27 — part of the Module 5 (Sales) doc reconciliation that added a
> SalesQuotation aggregate to SaleOrderService (Quotation → customer approval →
> convert-to-order flow). No consumer confirmed yet; published for future
> Reporting/analytics use.

**Description:** Published when `SalesQuotation.Create(...)` creates a new Draft quotation.

**Publisher:** Sales Service
**Subscribers:** None confirmed today

**Event Structure (actual code):**
```csharp
public class SalesQuotationCreatedEvent
{
    public Guid QuotationId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Routing:** RabbitMQ (`sales_order_events` exchange, via the same Outbox retrofit as the SalesOrder events above — see `SalesOrderEventOutboxProcessor`)
**Delivery:** Guaranteed

---

### SalesQuotationAcceptedEvent

> Added 2026-07-27 — same reconciliation as SalesQuotationCreatedEvent above.

**Description:** Published when `SalesQuotation.Accept()` records the customer's
approval — the gate that must pass before `ConvertQuotationToOrderCommand` can
turn the quotation into a real `SalesOrder`.

**Publisher:** Sales Service
**Subscribers:** None confirmed today

**Event Structure (actual code):**
```csharp
public class SalesQuotationAcceptedEvent
{
    public Guid QuotationId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime AcceptedAt { get; set; }
}
```

**Routing:** RabbitMQ (`sales_order_events` exchange)
**Delivery:** Guaranteed

---

## Purchasing Service Events

### PurchaseOrderCreatedEvent

**Description:** Published when a new purchase order is created.

**Publisher:** Purchasing Service  
**Subscribers:** Warehouse Service, Accounting Service

**Event Structure:**
```csharp
public class PurchaseOrderCreatedEvent : IntegrationEvent
{
    public Guid PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<PurchaseLineItem> LineItems { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PurchaseLineItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed (Outbox Pattern)

**Handling:**
- Warehouse Service: Prepare receiving dock
- Accounting Service: Record liability

---

### PurchaseOrderApprovedEvent

**Description:** Published when a purchase order is approved.

**Publisher:** Purchasing Service  
**Subscribers:** Accounting Service

**Event Structure:**
```csharp
public class PurchaseOrderApprovedEvent : IntegrationEvent
{
    public Guid PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime ApprovedAt { get; set; }
    public decimal TotalAmount { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

### ReceiptScheduledEvent

> Added 2026-07-10 — existed in code (`ERP.PO.Domain/Events/ReceiptScheduledDomainEvent.cs`)
> since the 2026-07-03 GRN work but was never added to this catalog.

**Description:** Published by `PurchaseOrder.ScheduleReceipt(date)` when a
receiving dock/date is planned ahead of the actual goods receipt. Does not
change `PurchaseOrderStatus`.

**Publisher:** Purchasing Service  
**Subscribers:** None today — see `docs/business/Purchasing/PurchasingWorkflow.md`
(intended: Warehouse Service, to plan dock/labor ahead of the GRN)

**Event Structure (actual code):**
```csharp
public class ReceiptScheduledDomainEvent
{
    public Guid PurchaseOrderId { get; set; }
    public string PONumber { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime OccurredOn { get; set; }
}
```

**Routing:** Kafka (`purchasing-topic`, Outbox Pattern)  
**Delivery:** Guaranteed

---

### PurchaseOrderReceivedEvent

**Description:** Published when a purchase order is received (GRN).

**Publisher:** Purchasing Service  
**Subscribers:** Warehouse Service (updates inventory by matching `ProductCode`
to SKU); Accounting Service subscription (`Record actual costs`) below is the
intended design — not confirmed wired today.

**Event Structure (actual code, `PurchaseOrderReceivedDomainEvent`, updated 2026-07-10):**
```csharp
public class PurchaseOrderReceivedDomainEvent
{
    public Guid PurchaseOrderId { get; set; }
    public string PONumber { get; set; }
    public Guid SupplierId { get; set; }
    public bool IsFullyReceived { get; set; }
    public List<ReceivedLine> ReceivedItems { get; set; }
    public DateTime OccurredOn { get; set; }
}

public class ReceivedLine
{
    public string ProductCode { get; set; }
    public decimal QuantityReceived; // accepted quantity only
    public decimal QuantityRejected;
}
```

> **Resolved 2026-07-10:** `ReceiveGoodsCommand` now accepts a
> `QuantityAccepted`/`QuantityRejected` split per line (previously a single
> quantity, matching the gap this catalog used to flag). `QuantityReceived`
> keeps its field name for wire compatibility but now means *accepted only*;
> `QuantityRejected` is new and has no consumer yet (WarehouseService still
> only reads `QuantityReceived`).

**Routing:** Kafka (`purchasing-topic`, Outbox Pattern)  
**Delivery:** Guaranteed

**Handling:**
- Warehouse Service: Update inventory levels
- Accounting Service: Record actual costs

---

## Warehouse Service Events

### InventoryReservedEvent

**Description:** Published when inventory is reserved for a sales order.

**Publisher:** Warehouse Service  
**Subscribers:** Sales Service, Accounting Service

**Event Structure:**
```csharp
public class InventoryReservedEvent : IntegrationEvent
{
    public Guid ReservationId { get; set; }
    public Guid OrderId { get; set; }
    public List<ReservedItem> ReservedItems { get; set; }
    public DateTime ReservedAt { get; set; }
}

public class ReservedItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string WarehouseLocation { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed (Outbox Pattern)

**Handling:**
- Sales Service: Confirm order can be fulfilled
- Accounting Service: Update inventory valuation

---

### InventoryUpdatedEvent

**Description:** Published when inventory levels change due to stock movement.

**Publisher:** Warehouse Service  
**Subscribers:** Sales Service, Purchasing Service, Analytics (Kafka)

**Event Structure:**
```csharp
public class InventoryUpdatedEvent : IntegrationEvent
{
    public Guid ProductId { get; set; }
    public int PreviousQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public string MovementType { get; set; } // "Received", "Sold", "Adjusted", "Damaged"
    public Guid? ReferenceId { get; set; } // OrderId, PurchaseOrderId, etc.
    public DateTime UpdatedAt { get; set; }
}
```

**Routing:** RabbitMQ (primary), Kafka (analytics)  
**Delivery:** Guaranteed

**Handling:**
- Sales Service: Check stock availability
- Purchasing Service: Trigger reorder if low stock
- Analytics: Real-time inventory metrics

---

### StockMovementRecordedEvent

**Description:** Published when a stock movement is recorded (receive, issue, adjustment).

**Publisher:** Warehouse Service  
**Subscribers:** Accounting Service, Analytics

**Event Structure:**
```csharp
public class StockMovementRecordedEvent : IntegrationEvent
{
    public Guid MovementId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string MovementType { get; set; }
    public string Reason { get; set; }
    public Guid? SourceOrderId { get; set; }
    public DateTime RecordedAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

## Accounting Service Events (Optional)

### InvoiceGeneratedEvent

**Description:** Published when an invoice is generated for a sales order.

**Publisher:** Accounting Service  
**Subscribers:** Sales Service

**Event Structure:**
```csharp
public class InvoiceGeneratedEvent : IntegrationEvent
{
    public Guid InvoiceId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime GeneratedAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

### PaymentReceivedEvent

**Description:** Published when payment is received for an invoice.

**Publisher:** Accounting Service  
**Subscribers:** Sales Service, Warehouse Service

**Event Structure:**
```csharp
public class PaymentReceivedEvent : IntegrationEvent
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; }
    public DateTime PaidAt { get; set; }
}
```

**Routing:** RabbitMQ  
**Delivery:** Guaranteed

---

## Shared Master Data Service Events

### EmployeeHiredEvent

**Description:** Published when a new employee is hired (master record, owned by SharedMasterDataService).

**Publisher:** SharedMasterDataService
**Subscribers:** HRMService (syncs a local, read-only EmployeeSnapshot projection)

**Event Structure:**
```csharp
public record EmployeeHiredEvent(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    string? Email,
    Guid DepartmentId) : IDomainEvent;
```

**Routing:** Kafka (`mst-topic`)
**Delivery:** Guaranteed (Outbox Pattern)

**Handling:**
- HRMService: Upsert local EmployeeSnapshot (used by leave/payroll handlers instead of a synchronous cross-service call)

---

### EmployeeTerminatedEvent / EmployeeTransferredEvent

Same publisher/topic/delivery as EmployeeHiredEvent. HRMService consumes both to keep its
EmployeeSnapshot projection's `EmploymentStatus`/`DepartmentId` current. See
`src/services/SharedMasterDataService/src/ERP.MST.Domain/Events/MasterDataEvents.cs` for
full definitions, and `src/services/HRMService/src/ERP.HRM.Infrastructure/BackgroundJobs/MasterDataEventConsumer.cs`
for the consumer.

### DepartmentCreatedEvent / DepartmentUpdatedEvent / PositionCreatedEvent / UnitOfMeasureCreatedEvent / CountryCreatedEvent

**Publisher:** SharedMasterDataService
**Subscribers:** None yet (published for future consumers; reference/lookup data is
otherwise fetched via `GET /api/v1/masterdata/...` REST calls, not events)

**Routing:** Kafka (`mst-topic`)

---

## Event Processing Guarantees

### Delivery Semantics

| Pattern | Guarantee | Use Case |
|---------|-----------|----------|
| Outbox Pattern | Exactly Once | Critical business events |
| At-Least-Once | Eventual Consistency | Non-critical updates |
| At-Most-Once | Performance Priority | Read-only operations |

### Retry Policy

- **Immediate Retries**: 3 attempts within 5 seconds
- **Delayed Retries**: Exponential backoff (30s, 5m, 30m, 2h)
- **Dead Letter Queue**: After 5 total failures

### Event Ordering

- Events from the same aggregate are processed in order
- Cross-aggregate ordering is not guaranteed
- Use event versioning for schema changes

## Event Versioning

When modifying event schemas:

1. **Add new optional properties** - No versioning needed
2. **Remove properties** - Create new event version (e.g., `UserCreatedEventV2`)
3. **Change property types** - Create new event version
4. **Rename properties** - Create new event version

Example:
```csharp
public class UserCreatedEventV2 : IntegrationEvent
{
    // ... existing properties ...
    
    // New in V2
    public string PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
}
```

## Event Publishing

### From Domain Entity

```csharp
public class SalesOrder : AggregateRoot
{
    public void ApproveOrder()
    {
        // Business logic validation...
        
        this.Status = OrderStatus.Approved;
        
        this.AddDomainEvent(new SalesOrderApprovedEvent
        {
            OrderId = this.Id,
            CustomerId = this.CustomerId,
            ApprovedAt = DateTime.UtcNow,
            TotalAmount = this.TotalAmount
        });
    }
}
```

### Publishing via Bus

```csharp
public class ApproveOrderCommandHandler : IRequestHandler<ApproveOrderCommand>
{
    private readonly IEventBus _eventBus;
    
    public async Task Handle(ApproveOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId);
        order.ApproveOrder();
        
        await _orderRepository.UpdateAsync(order);
        
        // Publish domain events
        foreach (var domainEvent in order.GetDomainEvents())
        {
            await _eventBus.PublishAsync(domainEvent, cancellationToken);
        }
    }
}
```

## Event Subscription

### Service Registration

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services
        .AddEventBus()
        .SubscribeTo<SalesOrderCreatedEvent>(typeof(SalesOrderCreatedEventHandler))
        .SubscribeTo<SalesOrderApprovedEvent>(typeof(SalesOrderApprovedEventHandler));
}
```

### Event Handler

```csharp
public class SalesOrderCreatedEventHandler : IIntegrationEventHandler<SalesOrderCreatedEvent>
{
    private readonly IInventoryService _inventoryService;
    
    public async Task HandleAsync(SalesOrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        // Reserve inventory for the order
        await _inventoryService.ReserveInventoryAsync(@event.OrderId, @event.LineItems, cancellationToken);
    }
}
```

## Cross-Cutting / Observability Events

### ServiceLogEvent

**Description:** Wire contract for the centralized log management feature (Information/Warning/Error/Exception logs across services and frontend apps, filterable by service/date/level — see `docs/business` pilot notes). Published by every Serilog-enabled backend service via the shared `KafkaLogSink` (pilot: Gateway, ProductService, IdentityService); frontend apps (pilot: Admin) instead POST this same shape directly to LoggingService's client-ingestion endpoint since they cannot produce to Kafka.

**Publisher:** Any pilot service's shared Serilog pipeline (`AddSerilogLogging`) + Admin frontend's client logger  
**Subscribers:** LoggingService (sole subscriber; indexes into Elasticsearch)

**Event Structure:**
```csharp
public class ServiceLogEvent : IntegrationEventBase
{
    public string ServiceName { get; set; }
    public string? Environment { get; set; }
    public LogSeverity Level { get; set; } // Information, Warning, Error, Critical
    public LogKind Kind { get; set; }       // Trace, Exception
    public string Message { get; set; }
    public string? ExceptionType { get; set; }  // set only when Kind = Exception
    public string? StackTrace { get; set; }     // set only when Kind = Exception
    public string? CorrelationId { get; set; }
    public string? Source { get; set; }         // "Backend" | "Frontend"
    public Dictionary<string, string>? Properties { get; set; }
}
```

**Routing:** Kafka (`system-logs-topic`) for backend services; HTTP `POST /logs/api/v1/logs/client` (via Ocelot Gateway, unauthenticated) for frontend apps  
**Delivery:** Best-effort, fire-and-forget — logs are diagnostic, not business-critical, so no outbox/transactional guarantee is used. A Kafka sink failure degrades to a `Console.Error` line rather than blocking or crashing the publishing service.

**Handling:**
- LoggingService: indexes the event into the `service-logs-{yyyy.MM}` Elasticsearch index (`Kind` derived automatically from whether the originating Serilog `LogEvent` carried an exception — no publisher call sites need to set it explicitly)

---

### SettingsChangedIntegrationEvent

**Description:** Published whenever any of the 22 Settings categories' System or
Tenant row is updated (see `docs/business/Settings/SettingsOverview.md`), so a
service that depends on another service's setting doesn't keep acting on a value it
cached before the other service's write. Deliberately carries **no** changed field
values — a consumer that cares re-fetches the category's current value via that
service's own Settings API, so it never needs to understand another service's
Settings schema.

**Publisher:** All 9 Settings-owning services, each via its own Outbox pattern (see
table below for topic per service).
**Subscribers:** ProductService and SaleOrderService — both filter for
`ServiceName == "SharedMasterDataService" && Category == "Currency"` only. No other
category has a consumer today (deliberately not built speculatively — see
`docs/business/Settings/SettingsOverview.md#event-driven-sync`).

**Event Structure:**
```csharp
public class SettingsChangedIntegrationEvent : IntegrationEventBase
{
    public string ServiceName { get; set; } = default!;   // "IdentityService", "SharedMasterDataService", ...
    public string Category { get; set; } = default!;      // "Auth", "Inventory", "Currency", ...
    public string Scope { get; set; } = default!;          // "System" | "Tenant"
    public Guid? TenantId { get; set; }                    // set only when Scope == "Tenant"
}
```

**Routing per publisher:**

| Publisher | Topic | Notes |
|---|---|---|
| IdentityService | `identity-topic` | Existing Outbox / local `IMessageBus` / `OutboxProcessor` — no new infrastructure, reused as-is |
| ProductService | `product-topic` | Existing Outbox→Kafka pipeline, reused as-is |
| PaymentService | `payment-topic` | Existing Outbox→Kafka pipeline, reused as-is |
| ShippingService | `shipping-topic` | Existing Outbox→Kafka pipeline, reused as-is |
| AccountingService | `accounting-topic` | Existing Outbox→Kafka pipeline, reused as-is |
| SharedMasterDataService | `mst-topic` | Existing Outbox→Kafka pipeline, reused as-is — this is the topic ProductService/SaleOrderService actually consume |
| NotificationService | `notification-topic` | **New** — this service had zero publish capability before this event; new `KafkaMessageBus` + `OutboxProcessor` added |
| SaleOrderService | `sale-order-settings-topic` | **New**, isolated Kafka producer + Outbox — deliberately separate from this service's existing RabbitMQ `sales_order_events` domain-event publishing, so that flow could not regress |
| WareHouseService | `warehouse-settings-topic` | **New**, isolated Kafka producer + Outbox — deliberately separate from this service's existing RabbitMQ `warehouse_exchange` domain-event publishing |

**Delivery:** Outbox pattern (at-least-once) for every publisher.

**Handling:**
- ProductService (`CurrencySettingsConsumer`, its first-ever Kafka consumer) and
  SaleOrderService (`CurrencySettingsConsumer`, alongside its existing
  `PaymentKafkaConsumer`/`ProductPriceChangedConsumer`/`ShippingKafkaConsumer`) both
  subscribe to `mst-topic` and, on a matching Currency event, invalidate
  `currency-settings:system` or `currency-settings:tenant:{tenantId}` in their own
  local cache. **No visible effect today** — neither service actually fetches/caches
  Currency Settings from SharedMasterDataService anywhere yet; this is
  forward-compatible plumbing.

---

### TenantLicenseChangedIntegrationEvent

**Description:** Published by IdentityService whenever a tenant's license is issued,
renewed, suspended, or revoked (see `docs/business/Identity/License.md`).
Deliberately carries no module list — a consumer that needs the current entitlement
calls IdentityService's `GET /api/license/tenant/{tenantId}` API. Cross-service
*enforcement* of module entitlement does not depend on this event at all — it is
JWT-claim-based (see License.md), so this event exists purely for notification and
analytics, not for cache invalidation.

**Publisher:** IdentityService, via its existing Outbox / local `IMessageBus` /
`OutboxProcessor` on `identity-topic` — no new publish infrastructure, reused as-is
(same mechanism `SettingsChangedIntegrationEvent` uses from this service).
**Subscribers:** None wired yet — intended for NotificationService (alert the tenant
admin) and ReportingService (analytics), matching the "publish now, wire narrow
consumers later" precedent already used for `SettingsChangedIntegrationEvent`.

**Event Structure:**
```csharp
public class TenantLicenseChangedIntegrationEvent : IntegrationEventBase
{
    public Guid TenantId { get; set; }
    public string Action { get; set; } = default!; // "Issued" | "Renewed" | "Suspended" | "Revoked"
}
```

**Delivery:** Outbox pattern (at-least-once).

**Handling:** None live today — no consumer is wired to `identity-topic` for this
event type yet (see `docs/business/Identity/License.md` for the enforcement model,
which does not require a consumer).

---

## Monitoring & Troubleshooting

### Event Metrics

- **Published**: Count of events published per type
- **Consumed**: Count of events consumed per type
- **Failed**: Count of failed event processing
- **Latency**: Time from publishing to consumption

### Dead Letter Handling

Events that fail processing multiple times are moved to Dead Letter Queue:

1. Check `rabbitmq_dlq_*` queues
2. Investigate root cause in logs
3. Fix the issue
4. Replay events from DLQ

### Event Tracing

All events include correlation IDs for end-to-end tracing:

```
Event Published → [Service A] → Event Consumed → [Service B] → Updated
(CorrelationId: abc-123)
```

---

**Last Updated:** 2026-07-16  
**Version:** 1.2 (added `TenantLicenseChangedIntegrationEvent`, License Management)
