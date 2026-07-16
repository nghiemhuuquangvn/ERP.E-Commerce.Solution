# License Management

**Service:** IdentityService (Port 5001)
**Database:** AuthDB

## Purpose

Controls which of the 14 licensable backend modules (every service except
IdentityService itself, which is the platform) a tenant is entitled to use.
Distinct from the 22-category Settings module (see
`docs/business/Settings/SettingsOverview.md`): a Settings category is a tenant
admin's own preference, self-editable; a license is a commercially-controlled
entitlement that only a SuperAdmin can issue, renew, suspend, or revoke — a
tenant admin can only view their own.

## Data model

- **`ModuleDefinition`** — system catalog of the 14 module codes (`SO`, `PO`,
  `WH`, `ACC`, `CRM`, `HRM`, `PRD`, `PAY`, `SHP`, `POS`, `PRM`, `NTF`, `RPT`,
  `MST` — see `ERP.Shared.Authorization.ModuleCodes`, the single canonical
  source every service references). Rarely changes; only when a new
  microservice ships.
- **`LicensePlan`** + **`LicensePlanModule`** — SuperAdmin-defined reusable
  templates (seeded: `STARTER`, `PROFESSIONAL`, `ENTERPRISE`), each with a
  default module set and an optional `MaxUsers` ceiling. A plan is a
  *starting point* for issuing a license, not a live link — editing a plan
  later does not retroactively change tenants already issued against it.
- **`TenantLicense`** + **`TenantLicenseModule`** — the actual issued record,
  one per tenant (unique on `TenantId`). Holds its own module set (a
  snapshot, independently editable), `MaxUsers` override, `LicenseKey`
  (display/audit string only — see below), `StartsAt`/`ExpiresAt`
  (`ExpiresAt = null` means perpetual), and `Status`
  (`Active`/`Suspended`/`Revoked`). `IsCurrentlyValid` is **not** a stored
  flag — it's computed live from `Status`, `StartsAt`, and `ExpiresAt` vs.
  `DateTime.UtcNow`, so a license doesn't need a background job to "expire";
  it simply stops being valid once read after its `ExpiresAt` passes.

## How entitlement is enforced

Enforcement is **JWT-claim-based**, deliberately not a real-time per-request
lookup — this was a considered trade-off (see `docs/business/Settings/`'s
existing precedent for the `permission` claim, which uses the same model):

1. At login/token-refresh, `AuthService.IssueTokensAsync` calls
   `ILicenseService.GetEffectiveModulesAsync(tenantId)`, which resolves the
   tenant's current `TenantLicense` (respecting `IsCurrentlyValid`) into a
   module-code list, filtered against `ModuleDefinition.IsActive` — a module
   deactivated in the catalog stops being granted even to tenants whose
   `TenantLicenseModule` row for it still exists, so the catalog-level
   kill-switch actually takes effect instead of being a no-op for existing
   holders.
2. `TokenService.GenerateAccessToken` embeds each code as a repeated
   `licensed_module` JWT claim — the same pattern already used for
   `permission` claims.
3. Every other service's `JwtClaimsUserContextService` reads these back into
   `UserContext.LicensedModules`, exposing `UserContext.HasModule(code)`.
4. `LicenseEnforcementMiddleware` (shared, `src/Shared/Authorization/`) runs
   right after the JWT/UserContext-populating middleware in each of the 13
   non-Identity services' pipelines, and rejects with 403 if the caller's
   tenant doesn't hold that service's module code. Each service registers
   its own identity via a `ModuleIdentity` singleton
   (`builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Warehouse))`,
   etc.). AccountingService and SaleOrderService — which run their own local
   `UserContextHeaderMiddleware` copies rather than the shared one — have
   the equivalent check inlined directly into those local files instead. The
   gate condition checks `context.User.Identity.IsAuthenticated` (the real
   ASP.NET Core authentication result) rather than the separately-populated
   `UserContext.IsAuthenticated`, so the check fails closed even if a
   service's `UserContext`-population step is ever missing or throws.
5. `[AllowAnonymous]` endpoints (health checks, login, etc.) are exempt, same
   convention as `[Authorize]`. IdentityService itself never runs this
   middleware — it must stay reachable regardless of any tenant's license
   state, so a SuperAdmin can always fix a suspended tenant.

**Fixed 2026-07-16:** the initial rollout had two opposite defects, found in a
follow-up security review before this feature shipped. (1) `UserContext`'s
`LicensedModules` field was never copied into the request-scoped instance by
`UserContextHeaderMiddleware` (or the two local copies), so `HasModule()`
always returned `false` — every licensed tenant was rejected everywhere. (2)
WareHouseService, ProductService, and CRMService ran
`LicenseEnforcementMiddleware` without ever running `UserContextHeaderMiddleware`
ahead of it, so their scoped `UserContext` stayed blank/unauthenticated and the
middleware's own gate condition treated every caller as anonymous — a full
bypass in those 3 services, license or not. Both are fixed: the missing copy
line was added everywhere, `UserContextHeaderMiddleware` was added to those 3
services' pipelines, and point 4's fail-closed gate change above was added as
a structural safeguard against this class of bug recurring.

**Consequence of this design**: revoking or suspending a tenant's license
takes effect for any *newly issued* token immediately, but a token already in
a caller's hands remains valid — with its stale `licensed_module` claims —
until it naturally expires or is refreshed. This bounded staleness (same
window as every other JWT claim in this system) was chosen over adding a
real-time cross-service lookup, to avoid introducing new synchronous
inter-service calls into a codebase that otherwise relies on JWT claims and
async eventing.

## Issuance

Admin-issued and DB-driven — there is no self-service tenant upgrade/billing
flow (the system has no subscription/payment integration for this today).
SuperAdmin picks a tenant and a `LicensePlan` (which pre-fills the module set
and `MaxUsers`), optionally customizes the module list, sets
`StartsAt`/`ExpiresAt`, and issues. A cosmetic `LicenseKey`
(`ERP-XXXX-XXXX-XXXX`) is generated for display/support/audit purposes only —
it is **not** the enforcement mechanism; the `TenantLicense` DB row is the
sole source of truth.

Issuing/renewing against a `PlanId` validates the plan exists and is
`IsActive` — a deactivated or nonexistent plan is rejected with a clean error
rather than silently copying a retired plan's module set or failing later at
`SaveChangesAsync` via the FK constraint. Renewing also requires `ExpiresAt`
(when provided) to be in the future — Issue already had this check; Renew was
missing it until the same review that found the enforcement bugs above.

## API

All routes under `IdentityService`, gated by the `license:*` permission
family (seeded in `src/Database/Identity/012_AddLicenseManagement.sql`;
SuperAdmin holds all 5, tenant Admin holds only `license:tenant:view`):

| Route | Permission | Purpose |
|---|---|---|
| `GET /api/license/modules` | any authenticated caller | List the 14-module catalog |
| `PUT /api/license/modules/{moduleCode}` | `license:modules:manage` | Edit a module's name/active flag |
| `GET /api/license/plans` | `license:plans:view` | List plan catalog |
| `PUT /api/license/plans/{planCode}` | `license:plans:manage` | Create/edit a plan + its module set |
| `GET /api/license/tenant/{tenantId}` | `license:tenant:view` | View a tenant's current license (own tenant, or any with `settings:system:manage`) |
| `POST /api/license/tenant/{tenantId}/issue` | `license:tenant:issue` | Issue a new/replacement license |
| `POST /api/license/tenant/{tenantId}/renew` | `license:tenant:issue` | Extend expiry / change modules / max users |
| `POST /api/license/tenant/{tenantId}/suspend` | `license:tenant:issue` | Temporarily invalidate |
| `POST /api/license/tenant/{tenantId}/revoke` | `license:tenant:issue` | Permanently invalidate |

`license:tenant:issue` is intentionally granted to SuperAdmin only in the
seed data — unlike every other Settings category, a tenant Admin cannot
self-service their own license.

## Eventing

Every issue/renew/suspend/revoke publishes a
`TenantLicenseChangedIntegrationEvent` (see
`docs/architecture/event-catalog.md`) via IdentityService's existing Outbox
pipeline on `identity-topic`. No consumer is wired yet — intended for
NotificationService (alert the tenant admin) and ReportingService
(analytics), following the same "publish now, add narrow consumers later"
precedent already used for `SettingsChangedIntegrationEvent`. This event is
**not** part of the enforcement path — enforcement never depends on it.

## Admin UI

`src/frontend/admin/src/pages/settings/LicensePage.tsx` — three tabs
(Modules, Plans, Tenant License), grouped in the nav alongside the other
Settings pages. The Tenant License tab differs from every other Settings
tenant-tab: SuperAdmins get a tenant picker plus Issue/Renew/Suspend/Revoke
actions; ordinary tenant admins get a read-only summary of their own
tenant's license only.

## Out of scope (this pass)

- No self-service tenant plan upgrade/billing integration.
- No Gateway-level fast-fail enforcement — each service independently checks
  its own module entitlement, consistent with the zero-trust precedent
  already established for JWT authentication itself (services must not
  assume the Gateway is the only path in).
- No finer-than-module granularity — the existing FeatureFlags category
  covers per-feature toggles if ever needed.
- No offline/signed license-key validation — `LicenseKey` is display-only.
