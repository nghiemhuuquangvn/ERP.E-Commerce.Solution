# Seed Data Conventions (cross-service GUID registry)

## Why this file exists

Every service owns its own database — no cross-database foreign keys are
allowed (see root `CLAUDE.md`, "Database Ownership" / "Integration Rules").
But many tables carry a denormalized GUID that conceptually points at a row
owned by a *different* service (e.g. `SalesDB.SalesOrders.ProductId` should
"be" a real `ProductDB.Products.Id`). Today those columns are seeded with
random `NEWID()` values or left empty, so cross-service demo data doesn't line
up and the UI shows orders referencing products that don't exist.

This file fixes that by giving every cross-referenced entity a **deterministic
GUID formula**: `entity-type prefix` + `sequence number N`. Any service's seed
script can compute a valid-looking reference to another service's row purely
by knowing this formula — no cross-DB query needed, no shared transaction
needed, and scripts stay independently runnable/idempotent in any order.

**Rule: every seed script (in every service) MUST use the formulas in this
file — never `NEWID()` — for any column that is the "N-th row" of a
registered entity below, or that references one.** Local-only tables (never
referenced by another service, e.g. `SalesOrderItems`, `NotificationHistory`)
may keep `NEWID()`/`DEFAULT` for their own `Id`.

## GUID formula

```
{PrefixHigh}-{PrefixLow}-0000-0000-{N padded to 12 hex digits}
```

`N` is a 1-based sequential index (1, 2, 3, ...) specific to that entity type.
In T-SQL:

```sql
CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), @N), 12) AS UNIQUEIDENTIFIER)
```

Existing fixed values already in the codebase (do not change):
- System Tenant: `00000000-0000-0000-0000-000000000001` (`Identity/001_CreateAuthDB.sql`) — reuse this exact GUID as `TenantId` in every new seed row, every service.
- SuperAdmin user: `20000000-0000-0000-0000-000000000001`
- System roles: `10000000-0000-0000-0000-00000000000{1..4}`
- System permissions: `30000000-0000-0000-0000-00000000000{1..5}`

## Registry (Prefix → entity → owning service)

| PrefixHigh-PrefixLow | Entity | Owning service / DB | Target row count | Notes |
|---|---|---|---|---|
| `00000000-0000` | Tenant | Identity / AuthDB | 1 (fixed, existing) | Always `...0001`. Do not seed more tenants unless asked. |
| `10000000-0000` | Role | Identity / AuthDB | 4 (existing) | Unchanged. |
| `20000000-0001` | Staff User | Identity / AuthDB | ~50 | Admin/Manager/Sales/Warehouse/Accountant staff logins. `UserTypeCode` per role. |
| `20000000-0002` | Customer User | Identity / AuthDB | 1000+ | `UserTypeCode = 'Customer'`. **N here is reused as the same N for CRM Contact and MasterData Employee-adjacent lookups are NOT implied** — Customer User N and CRM Contact N are the same person (see below). |
| `40000000-0001` | Country | MasterData | ~10 | VN + a handful of trading partners. |
| `40000000-0002` | Province | MasterData | ~30 | Children of Country N=1 (Vietnam) mostly. |
| `40000000-0003` | District | MasterData | ~150 | Children of Provinces. |
| `40000000-0004` | Ward | MasterData | ~600 | Children of Districts. |
| `40000000-0005` | UnitOfMeasure | MasterData | ~20 | pcs, box, kg, g, L, mL, m, m2, ... |
| `40000000-0006` | Currency | MasterData | ~5 | VND, USD, EUR, JPY, CNY. |
| `40000000-0007` | Department | MasterData | ~15 | Sales, Warehouse, Accounting, HR, IT, ... |
| `40000000-0008` | Position | MasterData | ~40 | 2-4 per department. |
| `40000000-0009` | Employee | MasterData | 1000+ | `DepartmentId`/`PositionId` reference the two above by formula. |
| `50000000-0001` | Category | Product | ~30 | Can self-nest via `ParentCategoryId` using the same formula. |
| `50000000-0002` | Brand | Product | ~40 | |
| `50000000-0003` | Product | Product | 1000+ | `CategoryId`/`BrandId`/`BaseUomId` (→ `40000000-0005`) by formula. |
| `50000000-0004` | ProductVariant | Product | 1000+ | 1 variant per product is fine (or a few for a subset). `ProductId` by formula. |
| `50000000-0005` | PriceList | Product | ~5 | Retail/Wholesale/Ecommerce per channel. |
| `60000000-0001` | Contact | CRM | 1000+ | **Reuse the same N as Customer User**: `Contacts.UserId` for Contact N = Customer User GUID for N (`20000000-0002-0000-0000-{N}`). This is what makes a "customer" the same person across Identity and CRM. |
| `60000000-0002` | Lead | CRM | ~300 | |
| `60000000-0003` | Opportunity | CRM | ~200 | |
| `60000000-0004` | Case | CRM | ~150 | `OrderId` may reference Sales (`70000000-0001`) by formula for a subset. |
| `70000000-0001` | SalesOrder | Sales | 1000+ | `CustomerId` = Customer User GUID (`20000000-0002-...`) picked pseudo-randomly per order; `SalesRepId`/`DepartmentId` reference MasterData Employee/Department. |
| — | SalesOrderItem | Sales | 2000+ | Local `Id` via `NEWID()`; `ProductId` references Product (`50000000-0003`) by formula. Not itself registered (never cross-referenced). |
| `80000000-0001` | Warehouse | Warehouse | ~5 | |
| `80000000-0002` | WarehouseLocation | Warehouse | ~50 | |
| — | Inventory | Warehouse | 1000+ | PK **is** `ProductId` (per schema) — so it directly reuses Product's GUID (`50000000-0003`), one row per seeded product. Not a separate prefix. |
| `90000000-0001` | Supplier | Purchasing | ~50 | |
| `90000000-0002` | PurchaseOrder | Purchasing | 500+ | References Supplier + Product by formula. |
| `A0000000-0001` | PaymentTransaction | Payment | 1000+ | References SalesOrder (`70000000-0001`) by formula. |
| `B0000000-0001` | Shipment | Shipping | 1000+ | References SalesOrder by formula. |
| `C0000000-0001` | Promotion | Promotion | ~100 | |
| `C0000000-0002` | Coupon | Promotion | ~200 | References Promotion by formula. |
| `D0000000-0001` | POS Transaction | POS | 500+ | References Product/Employee by formula. |
| `E0000000-0001` | JournalEntry | Accounting | 1000+ | |
| `E0000000-0002` | Invoice | Accounting | 1000+ | References SalesOrder by formula. |
| `E0000000-0003` | Bill | Accounting | 500+ | References PurchaseOrder by formula. |
| — | HRM AttendanceLog/LeaveRequest/PaySlip | HRM | 1000+ combined | `EmployeeId` references MasterData Employee (`40000000-0009`) by formula — HRM has **no local Employee table** (removed per CLAUDE.md note), only an `EmployeeSnapshot` projection which itself must be seeded to mirror MasterData Employees 1:1 by formula. |
| — | NotificationHistory | Notification | ~1000 | `UserId` references Identity User by formula. Not itself registered. |

## Execution notes for whoever writes each seed script

1. **Read the actual current schema file(s) for your service first** (table/column
   names above are a summary from an earlier scan — verify exact names, especially
   for tables not fully detailed here, before writing INSERTs).
2. Write one new file `NNN_SeedDemoData.sql` per service (`NNN` = next unused
   number in that service's folder), in the exact idempotent style already used
   (`IF NOT EXISTS (SELECT 1 FROM sys.tables ...)` for schema, and for data use
   `IF NOT EXISTS (SELECT 1 FROM Table WHERE Id = ...)` per row, or an
   `IF (SELECT COUNT(*) FROM Table) < TargetCount` guard before a bulk-insert
   loop — either is fine, but the script must be safely re-runnable).
3. Generate rows **set-based** (a numbers CTE / recursive CTE cross-joined with
   small arrays of first names, last names, city names, product-noun/adjective
   words, etc.) — mirror the technique in
   `Reporting/001_CreateReportingDB.sql`'s `DimDate` `WHILE` loop, or a
   `master..spt_values`/recursive-CTE numbers table for larger batches. Do not
   write 1000+ literal `INSERT ... VALUES` statements by hand.
4. Always set `TenantId = '00000000-0000-0000-0000-000000000001'` where the
   table has that column.
5. When a column references another service's entity, compute the GUID with
   the formula above — pick `N` pseudo-randomly in range `[1, TargetCount]` of
   that entity (e.g. `1 + ABS(CHECKSUM(NEWID())) % 1000` for a random product
   per order line), not sequentially, so the data looks realistic (repeat
   customers, popular products, etc.), except for 1:1 cases like
   Inventory-per-Product or EmployeeSnapshot-per-Employee where you iterate
   every N.
6. Dates: spread creation/order/movement dates realistically over the last
   ~12 months, not all on the same day, so time-series charts (Phase 4 KPI
   dashboard) have something to show.
7. Money amounts: use plausible ranges per domain (e.g. products $5-$500,
   orders summing their line items, not flat/round numbers everywhere).
8. After writing a service's script, do a mental (or local, if SQL Server is
   reachable) dry run for T-SQL syntax errors — these scripts run unattended
   inside `docker-compose`'s `db-migrate` step; a syntax error blocks every
   dependent service's startup.
