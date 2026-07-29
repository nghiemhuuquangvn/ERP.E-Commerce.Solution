# Database Performance & Indexing Review

> Senior BA / Data Architecture review, added 2026-07-29. Scope: all 16
> logical SQL Server databases under `src/Database/*` (61 migration files,
> 354 `CREATE INDEX` statements). Every finding below was verified by
> reading the actual migration SQL (not inferred from EF Core model
> classes) — see file:line references throughout. This document is
> **analysis and recommendations only** — no migration script in this
> pass changes any index or key. Executing the higher-risk recommendations
> (§3 in particular) needs its own dedicated, reviewed migration project.

## 1. Purpose & Scope

This review answers three questions for every table in the platform:

1. Is the **clustered index** (the physical row order — every SQL Server
   table is a B-Tree keyed on its clustered index, whether declared
   explicitly or not) built on a key that supports sequential inserts, or
   does it force random inserts into the middle of the tree?
2. Do the **nonclustered indexes** lead with columns selective enough to
   be useful, and avoid keying on columns that change on almost every
   write?
3. Are there **redundant or overlapping indexes** maintaining near-
   identical B-Trees for no additional query benefit?

The goal is not to rewrite the schema — most of the composite-index design
in this codebase is actually good practice (leading with `TenantId` or a
foreign key, trailing with a date for range scans). The goal is to name the
handful of patterns that recur across many services, plus the couple of
concrete, single-instance bugs, so they can be fixed deliberately and
consistently rather than discovered one at a time in production.

## 2. B-Tree Fundamentals (why these specific patterns matter)

A SQL Server index — clustered or nonclustered — is a B+-Tree. Leaf pages
hold the actual data (clustered) or a copy of the key + a row pointer
(nonclustered), sorted by the index key. Two properties of this structure
drive everything in this review:

- **Ordered insert vs. random insert.** If new key values always sort
  higher than everything already in the tree (e.g. an ever-increasing
  `IDENTITY` or timestamp), every insert lands on the right-hand edge of
  the tree — cheap, no reshuffling. If new key values are **uniformly
  random** across the whole key space (a random GUID), each insert lands
  at a random point in the middle of the tree. When the page at that point
  is full, SQL Server must **split** it: allocate a new page, move half
  the rows across, and update the parent pointer. Repeated over millions
  of inserts, this is the single most common cause of index
  fragmentation, wasted disk space (~50%-full pages after a split), and
  degraded range-scan performance in SQL Server systems — and it is a
  *design-time* property of the key, not something you can fix by adding
  more indexes later.
- **Leading-column selectivity.** A composite index `(A, B, C)` is only
  useful for filtering on `A` alone, `A+B`, or `A+B+C` — never `B` alone or
  `C` alone (SQL Server can't binary-search a tree sorted by `A` using a
  predicate on `B`). If `A` has very few distinct values (a boolean, a
  3-5-value status enum), the tree gives almost no help narrowing down
  rows before `B`/`C` take over — the index still has to be *maintained*
  (every write updates it) but returns little of its potential benefit.
  Worse: if that low-cardinality column is also the one that **changes**
  on every business-lifecycle transition (`Pending → Approved → Paid`),
  every transition is a delete-then-reinsert of that row's key in every
  index that includes it — i.e., page-split risk *by design*, not just at
  insert time.

Both patterns above are exactly what this review found, systemically.

## 3. Finding #1 (System-Wide) — 100% of Tables Use a Random-GUID Clustered Key

**Verified**: zero occurrences of the `CLUSTERED`, `NONCLUSTERED`, or
`NEWSEQUENTIALID()` keywords anywhere in `src/Database/**/*.sql`. Every
`PRIMARY KEY` in every table, in all 16 databases, is a `UNIQUEIDENTIFIER`.
Of the ~250 `PRIMARY KEY` declarations found, 171 carry an explicit
`DEFAULT NEWID()`; the remainder rely on the application layer supplying
the key (`Guid.NewGuid()` in EF Core), which is equally random. Because no
migration ever declares an index `NONCLUSTERED`, SQL Server's default rule
applies everywhere: **the `PRIMARY KEY` constraint is the clustered
index**, so every table's row-storage order is keyed on a uniformly random
16-byte value.

**Why this matters more here than in a typical CRUD app**: this platform
is an event-driven ERP with genuinely high-insert-rate tables —
`StockMovements`, `OutboxMessages` (11 separate per-service tables),
`SecurityAuditLogs`, `LoginAttempts`, `JournalEntryLines`,
`Notifications`, POS `Transactions`/`TransactionItems`,
`CommissionRecords`, `InventoryLots`, every `Fact*` table in Reporting.
Each of these accumulates rows continuously in production; each insert is
a random-position write into its own clustered B-Tree.

**Remediation options** (ranked by effort, **none executed in this pass**):

| Option | Effort | Trade-off |
|---|---|---|
| `NEWSEQUENTIALID()` as the column default | Low — one line per table, no app changes | SQL-Server-specific; sequential only within a single server's uptime (resets after reboot, though still far better than pure random); GUID is still 16 bytes, still 2-4x wider than a `BIGINT` key |
| Application-side sequential GUID generation (e.g. a "COMB"/UUIDv7-style generator combining a timestamp prefix with random suffix) | Low-Medium — one shared helper in `ERP.Shared.Core`, swap `Guid.NewGuid()` call sites | Portable across DB engines; still a GUID everywhere else in code (DTOs, API contracts) unaffected |
| Add a `BIGINT IDENTITY` surrogate key as the clustered index; keep the existing `UNIQUEIDENTIFIER` as a `UNIQUE NONCLUSTERED` "alternate key" for all existing FK/API usage | Medium-High — schema change per table, FK updates if any FK references the GUID directly (need to confirm none do; this repo's cross-service references are by GUID value only, never a DB-level FK, so cross-service impact should be zero, but same-database FKs need review table-by-table) | Best long-term I/O density (8 bytes vs. 16) and perfectly sequential inserts; largest migration effort, needs its own project with a full regression pass |

**Recommendation**: treat this as a dedicated project, not a drive-by fix.
Start with the highest-insert-rate tables identified above (StockMovements,
OutboxMessages, audit/log tables) using the `NEWSEQUENTIALID()` option —
it's a single-column-default change, backward compatible with all existing
application code (still a GUID), and closes most of the fragmentation risk
with the least effort. Reserve the `BIGINT IDENTITY` surrogate-key
approach for a later phase if I/O density becomes a measured problem.

## 4. Finding #2 (Recurring Pattern) — Lifecycle/Status Columns Keyed in Nonclustered Indexes

A status/state-machine column is, by definition, a column that changes
during normal business operation. Indexing it directly (especially as a
leading or co-leading key column) means every legitimate state transition
is an index-maintenance write, not just a data write. Confirmed instances:

| Service | Table.Index | Columns | Why it recurs |
|---|---|---|---|
| Accounting | `IX_Invoices_CustomerId`, `IX_Bills_SupplierId` | `..., Status` | Status: Draft→Approved→Paid/Overdue |
| Payment | `IX_Payments_Status` | `Status` alone | Status: Pending→Authorized→Captured→Failed/Refunded |
| Purchasing | `IX_PO_Status`, `IX_PurchaseRequests_Status` | `Status, OrderDate` / `Status` alone | Draft→Submitted→Approved→Received/Rejected |
| Sales | `IX_SalesOrders_Status`, `IX_SagaStates_Status`, `IX_OrderFulfillmentSagas_Status_LastUpdatedAt` | `Status, OrderDate` / `Status, LastUpdatedAt` | Order lifecycle *and* saga steps — sagas mutate `Status` on nearly every step of a distributed transaction, the highest-frequency case in this list |
| Shipping | `IX_Shipments_Status` | `Status` alone | Created→InTransit→Delivered/Returned |
| Warehouse | `IX_StockAdjustments_Status` | `Status` alone | Pending→Approved/Rejected |
| Notification | `IX_Notifications_Status` | `Status, CreatedAt` | Queued→Sending→Sent/Failed/Bounced, likely the highest-volume table in this list |
| Promotion | `IX_Promotions_Status` | `Status, StartDate, EndDate` | Draft→Active→Paused/Expired/Cancelled |
| HRM | `IX_Leave_Pending` | `Status, CreatedAt DESC` **WHERE `Status = 0`** | A *filtered* index that still keys on the column it filters on — dead weight, see below |

**The one correct example already in the codebase — generalize from it**:
`Warehouse.Reservations.IX_Reservations_Expiring` is `(HoldUntil) WHERE
Status = 0` — a **filtered** index whose key list does *not* repeat the
filter column. Every row indexed already satisfies `Status = 0` by
construction, so there's no reason to spend key-storage/maintenance on
`Status` itself; only `HoldUntil` (the actual thing being range-scanned:
"which reservations are expiring soon") needs to be a key column.

**Remediation template**: for each hot query that's currently served by a
`(Status, ...)` index, ask "does this query only ever run against ONE
value (or a small fixed set) of `Status`?" If yes (as with "find pending
approvals," "find undelivered notifications," "find active promotions
right now"), convert to a filtered index on the *other* columns only,
`WHERE Status = <that value>`. If the query genuinely needs to range over
multiple status values with roughly even row distribution, keeping
`Status` as a trailing (not leading) key column, after a genuinely
selective column like `TenantId` or a foreign key, is the better shape —
most of the composite indexes above already do this correctly (`TenantId`
or FK leads, `Status` trails); the ones flagged as **alone** or as the
**sole/leading** key are the ones that need attention.

**`IX_Leave_Pending`'s specific defect**: the filter (`WHERE Status = 0`)
already makes every indexed row have `Status = 0`, so storing `Status` as
a key column adds zero selectivity — it should be `(CreatedAt DESC) WHERE
Status = 0`, matching `IX_Reservations_Expiring`'s shape exactly.

## 5. Finding #3 — Confirmed Migration Bug: Reporting's Masked Index

`src/Database/Reporting/002_AddMissingEntityColumns.sql` adds a `DateKey`
column to both `FactSalesOrders` and `FactPayments`, then attempts:

```sql
CREATE INDEX IX_FactSalesOrders_DateKey ON FactSalesOrders(DateKey);
CREATE INDEX IX_FactPayments_DateKey ON FactPayments(DateKey);
```

guarded by `IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '...')`.
But `001_CreateReportingDB.sql` **already created an index with the exact
same name** on the older, different columns (`OrderDateKey`,
`PaymentDateKey`). Because the guard checks only the index *name*, not its
definition, it sees the name already exists and silently skips creation.

**Net effect, in every environment where migrations ran in declared
order**: the new `DateKey` column — added specifically to support a
reporting/BI query path — has **never actually been indexed**, in any
environment. The stale index on `OrderDateKey`/`PaymentDateKey` remains,
unrelated to the column it now shares a name with.

**Fix** (documented here, not executed): `002` should have used a
distinctly-named index (e.g. `IX_FactSalesOrders_DateKey_v2` or, better,
named for what it actually indexes going forward) and/or the migration
author should have run `DROP INDEX IF EXISTS ... ; CREATE INDEX ...`
instead of a name-existence guard when the intent is to change what's
indexed. See §7 for the governance rule this should become.

## 6. Finding #4 — Redundant / Overlapping Indexes

**Confirmed, unambiguous case**: `Warehouse.Inventories` carries both:
```sql
CREATE INDEX IX_Inventory_TenantId ON Inventories(TenantId);
CREATE INDEX IX_Inventory_LowStock ON Inventories(TenantId, AvailableQuantity, ReorderPoint);
```
Any query seeking on `TenantId` alone, or `TenantId` plus a range on
`AvailableQuantity`/`ReorderPoint`, is already served by
`IX_Inventory_LowStock`'s prefix. `IX_Inventory_TenantId` is a fully
redundant B-Tree, separately maintained on a table where
`AvailableQuantity` changes on every stock movement — i.e., it's paying
the maintenance cost of an extra index with zero added query capability.
**This is the single clearest "just drop it" candidate in the whole
catalog.**

**Consolidation candidates** (each table below carries 3 separate
single-purpose indexes where a `TenantId`-leading composite would likely
serve the same access patterns):
- `Shipping.Shipments`: `OrderId`, `TenantId`, `Status` as 3 standalone
  indexes.
- `POS.Transactions`: `TerminalId+Status`, `TenantId` alone, `CashShiftId`
  alone — on what is likely the single highest-insert-rate table in the
  retail domain.
- `Promotion.Coupons`: `Code+TenantId`, `PromotionId` alone, `TenantId`
  alone.
- `Product.ProductAttributeValues`: `(OwnerType, OwnerId, ProductAttributeId)`
  filtered-unique overlaps `(OwnerType, OwnerId)` plain — worth a DBA check
  on whether the plain one can be dropped in favor of the unique one.

These are lower-confidence than the Warehouse case (a real query-plan
review against actual traffic is needed before dropping any of them), but
all four are worth that review.

## 7. Finding #5 — Migration Hygiene (governance gaps, not B-Tree defects)

- `MasterData/001_CreateMasterDataDB.sql` and
  `002_ProvisionMissingTablesAndColumns.sql` redeclare an almost-identical
  index set for `Employees`/geography/translation tables. Both are
  properly `IF NOT EXISTS`-guarded (not a runtime bug), but the schema's
  real intent is now scattered across two files, making future drift
  harder to spot.
- The `Promotion` and `PromotionService` folders under `src/Database/`
  both target the same `PromotionDB` (`002_FixPromotionEntityColumns.sql`
  lives under `PromotionService/` while `001_CreatePromotionDB.sql` lives
  under `Promotion/`) — a folder-naming inconsistency, not an index
  problem, but worth fixing so future migrations land in the right place.

## 8. Prioritized Recommendations

**Tier 1 — Low risk, high value, safe to schedule immediately:**
1. Fix the Reporting masked-index bug (§5) — add the missing index under a
   correctly distinct name; this is additive (a new index), not a change
   to an existing one, so it carries no regression risk.
2. Drop `IX_Inventory_TenantId` on `Warehouse.Inventories` (§6) — confirmed
   redundant, removing it can only reduce write cost.
3. Convert `HRM.IX_Leave_Pending` to drop the redundant filtered key
   column (§4) — same reasoning, purely a maintenance-cost reduction.

**Tier 2 — Medium risk, needs a query-pattern review first:**
4. Review and likely consolidate the lone/standalone `Status` indexes
   listed in §4 into filtered indexes matching their real hot-path
   predicate (mirroring `IX_Reservations_Expiring`).
5. Review the 4 consolidation candidates in §6 against actual query plans
   before merging/dropping any of them.

**Tier 3 — High risk, dedicated project, out of scope for this review to execute:**
6. The system-wide clustered-key strategy (§3) — start with
   `NEWSEQUENTIALID()` on the highest-insert-rate tables identified there,
   treat the `BIGINT IDENTITY` surrogate-key approach as a distinct,
   later-phase project if I/O density becomes a measured bottleneck.

## 9. Migration Governance — Preventing Recurrence

The Reporting bug (§5) happened because a name-existence guard was used
where a definition-change was intended. Adopt as a standing rule for this
repo's migration scripts:

- **`IF NOT EXISTS` on index name is only safe when the index's definition
  never changes.** If a later migration needs to change what an existing
  index covers (add/remove/reorder columns, change the filter), it must
  `DROP INDEX IF EXISTS <name> ON <table>;` before `CREATE INDEX <name>
  ...` — the pattern `Product/010_AddBrandsUomConversionsAndPricingUpdates.
  sql` already uses correctly for `IX_PriceListItems_Key`. Follow that
  file's pattern, not Reporting's.
- **Never reuse an index name across migrations on the same table unless
  intentionally redefining it** — a new column needs a new index name.
- Add "does this PR add/change an index, and if so does it follow the
  DROP+CREATE pattern above" as an explicit line item in code review for
  any `src/Database/**/*.sql` change.

## 10. Full Per-Database Index Catalog (Appendix)

The complete table-by-table index inventory (table, index name, key
columns in order, unique/composite flags) for all 16 databases —
Accounting, CRM, HRM, Identity, MasterData, Notification, POS, Payment,
Product, Promotion, Purchasing, Reporting, Sales, Shipping, Warehouse —
that this review's findings above were derived from is maintained as a
living reference in `docs/architecture/database-map.md` (schema/table
design) and is reproduced in full in the companion
`05-Database-Performance-Indexing-Review.docx` deliverable's appendix
section, generated from this file. Cross-reference the per-service
`src/Database/{Service}/*.sql` files directly for the authoritative,
always-current source.
