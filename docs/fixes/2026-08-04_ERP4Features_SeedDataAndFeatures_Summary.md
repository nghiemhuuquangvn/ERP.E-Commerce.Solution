# ERP 4.0 Features + Full Data Seeding — 2026-08-04

## Overview

This session took the platform from "16 databases, mostly empty, 8 services with
zero seed data" to a fully populated system (1000+ rows on every headline table,
deterministic cross-service GUIDs so referential consistency holds without any
cross-DB query), then built and shipped three ERP 4.0 feature areas end-to-end:
**Real-time Dashboard/KPI**, **Multi-level Workflow Automation**, and
**Rule-based AI/Analytics**. Every deliverable below was verified live against
the running K8s cluster (`erp` namespace) — real API calls, real DB rows, real
Kafka/RabbitMQ notification records — not just "compiles."

As with prior sessions' fix logs, a strong recurring pattern surfaced: large
parts of this codebase were **written but never exercised end-to-end**. Every
phase below uncovered new bugs the moment a previously-empty table finally got
real data, or a previously-uncalled endpoint got actually invoked. None of
these were regressions from this session's own changes — they were dormant
defects this session's data/testing finally reached.

---

## Part A — Full data seeding (all 16 services)

**Scope decided with the user**: seed all 16 services at once, 1000+ rows on
headline tables (Products, Users/Customers, SalesOrders, Employees, etc.), by
extending the existing idempotent SQL migration scripts (`IF NOT EXISTS`/
`MERGE` pattern) rather than building a separate C# seeder.

### A.1 — Cross-service GUID convention

New file: `src/Database/SEED_DATA_CONVENTIONS.md`. Defines a deterministic
formula `{PrefixHigh}-{PrefixLow}-0000-0000-{N padded 12 hex}` plus a full
prefix registry (one prefix block per entity type per owning service). This
lets, e.g., Sales' seed script compute the exact GUID of "Product #137" without
ever querying ProductDB — satisfying the CLAUDE.md rule against cross-database
access while still keeping every service's seed data referentially consistent
with every other service's.

### A.2 — 14 new seed scripts

One new `NNN_SeedDemoData.sql` per service (MasterData, Identity, Product, CRM,
Sales, Warehouse, Purchasing, Payment, Shipping, Promotion, POS, Accounting,
HRM, Notification), generated with set-based T-SQL (numbers CTE × name arrays,
following the existing `DimDate`-generation precedent) rather than row-by-row
inserts, to hit 1000+ rows per table cheaply.

**Recurring T-SQL bug classes hit and fixed across these 14 scripts** (see also
`memory/project_seed_data_2026_08.md`):

1. `QUOTED_IDENTIFIER` OFF by default under `sqlcmd` → every new script now
   starts with `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;`.
2. `PRINT` cannot contain a subquery → assign to a local variable first.
3. A CTE using `NEWID()` gets re-evaluated on every reference → materialized
   into a `#temp` table first (hit in Promotion's Coupons script — was
   producing duplicate-key errors).
4. `DATEADD(HOUR, ...)` on a `DATE`-typed column → cast to `DATETIME2` first
   (POS, Accounting).
5. A bare `END` immediately followed by `WITH` needs an explicit `;` — hit
   twice in the Identity script.

### A.3 — Verification

Ran the full migration set against the live cluster's SQL Server, spot-checked
row counts per table via `sqlcmd`, and cross-checked a sample of
`SalesOrder.ProductId` values against `Product.Products.Id` computed by the
same GUID formula to confirm referential consistency held with zero cross-DB
queries. Confirmed Admin's Products/Sales Orders/CRM Contacts/Warehouse
Inventory pages render 1000+ real rows with working pagination/search.

---

## Part B — Real-time Dashboard & KPI (ReportingService)

**Decision with the user**: dashboard data must come from ReportingService's
proper Fact/Dimension star-schema architecture, not ad-hoc queries against
business-service databases.

### B.1 — What was built

- `src/Database/Reporting/003_SeedFactsFromSeedData.sql` — one-time ETL that
  backfills `FactSalesOrder`/`FactPayment`/`FactInventoryMovement`/
  `FactPOSTransaction`/`FactHRPayslip` from the seed data created in Part A
  (documented, explicit exception to the "no cross-DB query" rule, since ETL
  scripts are read-only and run once, not part of any service's runtime path).
- `DimProducts.StockQuantity` (`005_AddDimProductStockQuantity.sql`) — new
  column, backfilled from `WarehouseDB.Inventories.AvailableQuantity`, so the
  Inventory Report can read current stock directly from the warehouse instead
  of trying to reconstruct it from a sum of signed movement events.
- `ReportingRepository.GetTopProductsAsync` — new method backing the
  dashboard's "Top Products" widget.
- Admin `ReportsPage.tsx`/`DashboardPage.tsx` — added Top Products table,
  Inventory summary section, fixed field names to match the real backend DTO
  shape (`revenueByDay`, not the previously-assumed `byDate`).

### B.2 — Bugs found and fixed

- **`FactPOSTransactions.CashierId`** was `NVARCHAR(100)` in the live DB but
  `Guid` in the C# entity — every POS fact insert with a real cashier ID would
  have thrown a cast error. `ALTER TABLE ... ALTER COLUMN CashierId
  UNIQUEIDENTIFIER NOT NULL` (`004_FixFactPOSTransactionsCashierIdType.sql`).
- **`GetInventoryReportAsync` double-negated signed quantities** — the
  repository was flipping the sign of `Issue`-type movements *twice* (once via
  the movement's own stored sign convention, once again in application code),
  so the computed "current stock" was silently wrong for any product with a
  mix of receipts and issues. Removed the redundant re-flip; inventory now
  reads from `DimProducts.StockQuantity` (see B.1) instead of re-deriving it
  from movement history at query time.
- **Frontend `api/reports.api.ts` never matched the real backend contract** —
  it was written against a guessed shape (`byDate`, no `topProducts`, no
  `newCustomers`) that never existed on the wire. Rewritten to match
  `ReportingRepository`'s actual DTOs.

### B.3 — Verification

Rebuilt and redeployed `reporting-service` + `admin`. Confirmed live via API:
`GET /rpt/api/v1/reports/sales` returns real revenue/order-count/AOV numbers
computed from the seeded `FactSalesOrder` rows; `GET /rpt/api/v1/reports/
inventory` returns correct per-product stock levels matching WarehouseDB;
Admin's Reports and Dashboard pages render both without errors.

---

## Part C — Multi-level Workflow Automation

**Decision with the user**: build both the backend state machine/events and
the Admin UI approve/reject affordances, not just the API.

### C.1 — Purchase Order: Manager → Finance approval

**Business rule added**: a PO's `Approve()` now checks `TotalAmount` against a
`FinanceApprovalThreshold` (currently `5000m`, hardcoded on the aggregate — no
per-tenant/category override exists yet, flagged as a follow-up, not built).

- **`TotalAmount < threshold`**: single-step approval, unchanged from before —
  `Approve()` moves `Submitted → Approved` directly.
- **`TotalAmount >= threshold`**: `Approve()` now records the manager's
  approval (`ManagerApprovedBy/At`) and moves the PO to a new status,
  `PendingFinanceApproval` (`PurchaseOrderStatus = 8`), publishing
  `PurchaseOrderPendingFinanceApprovalDomainEvent`. A second, separate action —
  `ApproveFinance(approvedBy)`, exposed as `POST /api/v1/purchase-orders/{id}/
  finance-approve`, gated by a new `purchase_order:finance_approve` permission
  — must then be called to actually move the PO to `Approved`
  (`FinanceApprovedBy/At` recorded).
- **`Reject()`** was widened to accept a PO from either `Submitted` or
  `PendingFinanceApproval`, so a finance reviewer can reject a large PO the
  same way a manager can reject a small one — previously only `Submitted` was
  a valid source state.

New files: `PurchaseOrderPendingFinanceApprovalDomainEvent.cs`,
`ApprovePurchaseOrderFinanceCommand[Handler].cs`. Migration:
`Purchasing/008_AddMultiLevelApproval.sql` (adds the 4 new columns), Identity:
`016_SeedFinanceApprovePermission.sql`.

**Admin UI**: `PurchaseOrderListPage.tsx` gained a "Finance Approve" button
(visible only for `PendingFinanceApproval` rows, gated on the new permission)
and a Reject button + `RejectPurchaseOrderModal` (react-hook-form, mirrors
HRM's existing `RejectLeaveModal` pattern). `StatusBadge.tsx` gained colors for
`PENDING_FINANCE_APPROVAL`/`REJECTED`.

**Bugs found and fixed along the way**:
- **`MappingProfile` never mapped `PurchaseOrderItem.UnitPrice` at all** —
  every PO list/detail response silently dropped unit price, and the mapping
  gap had existed since before this session (not something this change
  introduced). Added the missing `.ForMember`.
- **Status was serialized PascalCase (`"Approved"`) but the frontend has
  always compared against `UPPER_SNAKE` (`"APPROVED"`)** — meaning the
  Approve/Finance-Approve buttons in Admin had **never once worked correctly**
  historically, regardless of this session's changes; the status comparison
  always failed. Added a `ToUpperSnakeCase()` conversion in the mapper for
  both `PurchaseOrder.Status` and `PurchaseOrderItem.Status`.

### C.2 — Leave Request approval → live notifications

The Leave Request approve/reject flow itself already existed in HRMService
(unchanged this session). What didn't exist: any working notification when a
leave request was approved or rejected. Root-caused and fixed 3 independent
bugs that had, between them, made this and the PO finance-approval flow's
notifications completely non-functional:

1. **HRMService's Outbox wrote `Type = domainEvent.GetType().FullName`**
   (namespace + class name only) instead of `.AssemblyQualifiedName` (which
   also includes the assembly name). `OutboxProcessor.Type.GetType(...)` at
   deserialize time can only resolve a type by `FullName` if it happens to
   live in the *currently executing* assembly — for a message consumed by a
   different service (NotificationService), this always returned `null` and
   the message silently failed to dispatch. **This meant HRMService had never
   successfully published a single integration event in its history**, not
   just for this feature. Fixed in `HrmDbContext.cs`; same fix applied to
   AccountingService/SharedMasterDataService/CRMService's `DbContext`s (source
   only — not rebuilt/redeployed this session since nothing exercised those
   paths yet).
2. **Newtonsoft's `Value<Guid?>()` throws `InvalidCastException`** when the
   underlying JSON value is a plain string (which every GUID field in every
   event is) rather than already boxed as a `Guid` — this affected **every**
   Kafka consumer in NotificationService, old and new alike, not just the two
   new event types added this session. Replaced every call site with a new
   `GuidOrEmpty(JObject, string)` helper built on `Guid.TryParse`.
3. **NotificationService's Kafka consumer was only ever subscribed to
   `identity-topic`** — added `PurchasingTopic`/`HrmTopic` consts and
   subscribed both, plus registered 4 new dispatch branches
   (`PurchaseOrderPendingFinanceApprovalDomainEvent`,
   `PurchaseOrderApprovedDomainEvent`, `LeaveApprovedEvent`,
   `LeaveRejectedEvent`) and their corresponding `INotification`/handler pairs
   in `IntegrationEventHandlers.cs`.

Also hit and fixed while wiring this up: `NotificationDbContext` was missing a
`.HasColumnName("LastError")` for `ErrorMessage` (schema drift, same class of
bug as prior sessions), plus two DB columns (`RecipientDeviceToken`,
`NotificationLogs.TenantId`) that the entity needed but no migration had ever
added — `005_SeedWorkflowTemplates.sql`/`006_FixNotificationEntityColumns.sql`.

**Operational note, not a code bug**: after redeploying, the new topics
weren't picked up immediately — Kafka consumer group metadata is cached
client-side, and a consumer that started before a topic existed doesn't always
notice a newly-produced-to topic promptly. `kubectl rollout restart
deployment/notification-service` forced a fresh subscription and the messages
started flowing. Worth remembering next time a "wired but the consumer swears
it never got the message" report comes in.

### C.3 — Verification

Live-tested both paths end-to-end: submitted a PO above the $5000 threshold →
confirmed it landed in `PendingFinanceApproval` → called finance-approve →
confirmed it reached `Approved`, and confirmed a corresponding
`Notifications` row (and `NotificationLogs` entries) was written for each
transition. Same for a Leave Request approve/reject cycle in HRM — confirmed
real notification rows land in `NotificationDB` for both outcomes. Rebuilt and
redeployed: `purchaseorder-service`, `notification-service` (twice — see
above), `hrm-service`, `admin`.

---

## Part D — Rule-based AI/Analytics

No ML infrastructure was introduced — both features are deterministic,
explainable rules over already-seeded transactional data, consistent with the
plan's scoping decision ("dự báo nhu cầu, insight tự động" via moving-average/
threshold rules, not a training pipeline).

### D.1 — Reorder-point → automatic Purchase Request (verification + bug fixes)

This pipeline (`StockAlertEvent` → `WarehouseEventRabbitMqConsumer` in
PurchaseOrderService → suggestion-only `PurchaseRequest`) already existed in
code before this session (see `docs/architecture/event-catalog.md`'s
`StockAlertEvent` entry, added 2026-07-27) but **had never actually fired
successfully** — it was blocked by 2 bugs discovered while proving it out live
this session (plus the `MappingProfile.UnitPrice` bug from Part C.1, which
independently blocked the PO list API the Purchase Request UI depends on):

1. **`StockMovements.SourceOrderId` had no column mapping** — the live DB
   column is actually named `ReferenceId` (a naming drift from before this
   session), so every write to `SourceOrderId` failed. Added
   `entity.Property(e => e.SourceOrderId).HasColumnName("ReferenceId")` to
   `WarehouseDbContext`.
2. **`StockMovement` (and `Inventory`, and effectively the whole
   WarehouseService Application layer) had no `TenantId` property at all**,
   despite the DB column being `NOT NULL` — every stock movement write was
   failing that constraint. Added `TenantId` to the `StockMovement` entity and
   its constructor, and updated the 6 call sites
   (`RecordStockMovementCommandHandler`, `ReservationFulfillmentService`,
   `ReceiveStockCommandHandler`, `AdjustStockCommandHandler`,
   `CompleteStocktakeCommand`, `CompleteTransferOrderCommand`) to pass the
   fixed system tenant GUID
   (`00000000-0000-0000-0000-000000000001`). **This is explicitly a pragmatic
   unblock, not a real fix** — WarehouseService has no actual multi-tenant
   scoping for stock movements yet; that gap is unchanged and out of scope
   here.

**Verified live**: sold 20 units of a seeded product (available quantity
61 → 41, below its seeded reorder point of 47) → confirmed a `PurchaseRequest`
row was auto-created ("Slim Gadget #0037, Current=41, Suggested=87"), matching
the existing design in `WarehouseEventRabbitMqConsumer`.

### D.2 — Sales order anomaly detection (new)

**Rule**: an order is flagged anomalous if its total is more than 3× the
customer's average order total, and the customer has at least 3 prior orders
to establish a meaningful baseline (fewer than 3 → never flagged, to avoid
false positives from a brand-new customer's first large order).

```csharp
public static class OrderAnomalyDetector
{
    private const int MinHistoryCount = 3;
    private const decimal ThresholdMultiplier = 3.0m;

    public static (bool IsAnomalous, string? Reason) Evaluate(
        Guid orderId, decimal orderTotal, IReadOnlyCollection<SalesOrder> customerOrders)
    {
        var history = customerOrders.Where(o => o.Id != orderId).ToList();
        if (history.Count < MinHistoryCount) return (false, null);
        var average = history.Average(o => o.TotalAmount);
        if (average <= 0 || orderTotal <= average * ThresholdMultiplier) return (false, null);
        var multiple = average > 0 ? orderTotal / average : 0;
        return (true, $"Order total is {multiple:0.#}x this customer's average order (${average:N2} over {history.Count} prior orders)");
    }
}
```

Wired into `GetOrderQueryHandler` (order detail) and `ListOrdersQueryHandler`
(order list — computed once per distinct customer in the page via a
`Dictionary<Guid, List<SalesOrder>>`, not once per row, to avoid an N+1 query
pattern against `GetByCustomerIdAsync`). Exposed as `isAnomalous`/
`anomalyReason` on both `SalesOrderDto` and `SalesOrderSummaryDto`.

**Admin UI**: a red "⚠ Unusual order" badge next to the order number on both
the order list (`OrderListPage.tsx`) and order detail (`OrderDetailPage.tsx`)
pages, with the full reason string as a tooltip.

**Verified live**: `GET /api/v1/orders?...` returned a real seeded order with
`totalAmount: 4199.84` correctly flagged with `anomalyReason: "Order total is
3.2x this customer's average order ($1,311.82 over 3 prior orders)"`.

### D.3 — Not built (explicitly out of scope this round)

- No demand-forecasting model beyond the existing static reorder-point
  threshold — a true moving-average/seasonal forecast was discussed in the
  plan as a stretch goal but not implemented; the reorder-point mechanism
  verified in D.1 already existed in code and was only unblocked, not
  extended.
- No configurable anomaly threshold (3× is a compile-time constant, not a
  per-tenant/category setting) and no anomaly detection anywhere outside Sales
  Orders (e.g. Purchase Orders, POS transactions).

---

## Services rebuilt and redeployed this session

`reporting-service`, `purchaseorder-service`, `notification-service` (x2),
`hrm-service`, `warehouse-service`, `salesorder-service`, `admin`.

## Cross-cutting follow-ups flagged, not fixed (out of scope)

- WarehouseService has no real multi-tenant scoping for stock movements (see
  D.1) — every write silently uses the fixed system tenant.
- `AccountingDbContext`/`MasterDataDbContext`/`CrmDbContext`'s Outbox `Type`
  column fix (assembly-qualified name) is applied in source but **not yet
  rebuilt/redeployed** — those 3 services' Outbox-published events remain
  silently undeliverable to any cross-assembly consumer until they are.
- PO finance-approval threshold ($5000) is a hardcoded constant, not
  per-tenant/category configurable.
- Anomaly detection threshold (3×) is likewise hardcoded, and Sales Orders is
  the only domain it covers.
