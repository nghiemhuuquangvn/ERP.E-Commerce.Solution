# Event Catalog

Complete reference of all integration events in the ERP E-Commerce Solution. This document defines event contracts, publishers, subscribers, and usage patterns.

## Event Naming Convention

**Format:** `{AggregateRoot}{Action}Event`

Examples:
- `UserCreatedEvent`
- `SalesOrderApprovedEvent`
- `InventoryReservedEvent`

## Tenant in every event (2026-10-20)

Every event that leaves a service carries the tenant it belongs to — decision 2026-10-20
(tenant isolation phase 2), so a consumer never guesses:

- **Contract:** the event class implements `ERP.Shared.Core.Domain.ITenantEvent`
  (`Guid TenantId { get; set; }`). This applies to every event listed below even where the
  C# sketch omits it; IdentityService events already declared `TenantId` explicitly.
  `SettingsChangedIntegrationEvent` keeps its own `Guid? TenantId` (null = platform-wide).
- **Publisher:** the Outbox writer stamps it from the entity that raised the event, or from
  the current tenant (`EventTenant.StampFrom` / `EventTenant.Stamp`) before serializing.
  SaleOrder saga commands (`InitiatePayment`, `ReserveInventory`, `CreateShipment`,
  `RefundPayment`) already carried `TenantId`.
- **Consumer:** `EventTenant.TryRead(json, out var tenantId)` → handle inside
  `TenantScope.Begin(tenantId)`. **No tenant = the event is logged and skipped** (fail
  closed). When the event refers to the consumer's own data (e.g. Payment/Warehouse/Shipping
  events about a sales order), that data decides the tenant and an event naming another
  tenant is dropped.
- **Deployment:** events written to an Outbox before this release have no `TenantId` and
  are skipped by upgraded consumers — deploy publishers and consumers together and let old
  Outboxes drain first. See `docs/PROJECT-STATUS.md` §3.

## Flow fixes 2026-10-22 (ROADMAP P0)

These reflect the code; the per-event sections below are older and partly aspirational.

| Event / message | Transport | Change | Consumer |
|---|---|---|---|
| `PurchaseOrderApprovedDomainEvent` | Kafka `purchasing-topic` | Now carries `TotalAmount` + `CurrencyCode` (manager approval below the threshold, or finance approval). PO lines can only change while Draft. | **AccountingService** books the supplier bill from it, once per PO (redelivery-safe). Notification unchanged. |
| `PurchaseOrderCreatedDomainEvent` | Kafka `purchasing-topic` | — | **No longer** creates a bill (it is raised before any line exists, so every bill was 0). |
| `CreateShipmentCommand` (saga) | RabbitMQ `sales_order_events` | Now carries `RecipientName`, `RecipientEmail`, `ShippingAddress` (Street/City/State/PostalCode/Country) and `Items` (lines still to ship). | **ShippingService**: City → district, State → province; a command without an address is logged and dropped (no placeholder shipment). |
| `TransactionCreatedEvent` (POS) | Kafka `pos-topic` | Now carries `Lines` (ProductId, ProductName, Quantity). 2026-10-24 (P1-1c): also `Amounts` (SubTotal, DiscountAmount, TaxAmount, Tenders[Method, Amount]), `CashShiftId`, `ExternalPaymentId`; `TransactionVoidedEvent` has `Amounts` + `CashShiftId`; `CashShiftClosedEvent` has `OpeningBalance`, `CountedCash`, `ExpectedCash`, `CashVariance`. Messages are keyed by transaction / shift id. **AccountingService** (`AccountingKafkaConsumer`) posts sale / void / variance. | **WarehouseService** (`PosTransactionConsumer`) takes the lines out of stock — never refuses, stock may go negative + `StockAlert`; reference `POS-{tx}`, idempotent. Reporting unchanged. |
| `TransactionVoidedEvent` (POS, new) | Kafka `pos-topic` | Raised only when a **completed** sale is voided. | **WarehouseService** puts the lines back (reference `POS-VOID-{tx}`). |

---

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

**Description:** Published when a role is assigned to a user. `ChangedByUserId`/
`ChangedByName` are null when the system made the assignment (e.g. self-registration's
default-role assignment) rather than another user — Notification Service uses this to
suppress a "changed by nobody" bell notification in that case.

**Publisher:** Identity Service (`identity-topic`, via the Outbox pattern —
`AuthDbContext.SaveChangesAsync` automatically drains every entity's raised domain events
into the Outbox on save; there is no separate `IDomainEventHandler<T>`/DI registration
step required per event — see `IDomainEventDispatcher`'s doc note below)  
**Subscribers:** Notification Service (`IdentityEventConsumer`) — sends an InApp bell
notification to the affected user when there's a human actor

**Event Structure (domain event, as actually serialized):**
```csharp
public class RoleAssignedDomainEvent : IDomainEvent
{
    public Guid UserId { get; }
    public Guid TenantId { get; }
    public Guid RoleId { get; }
    public string RoleCode { get; }
    public DateTime OccurredOn { get; }
    public Guid? ChangedByUserId { get; }
    public string? ChangedByName { get; }
}
```

**Routing:** Kafka (`identity-topic`)  
**Delivery:** Outbox pattern (at-least-once)

**Note:** IdentityService's `IDomainEventDispatcher`/`IDomainEventHandler<T>` classes
(e.g. `UserRegisteredDomainEventHandler`) are dead code — nothing ever calls
`DispatchAsync`. The real, working publish path is automatic:
`AuthDbContext.SaveChangesAsync` → `ProcessDomainEventsToOutboxAsync` serializes every
`AddDomainEvent(...)` call straight to the Outbox with `TypeNameHandling.All`, and
`OutboxProcessor` publishes it to Kafka. Adding a new published event to Identity only
requires raising it via `AddDomainEvent` on an aggregate — no handler class or DI wiring
needed.

---

### PermissionGrantedEvent

**Description:** Published when a permission is granted to a role. Since the event is
raised on `Role` (not `User`), every user currently holding that role is affected —
`AffectedUserIds` is resolved by `RoleService.AssignPermissionAsync` (Identity already
owns the `UserRoles` join) and embedded directly in the event, so the event stays fully
self-describing and Notification Service never needs to call back into Identity.
`ChangedByUserId`/`ChangedByName` follow the same null-means-system convention as
`RoleAssignedEvent`.

**Publisher:** Identity Service (`identity-topic`, same automatic Outbox path as
`RoleAssignedEvent` above)  
**Subscribers:** Notification Service (`IdentityEventConsumer`) — sends one InApp bell
notification per user in `AffectedUserIds` when there's a human actor

**Event Structure (domain event, as actually serialized):**
```csharp
public class PermissionGrantedDomainEvent : IDomainEvent
{
    public Guid RoleId { get; }
    public Guid TenantId { get; }
    public Guid PermissionId { get; }
    public string PermissionCode { get; }
    public string RoleCode { get; }
    public DateTime OccurredOn { get; }
    public Guid? ChangedByUserId { get; }
    public string? ChangedByName { get; }
    public IReadOnlyCollection<Guid> AffectedUserIds { get; }
}
```

**Routing:** Kafka (`identity-topic`)  
**Delivery:** Outbox pattern (at-least-once)

**Known follow-up (out of scope as of this writing):** revocation
(`RemovePermissionAsync`/`RemoveRoleAsync`) raises no domain event today — only grants
are covered.

---

### Pattern: publish-on-change → NotificationService bell (reusable)

The role/permission events above are the first end-to-end example of a general pattern —
"tell the affected user their thing changed, and by whom" — that any service can reuse
without a fresh design pass:

1. Add nullable `ChangedByUserId`/`ChangedByName` fields to the relevant domain event
   (null = system-made-the-change, which the notification handler should treat as "don't
   notify").
2. Raise the event via the aggregate's existing `AddDomainEvent(...)` call — most
   services' `DbContext.SaveChangesAsync` already drains this into the Outbox
   automatically (verify this for the specific service; IdentityService's
   `AuthDbContext` and NotificationService's own `NotificationDbContext` both do it this
   way already).
3. In `NotificationService`'s consumer for that service's Kafka topic (e.g.
   `IdentityEventConsumer`), add a `typeName.Contains("YourDomainEventName")` branch that
   parses the raw JSON into a local integration-event DTO and `mediator.Publish`es it.
4. Add the local `INotification` DTO + `INotificationHandler<T>` pair in
   `IntegrationEventHandlers.cs`, guarding on `ChangedByUserId is null` to skip
   system-made changes, and sending a `SendNotificationCommand` with
   `Channel = NotificationChannel.InApp`.
5. Delivery is automatic from there: `SendNotificationCommandHandler` delivers `InApp`
   immediately (not gated on priority) via `IRealtimeNotifier` → `NotificationHub`
   (`/hubs/notifications`), which pushes to the recipient's own SignalR group. No new
   hub/frontend work needed per-event-type — the bell is generic once the notification
   row exists.

---

### Accounting: events that now write journal entries (2026-10-23, ROADMAP P1-1)

Accounting turns these into ledger entries (docs/business/Accounting/AutoPosting.md): `SalesOrderCreated` → invoice + Dr AR / Cr
Revenue; `SalesOrderCancelled` (RabbitMQ `sales_order.SalesOrderCancelledEvent`, new binding) → unpaid invoice cancelled + entry
reversed; `PaymentProcessed` (needs `Method` as a number and `ProcessedAt`) → Dr Cash|Bank / Cr AR; `PurchaseOrderApproved` →
bill (posted when the bill is approved / paid). Consumed since 2026-10-24 (P1-1e): `PaymentRefunded` (only with a `RefundId`, i.e. gateway-confirmed) and `SalesOrderReturned` (RabbitMQ binding `sales_order.SalesOrderReturnedEvent`). The `warehouse-topic` subscription that this catalog and the dependency map list never received anything
(Warehouse publishes on RabbitMQ only) and has been removed.

---

### TwoFactorCodeGeneratedEvent

**Description:** Published when a user with 2FA enabled/required completes password
verification during login — carries the OTP for delivery. The DB only ever stores the
hash (`TwoFactorChallenge.CodeHash`). In the Outbox row and on `identity-topic` the `Code`
field is encrypted (`enc:v1:` + AES, `SecretEnvelope`; Notification opens it with the shared
`Security:SettingsEncryptionKey`) and the Outbox row is overwritten with `[redacted]` once
published. `PasswordResetRequested` (`ResetLink`) and `EmailConfirmationRequested`
(`ConfirmLink`) are protected the same way.

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
    string Destination,   // the real phone/email to deliver to (the challenge entity keeps a masked copy)
    string Code,          // one-shot OTP; "enc:v1:…" on the wire and in the Outbox, "[redacted]" after delivery
    int ExpiryMinutes,
    DateTime OccurredOn) : IDomainEvent;
```

**Routing:** Kafka (`identity-topic`)
**Delivery:** Outbox pattern (at-least-once)

---

### PasswordResetRequestedEvent

**Description:** Published by `POST /auth/forgot-password` when the email belongs to an
active account of an active tenant (unknown accounts get the same `200` response and no
event — no user enumeration). Carries the one-time reset LINK, which embeds the plaintext
token; `PasswordResetTokens` stores only its SHA-256 hash. Since 2026-10-19 this is the
ONLY way the token leaves IdentityService (it used to be returned in the HTTP response).

**Publisher:** Identity Service (`identity-topic`, Outbox — raised by
`PasswordResetToken.RaiseRequested`)
**Subscribers:** Notification Service (`IdentityEventConsumer` →
`PasswordResetRequestedIntegrationEventHandler`, template `PASSWORD_RESET`)
**Never delivered to tenant webhooks** (`OutboxProcessor.WebhookExcludedEventTypes`).

```csharp
public class PasswordResetRequestedDomainEvent : IDomainEvent
{
    Guid UserId; Guid TenantId; string Email;
    string ResetLink;     // Auth:PasswordResetUrl + ?token=<single-use token>
    int ExpiryMinutes;    // Auth:PasswordResetTokenExpiryMinutes (default 60)
    DateTime OccurredOn;
}
```

---

### EmailConfirmationRequestedEvent

**Description:** Published on self-service registration and tenant signup. Carries the
one-time confirmation LINK; `EmailConfirmTokens` stores only the token hash.
`POST /auth/confirm-email` requires `{ userId, token }` from that link.

**Publisher:** Identity Service (`identity-topic`, Outbox — raised by
`EmailConfirmToken.RaiseRequested`)
**Subscribers:** Notification Service (`IdentityEventConsumer` →
`EmailConfirmationRequestedIntegrationEventHandler`, template `EMAIL_CONFIRMATION`)
**Never delivered to tenant webhooks.**

```csharp
public class EmailConfirmationRequestedDomainEvent : IDomainEvent
{
    Guid UserId; Guid TenantId; string Email;
    string ConfirmLink;   // Auth:EmailConfirmationUrl + ?userId=..&token=..
    int ExpiryHours;      // Auth:EmailConfirmationTokenExpiryHours (default 48)
    DateTime OccurredOn;
}
```

**Routing:** Kafka (`identity-topic`) · **Delivery:** Outbox pattern (at-least-once)

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

> **Fixed 2026-07-27 (Module 8 reconciliation):** WareHouseService's handler for this
> event previously only flipped the matching `Reservation` to `Fulfilled` —
> `Inventory.TotalQuantity` was never actually decremented, so on-hand stock silently
> never reduced through normal order fulfillment. The handler now consumes the exact lots
> that were reserved, reduces `Inventory`/`InventoryLocationStock`, and writes a
> `CostConsumptionLog` row per lot (see Warehouse Service Events below).

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
**Subscribers:** **AccountingService** since 2026-10-24 (credit note: Dr Sales returns / Cr Receivables). Not yet: Payment (refund), Warehouse (restock) — ROADMAP P1-5.

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
**Subscribers:** none that act on it (Accounting books bills from `PurchaseOrderApproved` since 2026-10-22)

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

**Description:** Published when a purchase order is approved. As of the
2026-08-04 multi-level approval work (see below), this only fires directly
from `Approve()` when the PO's total is **below** the finance-approval
threshold (single-step approval); for POs at/above the threshold, this event
instead fires from `ApproveFinance()` once the finance step completes — either
way, this event means "the PO is now fully `Approved`."

**Publisher:** Purchasing Service  
**Subscribers:** Accounting Service; Notification Service (`PurchasingTopic`,
added 2026-08-04 — sends a confirmation notification; see
`PurchaseOrderApprovedIntegrationEventHandler`)

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

### PurchaseOrderPendingFinanceApprovalEvent

> Added 2026-08-04 — part of the Multi-level Purchase Order Approval feature
> (Manager → Finance). See `docs/business/Purchasing/PurchasingWorkflow.md`
> and `docs/fixes/2026-08-04_ERP4Features_SeedDataAndFeatures_Summary.md` §C.1
> for full context.

**Description:** Published by `PurchaseOrder.Approve(approvedBy)` instead of
`PurchaseOrderApprovedEvent` when `TotalAmount >= FinanceApprovalThreshold`
(currently a hardcoded `5000m` on the aggregate — no per-tenant/category
override exists yet). Records the manager's approval
(`ManagerApprovedBy`/`ManagerApprovedAt`) and moves the PO to
`PurchaseOrderStatus.PendingFinanceApproval` (`= 8`), awaiting a second,
separate `ApproveFinance(approvedBy)` call
(`POST /api/v1/purchase-orders/{id}/finance-approve`, gated by the new
`purchase_order:finance_approve` permission) before the PO reaches `Approved`
and `PurchaseOrderApprovedEvent` fires.

**Publisher:** Purchasing Service
**Subscribers:** Notification Service (`PurchasingTopic`) — notifies whoever
holds `purchase_order:finance_approve` that a PO is awaiting their review

**Event Structure (actual code):**
```csharp
public class PurchaseOrderPendingFinanceApprovalDomainEvent
{
    public Guid PurchaseOrderId { get; set; }
    public string PONumber { get; set; }
    public Guid SupplierId { get; set; }
    public decimal TotalAmount { get; set; }
    public string ManagerApprovedBy { get; set; }
    public DateTime OccurredOn { get; set; }
}
```

**Routing:** Kafka (`purchasing-topic`, Outbox Pattern)
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
- ~~Accounting Service: Update inventory valuation~~ — unconfirmed; not corrected until
  now. AccountingService's real warehouse-movement consumer only verifies a fiscal period
  is open and does no actual valuation posting (deferred pending Chart-of-Accounts
  mapping — see Warehouse Service README).

> **Note (2026-07-27, Module 8 reconciliation):** reservation is now a non-destructive
> soft hold — the FEFO-allocated lot(s) for each item are recorded internally
> (`ReservedItem.LotAllocations`, a list — a line can span more than one lot) but do not
> yet reduce `InventoryLot.RemainingQuantity`. Real depletion only happens at
> `SalesOrderShippedEvent` time.

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
- ~~Purchasing Service: Trigger reorder if low stock~~ — **corrected 2026-07-27**: no such
  consumer ever existed for this event. As of this pass, PurchaseOrderService does now
  react to low stock, but via the separate `StockAlertEvent` (below), not this one.
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
    public decimal UnitCost { get; set; }   // 2026-10-24 (P1-1b); 0 = unknown
    public decimal ValueDelta { get; set; } // signed change of the stock value: receipt +, issue -, gain +, loss -
}
```

**Routing:** RabbitMQ `warehouse_exchange`, key `warehouse.StockMovementRecordedEvent`  
**Accounting:** `WarehouseRabbitMqConsumer` (queue `accounting-service.warehouse-events`) posts it; see docs/business/Accounting/AutoPosting.md. `PurchaseOrderReceivedDomainEvent` lines now carry `UnitPrice`, `CurrencyCode`, `GoodsReceiptId`.  
**Delivery:** Guaranteed

---

### StockAdjustmentApprovedEvent / StockAdjustmentRejectedEvent

> Added 2026-07-27 — part of the Module 7 (Warehouse) doc reconciliation. Previously
> `AdjustStockCommand` wrote straight to `Inventory` with no approval concept at all,
> directly violating the business doc's explicit rule that an adjustment must be
> approved before it can affect Inventory. Replaced with a `StockAdjustment` aggregate
> (Pending → Approved/Rejected) — only the Approve path actually posts the change.

**Description:** Published from the new approval-gated Stock Adjustment workflow
(`POST /api/v1/stock-adjustments/{id}/approve` or `/reject`).

**Publisher:** Warehouse Service
**Subscribers:** None confirmed today (candidate: Accounting Service, for adjustments that affect inventory valuation)

**Event Structure (actual code):**
```csharp
public class StockAdjustmentApprovedEvent
{
    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityDelta { get; set; }
    public string ApprovedBy { get; set; }
    public DateTime ApprovedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}

public class StockAdjustmentRejectedEvent
{
    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public string RejectedBy { get; set; }
    public string Reason { get; set; }
    public DateTime RejectedAt { get; set; }
    public DateTime EventCreatedAt { get; set; }
}
```

**Routing:** RabbitMQ (`warehouse_exchange`, via the Outbox retrofit — see below)
**Delivery:** Guaranteed

---

### Also real but previously undocumented in this catalog

> Added 2026-07-27 — these existed in code before this reconciliation pass but were
> never added here; listed for completeness, not new behavior.

| Event | Trigger | Consumer |
|---|---|---|
| `InventoryInsufficientEvent` | A reservation request's line(s) couldn't be fully covered | SaleOrderService's saga (compensation path) |
| `ReceiptCompletedEvent` | Goods received (`ReceiveStockCommand`) | None confirmed |
| `StockAlertEvent` | Product quantity at/below reorder point | **PurchaseOrderService** (added 2026-07-27, Module 8) — `WarehouseEventRabbitMqConsumer` creates a suggestion-only `PurchaseRequest`; was unconsumed anywhere before this pass |
| `StocktakeCompletedEvent` | Stocktake variance approved and posted | None confirmed |
| `TransferOrderCompletedEvent` | Transfer order completed | None confirmed |
| `InventoryValuationSnapshotClosed` (added 2026-07-27, Module 8) | A period-end Inventory Valuation Snapshot is closed (`POST /inventory/valuation/snapshots`) | None confirmed — same reasoning as `InventoryReservedEvent`'s Accounting note above: AccountingService's GL posting is deferred until Chart-of-Accounts mapping exists |

**Reliability note:** as of 2026-07-27, every event in this section is staged into an
Outbox table (`WarehouseEventOutboxWriter`) in the same transaction as the triggering
business write, and delivered by a background `WarehouseEventOutboxProcessor` — replacing
the previous synchronous, non-transactional direct-publish from `EventPublisher`
(mirrors the same retrofit already applied to SaleOrderService's events).

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

## HRM Service Events

### LeaveApprovedEvent / LeaveRejectedEvent

> Added to this catalog 2026-08-04. Both events existed in HRMService's domain
> layer before this session, but had never successfully reached any consumer —
> see the Outbox `Type` bug below.

**Description:** Published when a Leave Request is approved or rejected
(`docs/business/HRM/BA-Analysis/05-request-approval.md` §1.4 describes the
target design for a generic multi-level Approval Engine; the code path that
actually publishes these two events today is HRM's existing, simpler
single-step Leave Request approve/reject flow — the Approval Engine has not
been built).

**Publisher:** HRM Service (Outbox → Kafka `hrm-topic`). 2026-10-24 (P1-1d): `PaySlipGeneratedEvent` also carries `GrossSalary`, `OvertimePay`, `Deductions`, `PeriodEnd`; new `PaySlipPaidEvent(PaySlipId, EmployeeId, NetPay, PayPeriod, PaidOn, PaymentMethod)` and `PaySlipVoidedEvent(PaySlipId, EmployeeId, PayPeriod, Reason)`; keyed by pay slip id; consumed by **AccountingService** (`AccountingKafkaConsumer`, payroll posting).
**Subscribers:** Notification Service (`HrmTopic`, added 2026-08-04) — sends
the requesting employee a confirmation/rejection notification via
`LeaveApprovedIntegrationEventHandler`/`LeaveRejectedIntegrationEventHandler`

**Event Structure (actual code):**
```csharp
public class LeaveApprovedEvent
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime ApprovedAt { get; set; }
}

public class LeaveRejectedEvent
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Reason { get; set; }
    public DateTime RejectedAt { get; set; }
}
```

**Routing:** Kafka (`hrm-topic`, Outbox Pattern)
**Delivery:** Guaranteed — **but see the fix below**: prior to 2026-08-04,
HRMService's Outbox wrote `Type = domainEvent.GetType().FullName` instead of
`.AssemblyQualifiedName`. `OutboxProcessor.Type.GetType(...)` can only resolve
a bare `FullName` when the type happens to live in the *currently executing*
assembly — for a message meant to be deserialized by a different service
(NotificationService), this always returned `null` and the message silently
never dispatched. This meant **HRMService had never successfully published a
single integration event in its history**, not just these two. Fixed in
`HrmDbContext.cs`; the identical bug (same fix needed) was also found in
AccountingService/SharedMasterDataService/CRMService's `DbContext`s but only
HRM has been rebuilt/redeployed so far — see
`docs/fixes/2026-08-04_ERP4Features_SeedDataAndFeatures_Summary.md`.

**Handling:**
- Notification Service: notify the requesting employee of the outcome

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

**Last Updated:** 2026-08-04  
**Version:** 1.3 (added `PurchaseOrderPendingFinanceApprovalEvent` — multi-level
PO approval; documented `LeaveApprovedEvent`/`LeaveRejectedEvent` and their new
NotificationService consumer; noted `PurchaseOrderApprovedEvent`'s new
NotificationService subscriber)
