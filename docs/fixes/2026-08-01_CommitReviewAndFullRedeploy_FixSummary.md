# Commit Review + Full K8s Redeploy Fix Summary — 2026-08-01

## Overview

The running K8s cluster (`erp` namespace, `erp.local`) hadn't been redeployed since
~40 commits landed (`4fe19ac`..`b8b0acf` "refactor permission"), including a major,
self-documented, explicitly-untested architectural change to the permission system
(`docs/business/Identity/module-02-identity-security.md` §12). This session reviewed
that accumulated history, fixed everything found broken, then rebuilt and redeployed
all 19 backend services + Gateway + Admin + Storefront.

**Final state: full end-to-end verification passing** — Identity (tenants/roles/
permissions/users/settings/license), core direct routes (orders/inventory/payments/
products), all 10 double-prefixed module routes (hrm/acc/mst/wh/po/so/pay/shp/ntf),
and both frontends all return 200.

---

## A — Findings from commit review

1. **15 migration files existed on disk but were never registered in
   `src/Database/RunAllScripts.ps1`** (the same recurring gap as prior sessions):
   `MasterData/004,005`, `Sales/006-009`, `Purchasing/005,006`, `Warehouse/004,005`,
   `Product/010`, `CRM/002`, `Reporting/002`, `HRM/003` + `HRM/003b`, plus
   `PromotionService/002` (a second, inconsistently-named folder for `PromotionDB`).
   `013_SeedRoleDefinitionsIntoRolePermissions.sql` was already registered — it just
   needed to actually run.
2. **`docker-compose.yml` was rewritten** to run SQL Server as an in-cluster
   container + a new `db-migrate` one-shot service that globs every `.sql` file
   directly, bypassing `RunAllScripts.ps1`. Not adopted for K8s (out of scope — would
   mean standing up a second DB instance); K8s still points at the same
   locally-reachable SQL Server this cluster has always used.
3. **`c9bca31` "Refactor the contructure Projects by Claude Code"** — verified no
   project/`.csproj` moves, just doc/skill reorganization (including deleting the
   previous `docs/fixes/` directory entirely — recreated here).
4. No `TODO`/`FIXME`/`NotImplementedException` introduced in the reviewed range.

## B — Fixes applied

### Fix 1 — Registered the 15 missing migrations
**File**: `src/Database/RunAllScripts.ps1`

### Fix 2 — Ran migration 013 (permission system DB seed)
Seeded `AuthDB.Permissions` (55 → 248 rows) and `RolePermissions` per `RoleCode`
(SuperAdmin=248, Admin=150, Viewer=39; Manager/Purchaser have zero rows because no
tenant has created a role with those exact codes yet — expected, not a bug). Needed
the usual `SET QUOTED_IDENTIFIER ON;` patch first.
**File**: `src/Database/Identity/013_SeedRoleDefinitionsIntoRolePermissions.sql`

### Fix 3 — `Warehouse/002` migration had never fully applied
Live `WarehouseDB` was missing `Stocktakes`/`StocktakeLines` even though migration
002 (already in `RunAllScripts.ps1`) creates them — an earlier partial run had failed
on an ungated `CREATE INDEX` before reaching those tables. Re-ran 002 (idempotent),
tables now present.

### Fix 4 — `Warehouse/005` migration DROP COLUMN failed on every run
**Root cause**: assumed a fixed FK constraint name
(`FK_Reservation_ReservedItems_InventoryLots_LotId`) that didn't match the real,
EF-auto-generated name (`FK__Reservati__LotId__xxxxxxxx`) — the `DROP CONSTRAINT IF
EXISTS` silently no-op'd, so the later `DROP COLUMN LotId` always failed with a
dependent-object error.
**Fix**: look up the FK dynamically by column via `sys.foreign_keys`/
`sys.foreign_key_columns` instead of assuming a name.
**File**: `src/Database/Warehouse/005_AddModule8InventoryReconciliation.sql`

### Fix 5 — Ocelot mirror routes (Fix 38 from a prior session) had been silently
reverted by the same commits that added `/v1/` to every route
**Root cause**: `ocelot.json` was regenerated to add `/v1/` segments everywhere, and
in the process lost the double-`/api/`-prefix mirror routes (`/api/{prefix}/api/v1/
{everything}`) that Admin's API clients actually hit (axios `baseURL: '/api'`
concatenates with their already-root-relative `/hrm/api/v1/...` paths). Without
these, every hrm/crm/acc/mst/po/so/wh/prd/pay/shp/pos/prm/ntf/rpt/id/logs page in
Admin would 404 again.
**Fix**: re-added all 17 double-prefix mirror routes, updated for the new `/v1/`
segment.
**File**: `src/Gateway/Ocelot.API/ocelot.json`

### Fix 6 — Identity's own permission migration (Phase 2) never wired up
`IUserContextService` — every migrated endpoint 401'd/500'd regardless of the
caller's actual permissions
**Root cause**: the "refactor permission" work moved IdentityService's 21 controllers
from a DB-checked `[Authorize(Policy="Permission")]` mechanism to the shared
`[AuthorizePermission(...)]` filter every other service uses — but never added
IdentityService's own `builder.Services.AddScoped<IUserContextService,
JwtClaimsUserContextService>()` (all 14 other services register this in their own
`Program.cs`; Identity previously didn't need it and was missed). Symptom in two
stages: (1) `AuthorizePermissionAttribute` resolves `IUserContextService` as `null` →
returns a bare `401` with **no `WWW-Authenticate` header** (the tell that it's not a
real JWT-validation failure) for literally every `[AuthorizePermission]`-gated
endpoint — tenants/roles/permissions/users/settings/license all affected, while
`/auth/login` (no permission check) and every other service (which already registers
this) kept working with the exact same token; (2) after registering
`IUserContextService`, a second gap surfaced — its constructor needs
`IHttpContextAccessor`, also never registered in Identity, causing a `500` instead.
**Fix**: added both `builder.Services.AddHttpContextAccessor();` and
`builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();` to
Identity's `Program.cs`, matching every other service's pattern.
**File**: `src/services/IdentityService/src/ERP.Identity.Api/Program.cs`
**This was the single highest-impact bug found this session** — it made every
Identity-owned Admin page (Tenants/Users/Roles/Permissions/all Settings categories/
License) completely unusable after the permission-system refactor, on top of the
(correctly mitigated) risk that migration 013 itself might not have seeded
correctly. Both risks are now resolved and verified live.

### Fix 7 — JWT permission claim now exceeds nginx's default header buffer size
**Root cause**: migration 013 + the Phase-1 permission-catalog consolidation mean
SuperAdmin's JWT `permission` claim is now 248 codes — the `Authorization` header
alone is ~9.9KB, well past nginx's 8KB `large_client_header_buffers` default. Every
authenticated request failed with a generic `400 Request Header Or Cookie Too Large`
**before any application code ran**, at two independent nginx layers: the K8s
ingress-nginx controller, and Admin/Storefront's own in-container nginx
(`nginx.docker.conf`).
**Fix**: increased `large-client-header-buffers`/`client-header-buffer-size`/
`proxy-buffer-size` on the `ingress-nginx-controller` ConfigMap (cluster-level, not
repo-tracked — Docker Desktop's built-in ingress-nginx addon), and added
`large_client_header_buffers 4 32k; proxy_buffer_size 32k; proxy_buffers 4 32k;` to
both `nginx.docker.conf` files.
**Files**: `src/frontend/admin/nginx.docker.conf`,
`src/frontend/storefront/nginx.docker.conf`
**Not repo-tracked**: the ingress-nginx-controller ConfigMap patch — note here in
case this cluster is ever recreated from scratch, the same patch will be needed
again: `kubectl patch configmap ingress-nginx-controller -n ingress-nginx --type
merge -p '{"data":{"large-client-header-buffers":"4 32k","client-header-buffer-size":
"32k","proxy-buffer-size":"32k"}}'`

## C — Deploy sequence used (for repeatability)

1. Registered missing migrations (Fix 1), ran AuthDB migrations first and in
   isolation (Fix 2), verified `Permissions`/`RolePermissions` row counts via SQL
   *before* touching any running service — pure DB check, zero deploy risk.
2. Staged (but never needed) a break-glass SQL script to re-grant SuperAdmin every
   permission directly, in case the post-deploy JWT check came back empty.
3. Ran the remaining 14 databases' missing migrations + the
   `AllServices/004_FixMissingBaseEntityColumns.ps1` sweep (no further gaps found).
4. Rebuilt all 19 backend images + Gateway + Admin + Storefront one at a time
   (sequential builds — Docker Desktop had crashed under parallel/batched builds
   earlier in the session; the user asked for sequential going forward).
5. Applied K8s manifests (`deployment/k8s/apply-all.ps1`), then rolled out
   `identity-service` **alone first** and verified a fresh SuperAdmin login's JWT
   `permission` claim was fully populated (248 codes) before touching anything else.
6. Rolled out the remaining 13 services + Gateway + Admin + Storefront.
7. Fixes 5, 6, 7 were all found *during* this final verification pass (not before) —
   each required a targeted rebuild+redeploy of just the affected service
   (`gateway`, `identityservice`, `admin`+`storefront`) rather than a full second
   pass.

## Addendum — Fix 8: eliminated the redundant double `/api/` prefix at its root cause

Reported post-deploy: `GET /api/so/api/v1/quotations` → 500. Two independent bugs, found and
fixed separately.

**Bug A (real 500, DB schema)**: `SalesQuotations` (migration `Sales/007_AddSalesQuotations.sql`)
was created without any of `BaseEntity`'s columns (`TenantId`, `Description`, `IsDeleted`,
`CreatedBy/At`, `UpdatedBy/At`, `RowVersion`) even though `SalesQuotation : AggregateRoot`
requires all of them — every read 500'd with `Invalid column name 'TenantId'` etc.
(`SalesQuotationRepository.GetAllAsync`). Fixed by adding guarded `ALTER TABLE` statements to
`007_AddSalesQuotations.sql` (idempotent on both a fresh install and this already-migrated DB)
and applying them directly against the live `SalesDB`; also added `SalesQuotations` to
`AllServices/004_FixMissingBaseEntityColumns.ps1`'s `SalesDB` list for future coverage. No
service rebuild needed — pure schema fix.

**Bug B (architectural, the user's own diagnosis — "thừa /api/ trong URL, cần xử lý đúng chuẩn
RESTful")**: 37 Admin API client files (`soClient.ts`, `poClient.ts`, `mstClient.ts`,
`hrmClient.ts`, `crmClient.ts`, `accClient.ts`, `whClient.ts`, `prdClient.ts`, `logsClient.ts`,
`users/tenants/roles/permissions/settings/license.api.ts` + 22 `settings/*.settings.api.ts`)
all built already gateway-rooted, root-relative paths like `/so/api/v1/quotations`, then sent
them through `apiClient` — whose axios `baseURL` is `/api`. Axios only treats `scheme://` or
protocol-relative `//` URLs as absolute, so it prepended `baseURL` anyway, producing
`/api/so/api/v1/quotations`. A prior session's fix had papered over this by adding 17
duplicate "mirror" routes to `ocelot.json` (`/api/{prefix}/api/v1/{everything}` alongside the
correct `/{prefix}/api/v1/{everything}`) instead of fixing the actual double-prefixing.
**Fix**: added a second axios instance, `gatewayClient` (no `baseURL`, same auth/refresh
interceptors), in `src/frontend/admin/src/api/client.ts`; repointed all 37 files at it via
`import { gatewayClient as apiClient } from './client'` and dropped their now-dead
`API_BASE`/`GATEWAY_ROOT` computation (each file's `_BASE` constant is now just the literal
path). Removed all 17 now-unnecessary double-prefix mirror routes from `ocelot.json` (50 → 34
routes, re-verified zero duplicates) and added one previously-missing single-prefix route,
`/logs/api/v1/logs` (GET), which had only ever existed as a double-prefix mirror. `tsc -b`
clean, rebuilt + rolled out `gateway` and `admin` only (no backend service touched — this was
gateway/frontend routing only). Re-verified all 16 route families (id/po/so/wh/acc/crm/hrm/mst/
prd/pay/shp/pos/prm/ntf/rpt/logs) resolve correctly through the single-prefix path with no
`/api/` doubling, and confirmed the old double-prefix path now correctly 404s.
**Files**: `src/frontend/admin/src/api/client.ts` + 37 client files, `src/Gateway/Ocelot.API/ocelot.json`.

## Fix 9 — ProductDB `Categories` missing `Description` (same class of bug as Fix 8's Bug A)

Reported post-deploy: `GET /prd/api/v1/categories` → 500,
`Microsoft.Data.SqlClient.SqlException: Invalid column name 'Description'`. `Category :
AggregateRoot` inherits `BaseEntity.Description`, but `001_CreateProductDB.sql`'s original
`CREATE TABLE Categories` never included it, and `007_RebuildProductsAndMissingTables.sql`'s
"STEP 2: Add RowVersion to Categories (BaseEntity requires it)" — clearly already aware
Categories was missing BaseEntity columns — added `RowVersion` but missed `Description`
specifically. `ProductDbContext` has no explicit `Entity<Category>()` config, so EF maps
`Description` by convention regardless. **Fix**: added a guarded `ALTER TABLE Categories ADD
Description NVARCHAR(MAX) NULL` right after the existing RowVersion step in
`007_RebuildProductsAndMissingTables.sql`, applied directly to the live `ProductDB`. No
service rebuild needed — pure schema fix.
**File**: `src/Database/Product/007_RebuildProductsAndMissingTables.sql`

**Broader check run**: queried all 15 databases for tables with *some but not all* of the 8
BaseEntity-shaped columns (a table should have all of them if the C# entity extends
`BaseEntity`/`AggregateRoot`, or none if it's a plain value object/lookup/fact table — a
partial match is the same smell that caused Fixes 3/8A/9). Found ~35 more tables with partial
matches across Sales/Purchasing/Warehouse/Product/Shipping/Pos/Notification/Reporting/Hrm/
Auth DBs. Spot-checked one (`SalesDB.OrderNotes`, flagged as "missing TenantId"): its actual
C# type, `ERP.SO.Domain.ValueObjects.OrderNote`, is a plain value object with only
`Note`/`AddedBy`/`CreatedAt` — the extra `CreatedBy/UpdatedBy/UpdatedAt/IsDeleted/RowVersion/
Description` columns on the live table are harmless leftovers from an earlier schema, not
missing columns the entity needs. **This means the heuristic has real false positives and
each remaining hit needs its actual C# entity checked before touching schema** — not done
here, since neither of the two live bugs this session (Categories, SalesQuotations) came from
this list's other entries, and blindly patching ~35 tables on a heuristic risks either no-op
churn or masking a genuinely different problem. Flagging as a follow-up, not fixing
speculatively.

## Fix 10 — systemic EF↔schema mismatches across 7 services (proactive sweep)

Reported: `GET /wh/api/v1/stocktakes` and `GET /wh/api/v1/transfer-orders` → 500. After
fixing those, the user asked to proactively recheck other API URLs for the same class of
error instead of waiting for each one to be reported individually. This triggered a
systematic audit of all 15 services' `DbContext` files against their live database schemas,
which turned up a recurring pattern: **an EF entity's effective table name (by convention —
either the CLR class name, if it has no `DbSet<T>`, or the `DbSet<T>` property name) doesn't
match the table a migration actually created**, and separately, **entities whose migration
was simply never written or never run at all**. Both fail identically: `Invalid object name`
(missing table) or `Invalid column name` (missing/renamed column), only at the moment
something actually queries that table — meaning these can sit dormant for a long time if
nothing exercises that code path.

### 10a — WarehouseDB: 3 entities defaulted to the wrong table name
**File**: `src/services/WareHouseService/src/ERP.WH.Infrastructure/Persistence/WarehouseDbContext.cs`
- `StocktakeLine` (no own `DbSet`) → EF defaulted to class name `StocktakeLine`; real table is
  `StocktakeLines`. Added `entity.ToTable("StocktakeLines")`.
- `TransferOrderLine` (same shape) → real table `TransferOrderLines`. Added `.ToTable(...)`.
- `Reservation.ReservedItems` (owned collection via `OwnsMany`) → real table
  `Reservation_ReservedItems`; was missing entirely (an earlier session's fix configured the
  `OwnsMany` mapping itself but never named the table), causing `GET /wh/api/v1/reservations`
  and the **`ReservationExpiryProcessor` background job to fail silently on every tick**.
  Added `items.ToTable("Reservation_ReservedItems")`.

Also found via the same live-testing pass: **`Reservations.ReleaseReason`** (set by
`Reservation.Release(reason)`) was never added by any migration — added a guarded `ALTER
TABLE` to `Warehouse/005_AddModule8InventoryReconciliation.sql`.
**Rebuilt + redeployed**: `warehouse-service`.

### 10b — PaymentDB: migration registered but never executed
`SavedPaymentMethods`, `InstallmentPlans`, `InstallmentLines` all missing from the live DB —
`Payment/002_AddInvoiceLinkAndSavedMethodsAndInstallments.sql` was correctly listed in
`RunAllScripts.ps1` but had simply never been run against this database (no code bug; ran it
plus 003/004 directly). No rebuild needed.

### 10c — ShippingDB: same never-run gap, plus a real batch-scoping bug in the migration itself
`ReturnShipments` was missing (migration registered, never run — same as 10b). While
re-running it, hit a **second, independent bug**: `Shipping/002_AddReturnsPodAddressRestructure.sql`
converts `ShipmentTrackingEvents.Status` from `int` to `nvarchar` inside a single
`BEGIN...END` batch — `ALTER TABLE ADD Status_New` followed by `UPDATE ... SET Status_New =
...` in the *same* batch. SQL Server compiles an entire ad-hoc batch before executing any of
it, so the `UPDATE`'s reference to a column the `ALTER` two lines above hadn't committed yet
failed at compile time with `Invalid column name 'Status_New'` — meaning **this conversion
had never once succeeded on any database this migration ever ran against**, and
`ShipmentTrackingEvents.Status` stayed `int` everywhere, silently mismatched against the
string-typed C# entity. Fixed by splitting the single batch into four separate `GO` batches
so each step can see the previous step's completed DDL (each independently guarded, safe to
re-run). Also added a missing `IF NOT EXISTS` guard around `ReturnShipments`'s two
`CREATE INDEX` statements (same "unguarded index after guarded table" pattern as prior
sessions' fixes). No rebuild needed — pure schema/migration fix.

### 10d — Three services: entities live-wired to controllers, but no migration ever created their table
- **AccountingDB**: `TaxRate`, `BankAccount`, `BankStatement` — all reachable via
  `TaxRatesController`/`BankAccountsController` and their handlers/repositories, but never
  migrated at all. Wrote `Accounting/003_AddTaxRatesBankAccountsAndStatements.sql`.
- **CrmDB**: `ContactTag` — reachable via `ContactsController`/`ContactTagRepository`, never
  migrated. Wrote `CRM/003_AddContactTags.sql`.
- **PosDB**: `PosProductCache`, `PosPromotionCache` — reachable via `ProductLookupQueryHandler`
  and the Kafka-driven cache-sync event handlers (`POSIntegrationEventHandlers`), never
  migrated. Wrote `Pos/003_AddProductAndPromotionCache.sql`.

All three schemas were reconstructed directly from each entity's C# properties + its EF
`modelBuilder.Entity<T>()` config (precision/maxlength). Ran all three directly against their
live databases; registered all three in `RunAllScripts.ps1`. No code changes, no rebuild.

### 10e — NotificationDB: table-name mismatches plus two more missing/renamed columns
**File**: `src/services/NotificationService/src/ERP.NTF.Infrastructure/Persistence/NotificationDbContext.cs`
- `NotificationTemplate` (`DbSet` property `Templates`) and `NotificationPreference` (`DbSet`
  property `Preferences`) both defaulted to those DbSet names; real tables are
  `NotificationTemplates`/`NotificationPreferences`. Added explicit `.ToTable(...)` to both.
- `WebhookEndpoint` — fully wired (`WebhooksController`/`WebhookHandlers`), no migration ever
  created its table at all. Wrote `Notification/003_AddWebhookEndpoints.sql`.
- Fixing the `NotificationTemplate` table-name mismatch immediately surfaced a **second**,
  previously-unreachable bug: `NotificationTemplate.Variables` (unconfigured `string?`, EF
  default `nvarchar(max)`) was never added to `NotificationTemplates`. Added to the same
  migration file (which now also creates `WebhookEndpoints`).
- Fixing that surfaced a **third**: `NotificationPreference.OptOutAt` doesn't exist — the live
  column is named `OptedOutAt` (`001_CreateNotificationDB.sql`). Rather than renaming a live
  column, mapped it explicitly: `e.Property(x => x.OptOutAt).HasColumnName("OptedOutAt")`.
  This is the same "unreachable code path masks the next bug" pattern seen throughout this
  session — each fix only exposed the next once the query could get further before failing.
**Rebuilt + redeployed twice**: `notification-service` (once for the `ToTable`/`Variables`
fix, once more for the `OptOutAt` column mapping found immediately after).

### 10f — ReportingDB: same table-name mismatch, but currently dormant
`DimDate` (`DbSet` property `DimDates`) would default to table name `DimDates`; real table is
`DimDate` (singular, `001_CreateReportingDB.sql`). Added `.ToTable("DimDate")`. Fixed
proactively even though nothing currently queries `.DimDates` directly (only referenced via
plain FK columns from the Fact tables, not EF navigation) — same latent-landmine reasoning as
the WarehouseDB fixes: cheap to fix now, expensive to debug later once something does query
it. **Rebuilt + redeployed**: `reporting-service`.

### Audit methodology (for repeating this check later)
Wrote a script that, per `DbContext.cs`: parses every `DbSet<Type> Property` declaration and
every `modelBuilder.Entity<Type>(entity => { ... })` block; computes EF's effective default
table name (explicit `.ToTable()` if present, else the matching `DbSet` property name, else
the bare class name); and cross-references that name against a live dump of `sys.tables` for
that entity's actual database. Every mismatch is then checked against the entity's real C#
properties and actual controller/handler wiring before touching schema — the same heuristic
run earlier in this session (looking for tables with partial `BaseEntity` column coverage)
produced real false positives (extra leftover columns on plain value objects, e.g.
`SalesDB.OrderNotes`), so **every flagged mismatch was verified individually, not
auto-patched**. `HrmDB`, `AuthDB`, `ProductDB`, `PromotionDB`, `PurchasingDB`, `SalesDB`, and
`MasterDataDB` came back clean — no further action needed there.

## Fix 11 — AccountingDB: `FiscalPeriod.PeriodName` vs live column `Name`

Reported: `GET /acc/api/v1/fiscal-periods` → 500, `Invalid column name 'PeriodName'`. Same
rename-drift class as Fix 10e's `OptOutAt`/`OptedOutAt`: the C# entity's property is
`PeriodName`, but the live `FiscalPeriods` table (`001_CreateAccountingDB.sql`) names the
column `Name`. Unlike the Warehouse/Notification cases, `AccountingDbContext` already has an
established, deliberate pattern for exactly this situation elsewhere in the same file —
`Invoice.Amount` → `HasColumnName("TotalAmount")`, `JournalLine.Debit/Credit` →
`HasColumnName("DebitAmount"/"CreditAmount")` — `FiscalPeriod` just never got its own entry.
**Fix**: added `modelBuilder.Entity<FiscalPeriod>(e => e.Property(x => x.PeriodName)
.HasColumnName("Name"))`, following the same convention. No DB change — pure column-name
mapping, so the live table's extra unused columns (`FiscalYear`, `PeriodMonth`, `ClosedAt`,
`ClosedBy` — present in the table but not on the C# entity) are left alone as harmless.
**File**: `src/services/AccountingService/src/ERP.ACC.Infrastructure/Persistence/AccountingDbContext.cs`
**Rebuilt + redeployed**: `accounting-service`. Regression-checked `invoices`,
`journal-entries`, `accounts` — all still 200.

## Fix 12 — AccountingDB: `Bill.Amount` vs live column `TotalAmount`

Reported: `GET /acc/api/v1/bills` → 500, `Invalid column name 'Amount'`. Identical bug to
Fix 11, same file, same root cause: `Bill.Amount` had no `modelBuilder.Entity<Bill>()` config
at all, so EF mapped it to a column literally named `Amount` — the live `Bills` table has no
such column, only `TotalAmount` (`001_CreateAccountingDB.sql`). This is the exact same
mismatch `Invoice.Amount` already has a fix for two blocks above it in this same file
(`HasColumnName("TotalAmount")`) — `Bill` was simply never given the same treatment.
**Fix**: added `modelBuilder.Entity<Bill>(e => e.Property(x => x.Amount)
.HasColumnName("TotalAmount").HasColumnType("decimal(18,2)"))`, copying the `Invoice` config
verbatim. `Bills`' other extra columns (`SupplierName`, `SubTotal`, `TaxAmount`, `AmountPaid`,
`AmountDue`, `CurrencyCode`, `Notes`, `ApprovedAt`/`By`, `PaidAt`) aren't on the `Bill` entity
at all and are left alone as harmless unused columns — same as `FiscalPeriod`'s extras in
Fix 11.
**File**: `src/services/AccountingService/src/ERP.ACC.Infrastructure/Persistence/AccountingDbContext.cs`
**Rebuilt + redeployed**: `accounting-service`. Regression-checked `invoices`,
`fiscal-periods`, `journal-entries`, `accounts`, `tax-rates`, `bank-accounts` — all 200.

**Follow-up done immediately**: since 3 of the last 4 Accounting fixes were the identical
property-vs-column-name shape, manually cross-checked every remaining entity in
`AccountingDbContext` (`Account`, `JournalEntry`, `JournalLine`) against `Accounts`/
`JournalEntries`/`JournalEntryLines`' live columns — all match cleanly, no further action
needed. `TaxRate`/`BankAccount`/`BankStatement` (Fix 10d) are guaranteed consistent since
their tables were generated directly from these same entities' properties + EF config. This
closes out `AccountingDbContext` for this class of bug — every entity in the file has now
been individually verified against its live table, not just spot-checked.

## Fix 13 — deployed a new login-redesign batch (per-tenant email + subdomain tenant resolution); found and fixed a Gateway regression that made it completely non-functional

User asked to deploy IdentityService + Frontend. Found a large, previously-unbuilt batch of
changes already sitting in the working tree (not committed, not something built this
session): `Users.Email` uniqueness moved from global to per-tenant
(`Identity/014_ChangeEmailUniqueIndexToPerTenant.sql`, `UserConfiguration.cs`), and Admin's
login page dropped its manual "Tenant Code" field entirely in favor of resolving the tenant
from the request's subdomain at the Gateway (`GatewayAuthorizationMiddleware
.ResolveTenantCodeFromHost`) — a `X-Tenant-Code` header the Gateway injects before forwarding
to IdentityService, which now hard-rejects with 400 if it's missing.

**Blocking issue found before deploying**: this cluster's ingress only has the bare host
`erp.local` for Admin — no per-tenant subdomain existed anywhere, and
`ResolveTenantCodeFromHost` returns `null` for the bare base domain by design. Deploying as
written would have 400'd every single Admin login, including SuperAdmin's. Raised this to the
user before proceeding; agreed fix: added a `system.erp.local` ingress host (same `admin`
backend) for the `SYSTEM` tenant, plus a matching Windows hosts-file entry, so subdomain
resolution has something to resolve.

**Second bug found live, after the ingress fix still didn't work**: `GatewayAuthorizationMiddleware`'s
own path-prefix allowlists (`TenantResolvedAuthPathPrefixes`, `KnownPublicPathPrefixes`) still
listed pre-versioning paths (`/api/auth/login`) instead of the real routes
(`/api/v1/auth/login`) — a leftover from before this session's `/v1/`-versioning pass that
nobody updated when the login redesign was written. Since `"/api/v1/auth/login".StartsWith
("/api/auth/login")` is false, the subdomain-resolution branch **never ran at all, for any
host** — the whole feature was dead code from the moment it was written, regardless of the
ingress/subdomain question. Fixed both arrays to match the real `/v1/`-versioned paths, and
removed one dead entry (`/api/logs/api/v1/logs/client`, a double-prefix mirror route removed
earlier in Fix 10).
**Files**: `deployment/k8s/ingress.yaml`, `src/Gateway/Ocelot.API/Authorization/GatewayAuthorizationMiddleware.cs`,
`src/services/IdentityService/*` (login redesign, pre-existing), `src/frontend/admin/*` +
`src/frontend/storefront/*` (pre-existing — Storefront already self-sends `X-Tenant-Code` via
a build-time env var, unaffected by any of this).

**Verified end-to-end** after redeploying Gateway + IdentityService + Admin + Storefront:
login via `system.erp.local` with no tenant code in the body → 200 with full permission
claim; login via bare `erp.local` → correctly rejected (`"Unable to resolve tenant from
request host."`) since no tenant subdomain applies there, as designed; Storefront login
(replicating its own `X-Tenant-Code: SYSTEM` header) → 200; authenticated follow-up calls
(`/id/api/v1/tenants`, `/id/api/v1/users`) through the new host → 200.

**Also ran, applied directly to the live AuthDB**: `Identity/014_ChangeEmailUniqueIndexToPerTenant.sql`
(idempotent — composite `(Email, TenantId)` unique index is strictly looser than the old
global-`Email` one, so no de-duplication needed first). Registered in `RunAllScripts.ps1`.

### Docker Desktop instability — root cause found and fixed this round
Repeated build failures/crashes throughout this session were traced to a real, fixable root
cause, not random flakiness: **no `.dockerignore` existed anywhere in the repo**, and all 14
.NET services build with `context: .` (repo root) against the shared root Dockerfile. Every
single build was sending the *entire* monorepo as build context — measured at ~3.1GB
(1.86GB of `bin`/`obj` across 140+ directories accumulated from this session's many local
`dotnet build` sanity checks, ~270MB of `node_modules` across the frontend/mobile apps, plus
`.git`, docs, deployment manifests). Repeatedly hashing and transferring gigabytes of
irrelevant context on every build is a highly plausible direct cause of the sustained
Desktop-VM resource pressure behind this session's repeated Docker Desktop
crashes/network-instability episodes. **Fixed**: added `.dockerignore` at the repo root
(excludes `**/bin/`, `**/obj/`, `**/node_modules/`, `.git/`, `docs/`, `tests/`,
`deployment/`) plus scoped ones in `src/frontend/admin/` and `src/frontend/storefront/`
(their own `node_modules/`/`dist/`). Verified: a subsequent Gateway build context resolved
immediately instead of stalling for minutes.

Separately (not fixed by the above — a different, unrelated failure mode also hit this
round): Docker Desktop's embedded DNS resolver serving containers (`192.168.65.7`) went into
a stuck state where containers could reach the network by raw IP but couldn't resolve any
hostname at all (`nuget.org`, `mcr.microsoft.com`), while the host itself resolved fine — a
known WSL2-backend Docker Desktop issue. A second `wsl --shutdown` + Docker Desktop restart
cycle cleared it. The `.dockerignore` fix should make this kind of episode much rarer going
forward (removes the sustained load contributing to it) but doesn't prevent this specific
DNS-forwarder failure mode outright if it recurs — same remediation (`wsl --shutdown`, then
reopen Docker Desktop) applies if it does.

## Fix 14 — SalesDB: three owned-collection shadow keys typed as `int` instead of `Guid`

Reported: `GET /api/v1/orders` → 500, `System.InvalidCastException: Unable to cast object of
type 'System.Guid' to type 'System.Int32'`. This one stayed latent through every earlier
regression sweep this session (including one run minutes before this report) because the
dev DB's `SalesOrders` table was empty at the time — the bug only fires once EF actually has
to shape an owned collection row, so an empty result set never touches it. It surfaced the
moment real order data existed (1200 rows, seeded/created since).

**Root cause**: `SalesOrderDbContext`'s three `OwnsMany` configs for `SalesOrder`
(`LineItems`→`SalesOrderItems`, `InternalNotes`→`OrderNotes`, `StatusHistory`→
`OrderStatusHistory`) each declare a shadow key via `.HasKey("Id")` alone, with no explicit
`.Property<Guid>("Id")` first — EF Core's convention for an untyped shadow key named "Id"
defaults to `int`, but all three tables' real `Id` column is `uniqueidentifier` (confirmed via
`sys.columns`/`sys.types`). Every list/detail read that included any of these collections hit
the cast failure. The correct pattern already exists elsewhere in this codebase
(`WarehouseDbContext`'s `Reservation.ReservedItems`: `items.Property<int>("Id");
items.HasKey("Id");`) — these three configs just never got it.
**Fix**: added `.Property<Guid>("Id")` immediately before `.HasKey("Id")` in all three
`OwnsMany` blocks.
**File**: `src/services/SaleOrderService/src/ERP.SO.Infrastructure/SalesOrderDbContext.cs`
**Rebuilt + redeployed**: `salesorder-service`. Verified against the real 1200-row dataset:
`GET /api/v1/orders` → 200, `GET /api/v1/orders/{id}` (includes `LineItems`) → 200.

**Process note**: this is the second bug this session (after Fix 13's dormant `DimDate`
mismatch) found only because it was *exercised with real data* — empty-table regression
sweeps are blind to shape/cast bugs in owned collections. Worth remembering when a
"passed regression, still 500s in practice" report comes in: check whether the sweep's test
data actually populated the collection in question before assuming it's a new regression.

## Fix 15 — reviewed and deployed a large new feature batch; found the erp namespace had been deleted; found and removed a duplicate resource-hogging docker-compose stack

User asked to review accumulated code changes and deploy them. This batch was substantially
larger than prior passes — new features (not bug fixes) across most of the platform:

- **ProductService**: product images moved to Azure Blob Storage (`AzureBlobFileStorageService`,
  backed by Azurite locally — new `deployment/k8s/infrastructure/azurite.yaml` StatefulSet,
  connection string wired correctly to `azurite:10000` in `appsettings.Docker.json`). Reviewed
  the DI wiring, container-creation logic, and blob-path parsing — all sound. Also three
  ProductDB schema-drift fixes (`012`/`013`/`014`) matching bugs already known from a prior
  session's notes (`ProductImages` missing 4 `BaseEntity` columns; `ProductAttributeValues
  .OwnerType` TINYINT vs EF's INT convention; `Products.RowVersion` missing on DBs that
  predated the table-rebuild migration) — already applied live.
- **PurchaseOrderService**: new two-step Manager→Finance approval chain for POs at/above a
  $5,000 threshold (`PurchaseOrder.Approve()`/`ApproveFinance()`, new `PendingFinanceApproval`
  status appended at the end of the enum to preserve raw-int compatibility, new
  `purchase_order:finance_approve` permission consistently wired through the DB seed
  (`Identity/016`), `PermissionConstants`, and the controller attribute). Reviewed the full
  chain end to end — consistent naming, correct state-machine guards, `Reject()` correctly
  extended to also allow rejection during the Finance-pending state.
- **NotificationService**: new event handlers + Kafka topic subscriptions
  (`purchasing-topic`, `hrm-topic`) for the new PO approval-chain and Leave-request events;
  also fixed a real, pre-existing latent bug found while adding these — `JToken.Value<Guid?>()`
  throws `InvalidCastException` on a plain JSON string in this environment, silently breaking
  every existing Guid-parsing branch (Payment/Shipment/Employee events) since none of their
  outcomes had ever been verified against real flowing data before. Replaced with an explicit
  `Guid.TryParse` helper across all branches, old and new.
- **WarehouseService**: `StockMovement` never had a `TenantId` property at all despite the DB
  column being `NOT NULL` — every insert site updated to pass the fixed system-tenant GUID
  (honestly documented as a stopgap pending real multi-tenant plumbing through this service).
- Two dozen new demo-data seed scripts (one per service, `NNN_SeedDemoData.sql`) following a
  new cross-service deterministic-GUID convention (`src/Database/SEED_DATA_CONVENTIONS.md`) —
  already applied live (1200 orders, 1050 products, 1103 users, 550 POs confirmed via row
  counts) before this session touched anything, so none were re-run.

All of the above build-checked cleanly (`dotnet build` per affected project + a full solution
rebuild for the two projects the `.sln` actually references) before any deployment. Registered
all 26 previously-unregistered migration files in `RunAllScripts.ps1` in dependency-correct
order (schema-fix migrations in their existing per-service blocks; the 15 demo-data seed
scripts moved into one dedicated final block, ordered per `SEED_DATA_CONVENTIONS.md`'s
cross-service dependency chain — e.g. Identity users before CRM contacts before Sales orders,
Reporting's fact-seed last since it reads from everything). Deliberately **not** registered:
`Identity/015_test_wrapped.sql` — a stray scratch copy of `015_SeedDemoUsersAndRoles.sql`
wrapped in `BEGIN TRANSACTION ... ROLLBACK TRANSACTION`, clearly a dry-run artifact
accidentally left in the migrations folder rather than a real migration; flagged to the user,
not deleted.

### Found mid-deploy: the entire `erp` K8s namespace had been deleted
`kubectl apply` for the new Azurite manifest failed with `namespaces "erp" not found` —
`kubectl get pods -A` confirmed the entire namespace (all 30+ Deployments/StatefulSets/the
Ingress) was gone, `kubectl get pv` returned nothing (Persistent Volumes gone too, likely
`Delete` reclaim policy). The core cluster (`kube-system`, `ingress-nginx`) was untouched and
had 26 days of uptime, so this wasn't a Docker Desktop VM reset — something explicitly deleted
just the `erp` namespace. **No data was lost**: the actual business data lives on the
host-installed SQL Server (`host.docker.internal,1433`, confirmed via `secrets.yaml`), never
inside the namespace that got deleted — only the in-cluster infra state (Kafka/RabbitMQ/Redis/
Elasticsearch — all rebuildable/transient) and every Deployment needed recreating. Flagged to
the user before acting; recreated everything via the existing `deployment/k8s/apply-all.ps1`
(added the new `azurite.yaml` apply step to it) — namespace, configmap, secrets, infra,
all 19 Deployments+Services, Ingress, all from version-controlled manifests.

### Found mid-recreation: a second, duplicate stack was resource-starving Docker Desktop
While the freshly-recreated K8s cluster was still trying to schedule ~30 pods, both `docker
build` and `kubectl` started timing out/erroring (`TLS handshake timeout`, Docker daemon `500
Internal Server Error`) — the same instability pattern from earlier in this session, but this
time traced to a **second, complete copy of the whole stack** running via plain `docker
compose up` in parallel: its own containerized SQL Server, Kafka, RabbitMQ, Redis,
Elasticsearch, and all 19 .NET services + both frontends (`docker compose ps` showed it live
for 5 hours, several containers already crash-looping with exit code 139/SIGSEGV). Running a
full duplicate of the entire platform's infrastructure alongside the K8s deployment this whole
time very plausibly explains the repeated, otherwise-inexplicable Docker Desktop
crashes/instability seen throughout this session, independent of the earlier `.dockerignore`
build-context fix. Flagged to the user before touching it (unclear provenance — not something
this session started); user confirmed removing it. `docker compose down` stopped and removed
all 24 of its containers plus its network; K8s pods started successfully scheduling and
stabilizing within minutes once the resource pressure was gone.

**Final state after recreating the namespace, stopping the duplicate stack, and rebuilding +
redeploying all 19 images with the new feature batch**: every pod `1/1 Running`, 0 restarts.
Verified login, `id/tenants`, `so/orders` (1200 rows), `po/suppliers`, `wh/stocktakes`,
`acc/bills`, `crm/contacts`, `mst/units-of-measure`, `prd/categories`, `prd/products`,
`pay/payments`, `ntf/templates`, the new PO finance-approval-bearing `po/purchase-orders` list,
and both frontend apps — all 200.

## Fix 16 — reviewed and deployed ChatBotService (new microservice); a K8s networking outage traced to `sessionAffinity: ClientIP` on an unsupported kernel; three real ChatBotService bugs found once auth/DB were actually exercised

Reviewed a large new batch centered on **ChatBotService** (Port 5017, Code: CHB) — a brand-new
microservice answering via live tool-calling against other services' REST APIs through the
Gateway, plus supporting changes to CRM (`GET /contacts/by-user/{userId}`), `ERP.Shared.Authorization`
(SignalR query-string JWT fallback scoped to `/hubs`, new `ChatBot`/`ChatBotPremium` module
codes), the Gateway route, both frontends' SignalR wiring, and K8s/Compose manifests. Code
quality was high — reading through `Program.cs`, `ErpGatewayClient`, `ChatProviderResolver`,
`CrmEscalationClient`, `ChatHub`, and `ChatBotDbContext` against `001_CreateChatBotDB.sql`
found nothing wrong. Two environment-config gaps were fixed immediately (Admin was missing a
real `.env` entirely — `VITE_CHATBOT_WS_URL` would have built as `undefined`; the K8s Ingress
had no host rule for `chat.erp.local`, the dedicated non-Gateway host the plan called for).

Deploying it, however, triggered a K8s networking outage that took most of this session to
root-cause, plus surfaced three real bugs in ChatBotService's auth/DB layer that no amount of
code review would have caught — only exercising them live did.

### The outage: `kube-proxy` couldn't sync ClusterIP routing for *any* Service, cluster-wide
First symptom was the already-known Kafka self-DNS-resolution deadlock recurring
(`UnknownHostException: kafka`) after a routine Docker Desktop restart. Chasing it down
revealed the real scope was much bigger: **no Service on the cluster could resolve or route**,
including CoreDNS's own upstream call to the Kubernetes API's ClusterIP
(`dial tcp 10.96.0.1:443: connect: connection refused`). `kube-proxy`'s logs showed the actual
cause: `iptables-restore` failing on every sync attempt —
`Extension recent revision 0 not supported, missing kernel module?` followed by
`RULE_APPEND failed (No such file or directory)` for a specific, *stable* chain hash
(`KUBE-SVC-X42DJOC3OWYTCKB4`) that reappeared identically across multiple full cluster resets —
proof it was one deterministic Service, not random corruption.

Two dead-end remediation attempts before finding the real cause (documented for next time this
kernel/iptables combination bites): a `wsl --shutdown` + Docker Desktop restart (full WSL2 VM
reboot) did not fix it — the identical `iptables-restore` failure reappeared immediately on a
freshly-booted VM, ruling out "corrupted VM state." Docker Desktop's own **Reset Kubernetes
Cluster** (full etcd wipe, all namespaces/Deployments/Secrets/PVs gone, no SQL data lost since
that's host-based) did restore *general* ClusterIP routing and DNS — proving the cluster
control-plane layer itself was fine — but the exact same `iptables-restore` error, with the
exact same chain hash, came straight back once every manifest was reapplied.

**Root cause**: `kube-proxy`'s iptables mode only emits `-m recent` rules (the extension the
kernel warning names) to implement `sessionAffinity: ClientIP`. Across this whole manifest set,
exactly one Service used it — `chatbotservice` in `deployment/k8s/deployments/chatbot.yaml`,
added for ChatHub's SignalR sticky-routing. This WSL2 kernel build doesn't support `xt_recent`,
so `iptables-restore` fails on that Service's rules — and because the sync is one atomic batch,
the *entire* ruleset for *every* Service fails with it, which is why literally nothing's
ClusterIP routing ever fully worked no matter how many times the cluster or VM was reset.
`chatbot-service` runs a single replica, so `sessionAffinity` provided zero actual behavior
change — removed it (`kafka.yaml` also picked up a `startupProbe` while investigating, unrelated
but worth keeping: Kafka's boot time grows with its accumulated `__consumer_offsets` log after
each kill, so a fixed `initialDelaySeconds` on liveness had become a self-reinforcing crash
loop). Also found and fixed along the way: ingress-nginx's default header buffer is too small
for this app's fully-populated SuperAdmin JWT (~10KB) — `400 Request Header Or Cookie Too Large`
on every re-install of the ingress controller from its stock manifest; patched
`large-client-header-buffers`/`client-header-buffer-size` onto the `ingress-nginx-controller`
ConfigMap (this must have been custom-tuned before whatever earlier event wiped it — worth
carrying forward into the stock manifest permanently rather than re-patching after every future
ingress-nginx reinstall).

### Three real ChatBotService bugs, found only once login + a real DB round-trip were exercised
1. **`Jwt:Issuer` typo** — `ERP.CHB.API/appsettings.json` had `"ERP.IdentityService"`; every
   other service (and IdentityService itself, the actual token signer) uses `"ERP.Identity"`.
   With `ValidateIssuer = true`, every token — including a genuinely valid SuperAdmin JWT that
   worked against every other service — failed JWT Bearer validation silently. No 401 resulted
   because of bug 2 below, making this harder to spot than a normal auth failure.
2. **`ConversationsController` had no `[Authorize]`** — only `Start` had an explicit
   `[AllowAnonymous]` (intentional, for guest chat). Without a class-level `[Authorize]`,
   `ListMine`/`GetHistory` silently accepted unauthenticated requests, and the empty
   `UserContext` only surfaced as an unhandled `UnauthorizedAccessException` → bare 500 deep in
   the application layer, instead of a clean 401 at the pipeline. Added `[Authorize]` at the
   controller level (matching `Start`'s explicit override).
3. **`ChatBotDbContext` never ignored `BaseEntity.IsDeleted`** — unlike `DomainEvents`/
   `RowVersion` (both explicitly ignored, with a comment explaining why), `IsDeleted` was left
   unmapped-but-not-ignored, so EF Core included it by convention. `001_CreateChatBotDB.sql`
   never had this column (no soft-delete UX exists for conversations), so the very first real
   query threw `Invalid column name 'IsDeleted'`. Same root-cause pattern as Fix 14 and the
   ProductDB schema-drift bugs: invisible to review, invisible to a passing build, only ever
   surfaces once a query actually runs against the real schema. Added `e.Ignore(x => x.IsDeleted)`
   alongside the other two.

All three only became visible once login → JWT → a real downstream query chain was actually
exercised end-to-end — the code review alone (including a column-for-column schema comparison)
had called this file clean.

**Final verification**: `kubectl get pods -n erp` — 0 pods outside `Running`, 0 climbing restart
counts. `GET /id/api/v1/tenants`, `/api/v1/orders`, `/api/v1/products` → 200. `GET
/api/v1/chatbot/conversations` with a valid token → 200 `[]`; without one → clean 401 (was 500).
`POST /hubs/chat/negotiate` → 200. `erp.local`, `system.erp.local`, `shop.erp.local` → 200.

## Outstanding / known follow-ups (not fixed, out of scope)

- Admin frontend hardcodes 169 permission literal strings across 89 files instead of
  importing from a shared constants source (already flagged in the repo's own docs
  as a pending Phase 3 item).
- `docker-compose.yml`'s new `Security__SettingsEncryptionKey` wiring gap (present in
  K8s already, missing from compose) — not fixed since K8s is the deploy path in use.
- No test users exist for Admin/Manager/Purchaser/Viewer roles to live-verify their
  JWTs via HTTP (only SuperAdmin and two Customer-role users exist) — the
  DB-driven `RolePermissions` row counts were spot-checked instead (SuperAdmin=248,
  Admin=150, Viewer=39), which uses the identical code path as the live-verified
  SuperAdmin JWT.
- The `large-client-header-buffers`/`client-header-buffer-size` fix from Fix 16 is a live
  `kubectl patch` against the `ingress-nginx-controller` ConfigMap, not captured in any
  version-controlled manifest — a future `Reset Kubernetes Cluster` or ingress-nginx
  reinstall from its stock upstream manifest will silently lose it and reintroduce the
  `400 Request Header Or Cookie Too Large` bug on login. Should be folded into whatever
  process (re-)installs ingress-nginx for this repo, e.g. a values/patch file applied
  right after the stock `deploy.yaml`, so it isn't tribal knowledge.
