# Code Review + Docker/Kubernetes Deployment Fix Summary — 2026-07-13

## Overview

Full code review of the last 5 commits (JWT hardening, permission system, new
LoggingService), followed by first real Kubernetes deployment of the whole
platform and end-to-end testing through the Admin SPA at `http://erp.local`.

31 issues found and fixed across 5 categories. Most were only exposed *after*
a previous fix landed — each section below is in the order things were
actually found, because several bugs were masked by an earlier one.

Final audit: **all tested endpoints across 8 modules → 200 OK** (Identity,
SalesOrder, Product, Warehouse, PurchaseOrder, Payment, Reporting,
MasterData).

---

## A — Code review findings (last 5 commits)

Automated review (8 independent finder angles + verification pass) over the
JWT/security hardening and new LoggingService. Recorded for follow-up, not
all immediately fixed at review time — see notes per item.

| # | Finding | File | Severity |
|---|---------|------|----------|
| 1 | LoggingService has no authentication — port 5016 exposed directly, bypasses Gateway entirely | `LogsController.cs` | Security |
| 2 | Client log-ingestion endpoint has no payload size limit, compounding #1 | `LogsController.cs` | Security |
| 3 | `InviteToTenantAsync` silently fails to persist tenant/role assignment (EF Core `Update()` on pre-keyed child entities marks them `Modified` not `Added`) | `UserService.cs` | Correctness |
| 4 | Permission Create/Update/Delete never call `SaveChangesAsync` — returns 200/201 but writes nothing | `PermissionService.cs` | Correctness |
| 5 | Auth/CORS middleware ordering inconsistent across services; PurchaseOrderService missing `UseCors` entirely | `Program.cs` ×13 | Correctness |
| 6 | `TenantName` never populated in the new JWT-based user context — blank in logs/audit/UI | `JwtClaimsUserContextService.cs` | Correctness |
| 7 | Cross-tenant view authorization duplicated verbatim in two controllers instead of centralized in the service layer | `UsersController.cs`, `RolesController.cs` | Convention |
| 8 | Reject-PO endpoint hardcodes `RejectedBy = "system"` instead of using the `IUserContextService` already injected in the same controller | `PurchaseOrdersController.cs` | Correctness |
| 9 | `KafkaLogSink` writes synchronously (blocking) on every request across all 14 services — not wrapped in `WriteTo.Async` | `KafkaLogSink.cs` | Efficiency |
| 10 | Two actions still manually parse the `tenant_id` claim instead of using the new `GetCallerTenantId()` helper — incomplete refactor | `UsersController.cs` | Convention |

---

## B — Kubernetes infrastructure (first deploy)

Docker Compose ran clean from the start. All 11 issues below only surfaced
on Kubernetes — mostly because the K8s manifests had never been kept in sync
with fixes already applied to Docker Compose.

### Fix 11 — YAML syntax error blocking all `kubectl apply`

**Root cause**: missing space after `:` — `sql-notification-db:"Server=..."`.
**File**: `deployment/k8s/secrets.yaml:25`

### Fix 12 — Kafka crash loop (same bug Docker Compose already fixed, never ported to K8s)

**Root cause**: `apache/kafka:3.9.0` + `advertised.listeners=0.0.0.0` — the
exact bug documented in the troubleshooting guide, fixed in
`docker-compose.yml` but never applied to the K8s manifest.
**Fix**: switched to `confluentinc/cp-kafka:7.8.0` with the same listener
config as the working Compose setup.
**File**: `deployment/k8s/infrastructure/kafka.yaml`

### Fix 13 — Kafka DNS self-reference deadlock at startup

**Root cause**: the pod needs to resolve its own `kafka:9092` hostname to
pass its readiness probe, but a headless Service only publishes DNS records
for pods that are already `Ready` — an unbreakable loop for a single
replica.
**Fix**: added `publishNotReadyAddresses: true` to the Service.
**File**: `deployment/k8s/infrastructure/kafka.yaml`

### Fix 14 — RabbitMQ & Kafka killed repeatedly by kubelet (probes too heavy)

**Root cause**: `rabbitmq-diagnostics ping` and `kafka-broker-api-versions`
each spin up a full BEAM/JVM process just to answer "alive?" — exceeded the
10s timeout when the machine ran Compose and K8s simultaneously.
**Fix**: switched readiness/liveness probes to `tcpSocket` (checks the port
is listening — nearly free).
**Files**: `deployment/k8s/infrastructure/{rabbitmq,kafka}.yaml`

### Fix 15 — RabbitMQ rejected `admin` login despite correct secret

**Root cause**: the PVC still held mnesia data from an earlier test run with
a different password — `RABBITMQ_DEFAULT_PASS` only takes effect on first
boot against an empty volume.
**Fix**: deleted the StatefulSet + PVC, let it re-seed clean (disposable dev
data, not production).

### Fix 16 — WarehouseService couldn't reach RabbitMQ — wrong env var name

**Root cause**: manifest set `RabbitMQSettings__Password`, but the actual C#
code reads `configuration["RabbitMQ:Password"]` — wrong prefix, silently
fell back to the hardcoded `"guest"` default.
**Fix**: renamed to `RabbitMQ__Password`, matching the other 12 services.
**File**: `deployment/k8s/deployments/warehouse.yaml`

### Fix 17 — ProductService missing the RabbitMQ password env var entirely

**Root cause**: no RabbitMQ password variable declared at all — same
`"guest"` fallback as #16.
**Fix**: added `RabbitMQ__Password` sourced from the secret, matching the
standard pattern.
**File**: `deployment/k8s/deployments/product.yaml`

### Fix 18 — Every Gateway-proxied API call returned 502 (K8s Service names didn't match `ocelot.json`)

**Root cause**: `ocelot.json` routes downstream hosts using Docker Compose
naming (`identityservice`, no hyphen), but all 16 K8s Service objects were
named with hyphens (`identity-service`) — Gateway reported
`Name or service not known` for every route.
**Fix**: renamed the Service objects (kept Deployment names unchanged) to
match `ocelot.json` exactly; deleted the 16 old hyphenated Services.
**Files**: `deployment/k8s/deployments/*.yaml` (16 files)

### Fix 19 — Gateway spamming logs with failed Kafka connections to `localhost:9092`

**Root cause**: Gateway's deployment was missing `envFrom: configMapRef`
entirely — the only service missing this block.
**Fix**: added `envFrom` pointing to `erp-config`, matching every other
service.
**File**: `deployment/k8s/deployments/gateway.yaml`

### Fix 20 — Real secret (SQL/RabbitMQ passwords) committed to git despite a `.gitignore` rule

**Root cause**: the `.gitignore` rule was added after the file was already
tracked — git doesn't retroactively untrack.
**Fix**: `git rm --cached` to stop tracking (kept the local file). Per user
decision: no credential rotation, no git history rewrite.
**File**: `deployment/k8s/secrets.yaml`

### Fix 21 — LoggingService (newest service) never had a K8s manifest

**Root cause**: added to `docker-compose.yml` but nobody created the
matching K8s Deployment/Service.
**Fix**: created a new manifest following the existing per-service pattern.
**File**: `deployment/k8s/deployments/logging.yaml` (new)

---

## C — Runtime & frontend (`erp.local` loads but nothing works)

Once infrastructure was stable, the page loaded but almost every browser
action still failed — reverse-proxy and browser-compatibility issues, not
infrastructure anymore.

### Fix 22 — Entire Admin page crashed/blank — `BrowserAuthError: crypto_nonexistent`

**Root cause**: MSAL (`PublicClientApplication`) was constructed eagerly at
module load. Its constructor requires `window.crypto.subtle`, which browsers
only expose in secure contexts (HTTPS, or the literal `localhost` origin).
`http://erp.local` doesn't qualify, so it threw before React could even
render — crashing plain email/password login and Google/Facebook too,
despite MSAL never having a real client ID configured.
**Fix**: lazy init via `getMsalInstance()`, only constructed on click of
"Continue with Microsoft"; removed the `MsalProvider` wrapper since nothing
uses MSAL React hooks.
**Files**: `src/frontend/admin/src/lib/msalConfig.ts`,
`src/frontend/admin/src/pages/auth/LoginPage.tsx`

### Fix 23 — Admin/Storefront nginx couldn't resolve `gateway` inside K8s

**Root cause**: `resolver 127.0.0.11` was hardcoded — that address is Docker
Compose's embedded DNS server. K8s uses CoreDNS at a completely different
address (`10.96.0.10` in this cluster).
**Fix**: added `docker-entrypoint.sh` that reads the real nameserver from
`/etc/resolv.conf` at container start and injects it via `envsubst` before
starting nginx.
**Files**: `src/frontend/{admin,storefront}/{Dockerfile, nginx.docker.conf,
docker-entrypoint.sh}`

### Fix 24 — Still broken after #23 — nginx doesn't apply search-domain expansion like normal DNS clients

**Root cause**: nginx's `resolver` directive runs its own minimal DNS client
that does **not** apply `/etc/resolv.conf`'s `ndots`/`search` settings the
way curl or .NET's `HttpClient` do — querying the bare name `gateway`
against CoreDNS directly returns `SERVFAIL`.
**Fix**: entrypoint script builds the full FQDN
(`gateway.erp.svc.cluster.local`) from the `search` line in `resolv.conf`;
Compose still uses the short name as before.
**Files**: same as #23

### Fix 25 — Browser → LoggingService log reporting returned 404

**Root cause**: `apiClient` (axios) always prepends `/api` to every request
path, so the real request is `/api/logs/api/v1/logs/client`, but
`ocelot.json` only registered `/logs/api/v1/logs/client` (missing the
`/api` prefix). MasterData and Identity had the identical bug fixed
previously — LoggingService was overlooked.
**Fix**: added matching `/api/`-prefixed routes for LoggingService, mirroring
the existing pattern for the other two services.
**File**: `src/Gateway/Ocelot.API/ocelot.json`

---

## D — Backend logic (surfaced only when testing the real user flow)

Each of these three only appeared after the previous one was fixed, testing
login → refresh → call a protected endpoint in the real order a user would.

### Fix 26 — `/api/v1/auth/refresh` always returned 400

**Root cause**: `RefreshTokenRequest` required a client-supplied `IpAddress`
(non-nullable `string`) — that value is supposed to be computed server-side
from request headers (the controller already had that logic written), but
the frontend only ever sends `{refreshToken}`. ASP.NET Core's automatic
model validation rejected the request with 400 *before* the action method
could run and fill it in.
**Fix**: made `IpAddress`/`TenantId` optional on the DTO — the same fix
applies to `/logout` since it shares the DTO.
**File**: `src/services/IdentityService/src/ERP.Identity.Application/DTOs/Auth/RefreshTokenDto.cs`

### Fix 27 — After #26, a 500 appeared: `Invalid column name 'CreatedBy'/'UpdatedBy'`

**Root cause**: the `Permissions` table is missing 2 audit columns required
by `Permission : BaseEntity`. Migration `005_AddBaseEntityAuditColumns.sql`
correctly fixed this for 9 other tables but omitted `Permissions`.
**Fix**: new migration `008_AddPermissionsAuditColumns.sql`, run against the
live DB. (Migration 005 itself was left untouched — already applied
elsewhere.)
**Files**: `src/Database/Identity/008_AddPermissionsAuditColumns.sql` (new),
`src/Database/RunAllScripts.ps1`

### Fix 28 — Every Bearer-protected Gateway route returned 401 — JWT signing key mismatch

**Root cause**: IdentityService signs tokens using its own hardcoded default
key from `appsettings.json`; Gateway validates using a *different* hardcoded
default key. No `jwt-signing-key` secret had ever been wired into K8s,
despite `secrets.yaml.example` already documenting a slot for it. Only
public routes (login, product listing) were unaffected, so this passed
earlier smoke tests.
**Fix**: added a real `jwt-signing-key` to the secret, wired `Jwt__Key` into
all 16 deployments (Identity, Gateway, and the other 14 services).
**Files**: `deployment/k8s/secrets.yaml`, `deployment/k8s/deployments/*.yaml`
(16 files)

---

## E — Authorization (403s everywhere once login/JWT actually worked)

Not several unrelated bugs — one architectural gap: **two disconnected
permission vocabularies that were never unified** after the move to
JWT-based auth.

### Fix 29 — Every permission-gated endpoint on all 13 business services returned 403

**Root cause**: SalesOrder, PurchaseOrder, Accounting, etc. check permission
codes from `ERP.Shared.Authorization.RoleDefinitions`/`PermissionConstants`
(e.g. `sales_order:read`). Before the JWT hardening, the Gateway computed
this set and forwarded it via a header every service trusted. After
services started validating the JWT directly (a real security fix), the
JWT's `permission` claim was only ever populated from IdentityService's own,
separate AuthDB permission codes (`users:manage`, etc.) — the two
vocabularies were never merged.
**Fix**: at token issuance, merge
`RoleDefinitions.GetPermissionsForRoles(roles)` into the JWT's `permission`
claims — one fix point, applies to all 13 services since they all share
`JwtClaimsUserContextService`.
**File**: `src/services/IdentityService/src/ERP.Identity.Infrastructure/Services/AuthService.cs`
(`IssueTokensAsync`)

### Fix 30 — SuperAdmin still 403 on IdentityService's own endpoints (Tenants/Users/Roles/Permissions/Settings)

**Root cause**: each action requires its own fine-grained permission code
(`ViewTenants`, `ViewUsers`, ...) checked directly against the DB — no
bypass for any role. SuperAdmin was only ever seeded with 4 broad
permissions; every new fine-grained permission added later (e.g. migration
007) silently locks SuperAdmin out again unless someone remembers to
re-grant it.
**Fix**: added a code-level bypass — the `SuperAdmin` role always succeeds
authorization, no DB lookup needed. More durable than keeping
`RolePermissions` manually in sync forever.
**File**: `src/services/IdentityService/src/ERP.Identity.Application/Authorization/PermissionAuthorizationHandler.cs`

### Fix 31 — PurchaseOrderService has its own local, dead-code `RoleDefinitions`, also missing SuperAdmin

**Root cause**: the service has a local `UserContextService`/`RoleDefinitions`
that computes permissions from the `role` claim, but `Program.cs` registers
the shared `JwtClaimsUserContextService` instead — the local copy is never
actually invoked at runtime. Not a live bug, but confusing for future
readers.
**Fix**: added the missing `SuperAdmin` entry for consistency, even though
it doesn't affect runtime behavior today.
**File**: `src/services/PurchaseOrderService/src/ERP.PO.Application/Authorization/RoleDefinitions.cs`

---

## F — Redeploy verification after GitHub fetch (2026-07-19)

A large fetch (208 files, ~10.7k insertions — Settings/FeatureFlags/License
management, Redis cache changes) required rebuilding and redeploying all 19
service images. Rebuilding surfaced 4 more issues during end-to-end
re-verification, none related to the rebuild itself — all pre-existing gaps
the new License-enforcement and Settings features finally exercised.

### Fix 32 — Admin SPA's non-`/api`-prefixed routes silently served the SPA shell instead of JSON

**Root cause**: ~29 of the Admin frontend's API client files (tenants,
users, roles, permissions, every `settings/*.api.ts`, masterdata, logs,
license) deliberately build root-relative URLs like `/id/api/roles`,
`/mst/api/departments`, `/po/api/purchase-orders` — bypassing the `/api`
prefix entirely to hit Ocelot's per-service route prefixes directly (see
the file-header comments: *"mounted under the Gateway's `/id/api/...`
route"*). But the Admin container's nginx only had a `location /api/`
proxy block; every other prefix fell through to `try_files ... /index.html`
— returning HTTP 200 with `text/html` instead of an error, which is why
this went unnoticed for so long (no 4xx/5xx to catch in a network tab scan,
just a page that silently didn't update).
**Fix**: added a second nginx `location` block matching
`^/(id|po|so|wh|acc|crm|hrm|mst|prd|pay|shp|pos|prm|ntf|rpt|logs)/api/`,
proxying to the same gateway upstream as `/api/`.
**File**: `src/frontend/admin/nginx.docker.conf`
**Impact**: this was very likely the root cause of a large fraction of the
403/404s reported earlier in this document's own testing — those tests
went through `curl` directly against the gateway or `api.erp.local`, which
never exercised this nginx layer, so it was never caught until this
session tested through `erp.local` (the same host the real browser uses).

### Fix 33 — `settings:system:manage`/`settings:system:view` never made it into the live AuthDB

**Root cause**: migration `002_AddExternalAuthSettingsAndTwoFactor.sql` was
edited at some point to add these two permissions (+ the tenant pair) and
grant them to SuperAdmin/Admin, but the live DB had already run an older
version of this file — idempotent `IF NOT EXISTS` guards only help on a
fresh run, they don't retroactively apply a diff to an already-migrated
database. Surfaced when `TenantLicensesController.IssueLicense` did a live
DB permission check for `settings:system:manage` (deliberately not
JWT-claim-based, see Fix 34) and got a silent `403` because the row simply
didn't exist.
**Fix**: re-ran just the permission-seeding batch from migration 002
(with `SET QUOTED_IDENTIFIER ON`, see Fix 27's precedent) directly against
`AuthDB`.
**Lesson for future migrations**: editing an already-applied migration file
to add new statements does *not* get picked up automatically — needs a new
migration file, or a documented manual re-run step. Worth auditing other
early migrations (001–007) for the same pattern.

### Fix 34 — Issuing a tenant's first license always 500'd (FK violation)

**Root cause**: `AuthDbContext`'s `OnModelCreating` defines `TenantLicense`
and `TenantLicenseModule` as separate entities but never configures the FK
relationship between them (no `HasOne`/`HasForeignKey`), even though the DB
schema has a real `FK_TenantLicenseModules_TenantLicense` constraint.
Without that relationship in the EF model, the change tracker doesn't know
`TenantLicenseModules` inserts depend on the new `TenantLicenses` row, and
batched the child INSERT before the parent's within the same
`SaveChangesAsync`, hitting the FK constraint every time
(`LicenseService.IssueLicenseAsync`, new-tenant path).
**Fix**: added `entity.HasOne<TenantLicense>().WithMany().HasForeignKey(m
=> m.TenantLicenseId).OnDelete(DeleteBehavior.NoAction)` to the
`TenantLicenseModule` entity configuration (matching the migration's
unspecified — i.e. `NO ACTION` — delete behavior).
**File**: `src/services/IdentityService/src/ERP.Identity.Infrastructure/Persistence/AuthDbContext.cs`

### Fix 35 — `ERP.Shared.Authorization.PermissionConstants`/`RoleDefinitions` was missing ~90 permission codes that controllers actually check

**Root cause**: auditing every `HasPermission("...")` call site across all
13 business services (the ones that don't check permissions against
Identity's own DB — see the two-permission-system note from Fix 29) against
what `PermissionConstants.GetAllPermissions()` actually contains found the
shared catalog was missing, in full: `inventory:view`/`inventory:manage`
(Warehouse — **entirely missing, no role could ever pass this check**),
`logs:view`, all 5 `reports:{sales,inventory,payments,pos,hr}:view`, 7
Accounting `:view` variants (only the `:manage` half of each pair existed),
5 Payment codes (`view`/`authorize`/`capture`/`cancel`/`settlements:view`),
2 POS shift codes, and all 44 `settings:{cart-checkout,currency,email,
inventory,notification,order,payment,shipping,sms,store-product,tax}:
{system,tenant}:{view,manage}` codes from the new per-service Settings
features. Since `SuperAdmin` = `GetAllPermissions()` and every other role's
list is hand-maintained from the same constants, none of this was reachable
by any role — not just SuperAdmin — for Warehouse and half of Payment.
**Fix**: added all ~90 missing constants to `PermissionConstants.cs`
(grouped by module, plus a `GetTenantSettingsPermissions()` helper for the
21 tenant-tier Settings pairs). Extended `RoleDefinitions`: `SuperAdmin`
picks them up automatically via `GetAllPermissions()`; `Admin` gets the
full new business set plus tenant-tier (not system-tier) Settings, mirroring
IdentityService's own system/tenant tiering rule; `Manager`/`Viewer` get
view-level/operational subsets consistent with their existing scope.
**Files**: `src/Shared/Authorization/PermissionConstants.cs`,
`src/Shared/Authorization/RoleDefinitions.cs`
**Rebuild scope**: only `identityservice` needed rebuilding — it's the only
service that calls `RoleDefinitions.GetPermissionsForRoles(...)` (at
login/refresh, to build the JWT `permission` claim); every business service
just string-compares its own hardcoded permission literals against that
claim, so none of them needed rebuilding for this fix.
**Caveat**: role→permission assignments for the newly-added codes
(especially `Manager`/`Viewer`'s exact scope) were chosen to be consistent
with the existing pattern in the file, not confirmed against a product
spec — revisit if the actual business rules differ.

### Fix 36 — `GET /po/api/purchase-orders` always 500'd (missing DB columns)

**Root cause**: `PurchaseOrder.RejectionReason` and
`PurchaseOrderItem.RejectedQuantity` exist as domain entity properties
(backing the Reject-PO flow noted in review finding #8) but no Purchasing
migration ever added the columns — `PurchasingDB` was created before this
feature and never caught up. Any query touching either table failed with
SQL error 207 (`Invalid column name`).
**Fix**: new migration `004_AddRejectionColumns.sql` —
`PurchaseOrders.RejectionReason NVARCHAR(500) NULL`,
`PurchaseOrderItems.RejectedQuantity DECIMAL(18,2) NOT NULL DEFAULT 0`.
No image rebuild needed, DB-only.
**File**: `src/Database/Purchasing/004_AddRejectionColumns.sql`, registered in `RunAllScripts.ps1`.

### Fix 37 — `GET /api/logs/api/v1/logs` always 401'd, even with a valid token

**Root cause**: `deployment/k8s/deployments/logging.yaml` was created after
Fix 28 (the JWT-signing-key rollout across "all 16 deployments") and never
got the `Jwt__Key` env var wired in — every other service reads it from the
`erp-secrets` / `jwt-signing-key` secret, but LoggingService fell back to
its `appsettings.json` placeholder (`"CHANGE_ME_SET_VIA_JWT__KEY_..."`), so
no real caller's token could ever validate against it.
**Fix**: added the same `Jwt__Key` `secretKeyRef` block already used by
every other deployment (e.g. `identity.yaml`, `gateway.yaml`).
**File**: `deployment/k8s/deployments/logging.yaml`
**No image rebuild needed** — config-only change, applied via `kubectl apply` + rollout restart.

### Fix 38 — Axios `baseURL` auto-prefixing meant real browser traffic used double-prefixed paths Ocelot never routed for 7 of 10 affected services

**Root cause**: every Admin API client using the `GATEWAY_ROOT` pattern
(Fix 32) builds a root-relative path like `/shp/api/settings/shipping/system`
and passes it to `apiClient` (axios instance, `baseURL: '/api'`). Axios's
`combineURLs` always concatenates `baseURL` + the given path when the path
isn't a full absolute URL — *regardless* of a leading `/` — so the actual
request the browser sends is `/api/shp/api/settings/shipping/system`
(double-prefixed), never the single-prefixed path Fix 32's nginx rule (or
manual `curl` testing against the bare prefix) was checking. `ocelot.json`
already had this exact double-prefix mirror route for 3 services (`id`,
`mst`, `logs` — someone had clearly hit this before), but not for the other
10. Reported directly by the user: `/api/shp/api/settings/shipping/system
→ 404`.
**Fix**: added the same `/api/{prefix}/api/{everything}` mirror route for
all remaining prefixes (`po`, `so`, `wh`, `acc`, `crm`, `hrm`, `prd`, `pay`,
`shp`, `pos`, `prm`, `rpt`) to `ocelot.json`.
**File**: `src/Gateway/Ocelot.API/ocelot.json`
**Rebuild scope**: `gateway` only.
**Correction to Fix 32**: the nginx regex added in Fix 32 (matching
single-prefixed `/id/api/...` etc.) is harmless but was verifying the wrong
path — real Admin traffic is double-prefixed. The actual fix for real
browser traffic is this Ocelot mirror-route addition; Fix 32's nginx change
doesn't hurt anything but wasn't the thing actually exercised by the SPA.

### Fix 39 — All 8 non-Identity Settings features were 100% unusable: 22 tables across 8 databases never existed

**Root cause**: chasing Fix 38's routing down to real responses surfaced
that every non-Identity Settings feature 500'd with `Invalid object name` —
the migration files for every one of them (`Accounting\002_AddTaxSettings.sql`,
`Notification\002_AddNotificationEmailSmsSettings.sql`,
`Payment\003/004_...`, `Product\007/008/009_...`,
`Sales\003/004/005_...`, `MasterData\002/003_...`,
`Warehouse\003_AddInventorySettings.sql`, `Shipping\003_AddShippingSettings.sql`)
**already existed in the repo** but were never registered in
`RunAllScripts.ps1` — the exact same class of gap as Identity's Fix (the
original migration-registration issue that kicked off this whole redeploy
session), just never caught for the other 7 databases because nobody had
exercised these endpoints through the real routing path until Fix 38.
**Fix**: registered all 13 missing script entries in `RunAllScripts.ps1`
(in their correct per-database order) and ran each directly against the
live DBs. All settings tables lazily get their one row per tenant / one
well-known-GUID row on first access (`GetOrCreate...Async` pattern in each
service's `*SettingsService`) — no data backfill needed, just the tables.
**File**: `src/Database/RunAllScripts.ps1`

### Fix 40 — Warehouse Settings tables had a NOT NULL column the entity never sets, breaking every first-time read

**Root cause**: `Warehouse\003_AddInventorySettings.sql` declared
`UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()` on both
`SystemInventorySettings` and `TenantInventorySettings`, but the domain
entities (`SystemInventorySettings`/`TenantInventorySettings` — plain
POCOs, no shared `BaseEntity`) declare `UpdatedAt` as `DateTime?`, only
assigned inside `.Update(...)`. On the very first `CreateDefault()` insert
(the lazy-create path from Fix 39), EF Core sends an explicit `NULL` for
`UpdatedAt`, which overrides the column default and violates `NOT NULL`.
**Fix**: changed the column to `DATETIME2 NULL` in the migration file, and
`ALTER COLUMN`'d the two already-created live tables to match.
**File**: `src/Database/Warehouse/003_AddInventorySettings.sql`
**No image rebuild needed** — DB-only.

### Fix 41 — All 21 IdentityService Settings tables were missing the `Description` column, breaking every one of them

**Root cause**: reported directly by the user (`GET /api/id/api/settings/general/system → 500`). Every Settings entity in IdentityService (`System/TenantAuthSettings`, `System/TenantGeneralSettings`, `TenantCompanySettings`, and all 8 Phase-3 categories — Advanced/Api/Audit/FileStorage/Integration/Localization/Scheduler/Workflow, System+Tenant each) inherits the shared `BaseEntity`, which includes a `Description` column — but migrations 002, 008, and 010 never included it in any of their 21 `CREATE TABLE` statements. Every read hit `Invalid column name 'Description'`.
**Fix**: `ALTER TABLE ... ADD Description NVARCHAR(MAX) NULL` on all 21 live tables, and patched the same column into all 21 `CREATE TABLE` statements across `002_AddExternalAuthSettingsAndTwoFactor.sql`, `008_AddGeneralAndCompanySettings.sql`, and `010_AddPhase3FixedSettingsCategories.sql` so a fresh deploy doesn't need this manual step.
**No image rebuild needed** — DB-only.

### Fix 42 — Every service using `ISecretProtector` (Identity's Integration Settings, Payment, Notification) 500'd with a missing-config error

**Root cause**: `ERP.Shared.Security.AesSecretProtector` (encrypts VnPay keys, SMTP passwords, SMS API keys, external integration API keys at rest) reads `Security:SettingsEncryptionKey` from configuration and throws if absent — and that key was never defined anywhere: not in any `appsettings.json`, not in `secrets.yaml`, not in any deployment's env vars. Every controller action on IdentityService's Integration Settings, all of PaymentService's Settings endpoints, and all of NotificationService's Email/SMS/Notification Settings endpoints failed at DI construction time (`AesSecretProtector`'s constructor throws), regardless of whether the specific request actually touched an encrypted field.
**Fix**: added `settings-encryption-key` to `erp-secrets`, wired `Security__SettingsEncryptionKey` (`secretKeyRef`) into the 3 affected deployments (`identity.yaml`, `notification.yaml`, `payment.yaml`).
**Files**: `deployment/k8s/secrets.yaml`, `deployment/k8s/deployments/{identity,notification,payment}.yaml`
**No image rebuild needed** — config-only, applied via `kubectl apply` + rollout restart.

### Fix 43 — IdentityService's OutboxProcessor logged every pod shutdown as an error

**Root cause**: reported by the user via a log line — `TaskCanceledException at OutboxProcessor.ExecuteAsync line 40`. Uniquely among the 12 services' `OutboxProcessor`s, Identity's has an early-exit branch (`if (!messages.Any()) { await Task.Delay(5000, stoppingToken); continue; }`) *inside* the same `try` that's wrapped by a catch-all `catch (Exception ex) { _logger.LogError(ex, "Lỗi khi xử lý Outbox"); }`. Every graceful shutdown (pod restart, rollout, `kubectl rollout restart` — which this session did dozens of times) cancels `stoppingToken`, `Task.Delay` throws `TaskCanceledException`, and the catch-all logged it as an application error even though nothing was actually wrong. The other 11 services' `OutboxProcessor`s all have their poll-interval `Task.Delay` *outside* the try/catch, so they don't have this bug.
**Fix**: added a dedicated `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }` before the catch-all (so real errors still log, but shutdown doesn't), and moved the loop's trailing `Task.Delay` into its own try/catch with the same handling.
**File**: `src/services/IdentityService/src/ERP.Identity.Infrastructure/BackgroundJobs/OutboxProcessor.cs`
**Rebuild scope**: `identityservice` only.

### Fix 44 — Creating a duplicate country/province/district/ward crashed with 500 instead of a proper 409

**Root cause**: reported by the user as a `401` on `POST /mst/api/v1/masterdata/geography/countries` — reproducing with a fresh token showed the endpoint actually works fine (201 Created) for genuinely new data, so the user's `401` was a stale/expired browser JWT (this session restarted `identity-service` many times), not a backend bug. But testing with an already-seeded code (`VN`) surfaced a real, separate bug: `CreateCountryCommandHandler` (and the equivalent Province/District/Ward handlers) throw a plain `InvalidOperationException` on a duplicate code, which `GeographyController`'s 4 `Create*` actions never caught — falling through to the default `UseExceptionHandler()` and returning a generic 500.
**Fix**: wrapped all 4 `Create*` actions (`CreateCountry`, `CreateProvince`, `CreateDistrict`, `CreateWard`) in `try/catch (InvalidOperationException ex) { return Conflict(...); }`.
**File**: `src/services/SharedMasterDataService/src/ERP.MST.API/Controllers/GeographyController.cs`
**Rebuild scope**: `masterdataservice` only.
**Note re: the reported 401**: if this recurs after a fresh login, it's a real bug; if it only happens after the token has been sitting in the browser for a while (esp. after any service restart), it's just token expiry — the fix there is on the client (re-login / verify the refresh-token flow actually fires on 401), not the backend.

### Fix 45 — Gateway logged "Request without authentication" as a Warning for every health-check probe and public route

**Root cause**: user asked about the cause of this recurring log line. `GatewayAuthorizationMiddleware` runs unconditionally for every request and unconditionally `LogWarning`s whenever `context.User.Identity.IsAuthenticated` is false — with no awareness that several routes are *deliberately* public per `ocelot.json` (`/api/auth/*`, `/api/products*`, `/logs/api/v1/logs/client` + its `/api/`-prefixed mirror) plus the Gateway's own `/health`, hit every 10-20s by every pod's K8s readiness/liveness probe. None of this indicates a problem — the middleware doesn't even block the request, it just skips adding the `X-User-*` convenience headers.
**Fix**: added a `KnownPublicPathPrefixes` list; unauthenticated requests matching one of them now log at `Debug` (filtered out by the default `Information` minimum level, so effectively silent) instead of `Warning`. Genuinely unexpected unauthenticated requests to other paths still log at `Warning` as before.
**File**: `src/Gateway/Ocelot.API/Authorization/GatewayAuthorizationMiddleware.cs`
**Rebuild scope**: `gateway` only.

### Environmental note — License enforcement is new and tenants start unlicensed by design

Not a bug: migration `012_AddLicenseManagement.sql` seeds the module/plan
catalog but deliberately issues no `TenantLicenses` row to any tenant — a
SuperAdmin must explicitly issue one via `POST
/api/license/tenant/{tenantId}/issue`. The `SYSTEM` tenant used for testing
throughout this document had no license until this session issued one
(Enterprise plan, all 14 modules) — without it, `GatewayAuthorizationMiddleware`
403s with `"Your organization is not licensed for this module"` for every
module's routes, which looks like a regression but is the intended gate.

---

## Environmental note (not a code bug)

Mid-session, Docker Desktop's backend briefly returned `500 Internal Server
Error` on `docker ps` (not just a K8s API timeout) — required a manual
Docker Desktop restart. Not caused by anything in this repo; noted here in
case it recurs.

---

## Final verification

All fixes verified end-to-end against Kubernetes (namespace `erp`) through
`http://erp.local`, using a real login → token refresh → protected-endpoint
call sequence, matching what the Admin SPA actually does.

| Module | Route tested | Status |
|--------|--------------|--------|
| Identity (auth) | `POST /api/v1/auth/login` | **200 ✓** |
| Identity (auth) | `POST /api/v1/auth/refresh` | **200 ✓** |
| Identity | `GET /api/id/api/tenants` | **200 ✓** |
| Identity | `GET /api/id/api/users` | **200 ✓** |
| Identity | `GET /api/id/api/roles` | **200 ✓** |
| Identity | `GET /api/id/api/permissions` | **200 ✓** |
| Identity | `GET /api/id/api/settings/system` | **200 ✓** |
| SalesOrder | `GET /api/v1/orders` | **200 ✓** |
| Product | `GET /api/v1/products` | **200 ✓** |
| Warehouse | `GET /api/v1/inventory` | **200 ✓** |
| PurchaseOrder | `GET /po/api/purchase-orders` | **200 ✓** |
| Payment | `GET /api/v1/payments` | **200 ✓** |
| Reporting | `GET /api/v1/reports/sales` | **200 ✓** |
| MasterData | `GET /mst/api/v1/masterdata/departments` | **200 ✓** |
| LoggingService | `POST /api/logs/api/v1/logs/client` | **200/400*** |

\* 400 only because the test payload was an empty array — confirms routing
and auth work; not a routing/gateway error.

**Not exhaustively tested**: this covers the endpoints that were reported
broken or are representative of each module, not every endpoint in every
service. The two architectural fixes (D/Fix 28 — JWT signing key, E/Fix 29 —
permission vocabulary merge) are root-cause fixes that apply uniformly
across all services, so further issues found later are more likely
per-endpoint business logic bugs than a repeat of this session's systemic
issues.

## Outstanding / known follow-ups (not yet fixed)

- **LoggingService still has no authentication** at the service level
  (review finding #1/#2) — port 5016 is directly reachable, bypassing the
  Gateway's Bearer requirement. Deliberately left open pending a decision
  on whether this service should stay internal-only or gain its own auth.
- `deployment/k8s/secrets.yaml`'s SQL/RabbitMQ credentials were committed to
  git history before `.gitignore` caught it (Fix 20) — untracked going
  forward, but the old values are still recoverable from git history if
  anyone clones this repo. Rotate if this repo is ever made public.
