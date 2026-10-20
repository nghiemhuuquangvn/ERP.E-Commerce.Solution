# Project Status & Handoff

> Read this right after `CLAUDE.md` when picking the work up on another machine.
> Keep it current: update it at the end of every work session (what changed, what's next).
> Last updated: **2026-10-20** (service authorization phase 1).

## 1. Where things stand

| Branch | State | Content |
|---|---|---|
| `master` (origin) | includes PR #1 | Outbox/EventId fixes + IdentityService security hardening (commit `73970b5`), CRM tenant isolation + shared tenant-isolation primitives (PR #1 = `feature/tenant-isolation`) |
| `feature/identity-authorization-tenant-host` | committed locally (6 commits on top of master) — **push it before switching machines** | User/role/permission isolation + "tenant = request host" (see §2) |
| `feature/service-authz-phase1` | branched from the one above; phase 1 of the service authorization plan | Product permissions, explicit authorization everywhere, same-origin hosts, closed service ports (see §2) |

On a fresh clone: `git fetch && git switch feature/identity-authorization-tenant-host` (once pushed).
Verify the baseline first (§5) — everything below was green on 2026-10-20.

## 2. Completed work (newest first)

### 2026-10-20 — Service authorization, phase 1 (`feature/service-authz-phase1`)
Review of all 16 services: `docs/business/Identity/SecurityReview-2026-10.md` ("Service
authorization review"). Done: ProductService permissions (`products:*`, migration
`Identity/023`); `AddErpAuthorization()` FallbackPolicy in every service; explicit
`[AuthorizeAnyAuthenticatedUser]`/`[AllowAnonymous]`; `tests/unit/ERP.Architecture.Tests`
(fails on any action without explicit authorization; 57 legacy in-action-check controllers
allowlisted); coupon/review take the user from the token; templates need a permission; guest
chat history needs its session id; license enforcement in Accounting + SaleOrder; nginx /
ingress host conventions (`<tenant>.erp.local` admin, `<tenant>.shop.erp.local` storefront,
custom domains → storefront) with `/api` and `/hubs/*` same-origin; no published service
ports (`docker-compose.debug.yml`), infra on 127.0.0.1; broker UIs no longer public;
`Tenancy:StorefrontBaseDomain`.

### 2026-10-20 — Identity authorization & tenant-from-host (`feature/identity-authorization-tenant-host`)
Full write-up: `docs/business/Identity/SecurityReview-2026-10.md` ("Follow-up review").
- **Platform vs tenant permissions**: `src/Shared/Authorization/PermissionScopes.cs`
  (`PermissionScopes`, `PlatformTenant`, `PlatformRoles`). Platform permissions only effective
  in the SYSTEM tenant; stripped from every other token.
- **Token permissions** computed only by `Identity.Infrastructure/Security/EffectivePermissions.cs`
  (also `WhyNotGrantableAsync` = anti-escalation for role grants + API key scopes).
- `AuthorizePermissionAttribute`: **role-name "SuperAdmin" bypass removed** (all services).
- Cross-tenant endpoints: `[TenantRouteAccess(...)]` (`Identity.Api/Authorization`) backed by
  `ITenantAccessPolicy` — replaced 13 copy-pasted `CallerCanAccessTenantAsync`.
- **Tenant = host**: `TenantDomain` entity + `ITenantDomainService` (resolve endpoint
  `GET /api/v1/tenants/resolve?host=`, domain CRUD for platform admins); Gateway
  `GatewayAuthorizationMiddleware` + `TenantHostResolver` (404 unknown host, strips client
  `X-Tenant-*`/`X-User-*`, 401 if JWT tenant ≠ host tenant). Auth endpoints read the tenant
  only from the Gateway header; refresh bound to host tenant; `tenant-signup` only on the
  platform host; OIDC tenant = host. Storefront no longer sends a tenant (`VITE_TENANT_CODE` gone).
- Migrations: `src/Database/Identity/021_EnforcePlatformPermissionScope.sql`, `022_AddTenantDomains.sql`.
- Tests: `tests/unit/ERP.Identity.UnitTests/Security/AuthorizationScopeTests.cs`,
  new project `tests/unit/ERP.Gateway.UnitTests`.

### 2026-10-20 — Multi-tenant isolation (PR #1, merged)
- `src/Shared/Infrastructure/Data/{TenantScope.cs,TenantFilterConfiguration.cs}`: per-request
  query filter + `EnforceTenantOnSave`; old constant filter throws.
- CRMService isolated (`Database/CRM/005_BackfillTenantId.sql` → empty TenantId rows to SYSTEM);
  PurchaseOrderService query filter fixed. Pattern documented in CLAUDE.md "Multi-Tenant Isolation".

### 2026-10-19 — IdentityService security review + Outbox (commit `73970b5`)
- Register/external-login always Customer, forgot-password never returns the token (email via
  `PasswordResetRequestedDomainEvent`), token-based email confirmation, IsActive checks,
  trusted-proxy client IP, webhook SSRF policy, `JwtKeyGuard`, secrets removed from appsettings.
- Outbox: `OutboxEventDeserializer`, stable `EventId`, batching, dead-lettering (CLAUDE.md "Outbox rules").

## 3. Deployment prerequisites (don't skip — behaviour depends on them)
1. Run migrations in order: CRM `005`, Identity `021`, `022`, `023`, Notification `008`
   (`deployment/docker/run-migrations.sh` runs every `*.sql` in filename order).
2. `.env`: fresh `JWT_SIGNING_KEY` (`openssl rand -base64 48`) — containers run as Production and
   refuse placeholder/leaked keys (`JwtKeyGuard`). **Rotate** the SA password and JWT key that
   leaked in git history (see SecurityReview §"Required operator action") — not yet confirmed done.
3. Tenancy config: IdentityService `Tenancy:BaseDomain`, `Tenancy:PlatformHost`; Gateway
   `Tenancy:IdentityUrl`. `Tenancy:FallbackTenantCode` is `SYSTEM` only in Development /
   docker-compose (so `http://localhost` works) — **must be empty in real multi-tenant prod**.
4. Only nginx may be publicly reachable (services trust Gateway-set `X-Tenant-*`); docker-compose
   no longer publishes service ports. Hosts: `<tenant>.erp.local` (admin, platform =
   `admin.erp.local`), `<tenant>.shop.erp.local` (storefront), custom domains (storefront);
   locally add them to /etc/hosts or use `localhost` (falls back to SYSTEM).
   Tenants on plans without ACC/SO modules now get 403 from Accounting/SaleOrder.
5. Review the API keys listed by migration 021 and revoke them.

## 4. Backlog (prioritised — from the 2026-10-20 system inventory)

**P0 — security / data integrity**
- Service authorization phases 2–5 (plan in SecurityReview "Service authorization review"):
  2) tenant isolation below; 3) Redis-backed token revocation + authz version checked by every
  service; 4) RS256/JWKS validation so OIDC tokens work and no service can mint tokens;
  5) migrate the 57 allowlisted controllers to attributes, hub ticket-only, `ValidAlgorithms`.
- Roll out tenant isolation (CLAUDE.md pattern) to: Accounting, HRM, POS, Payment, Product,
  Promotion, Reporting, SaleOrder, MasterData, Shipping, Warehouse, Notification, ChatBot.
  Per service: does it stamp TenantId on writes? do consumed events carry TenantId
  (`SalesOrderCreatedEvent` does not)? PurchaseOrder still lacks `EnforceTenantOnSave`.
- ChatBotService guest chat takes tenant from client (`VITE_TENANT_ID`) and bypasses the Gateway.
- NuGet: `System.Security.Cryptography.Pkcs` 9.0.4 high-severity advisory (transitive, every service).
- Identity Phase 2: RS256/JWKS + shared token revocation; OpenIddict signing cert from a secret
  store; invalidate old 2FA challenge on resend & keep OTP out of `OutboxMessages`; Swagger off in
  prod; per-service SQL logins instead of `sa`, `Encrypt=True`; re-auth for 2FA changes; encrypt
  webhook secrets (needs `Security:SettingsEncryptionKey`); Gateway-level rate limiting.

**P1 — broken / unfinished**
- External-login auto-provisioning always fails (`User.Create` rejects empty PasswordHash).
- 2FA email OTP is sent to the *masked* address.
- `ExternalLoginRepository`: 4 methods throw `NotImplementedException`.
- Event flows not wired: SaleOrder ← UserCreated/PromotionCreated; Warehouse ← PurchaseOrderApproved;
  ReceiptScheduled, QuantityRejected, SalesOrderReturned have no consumers; `crm-topic`,
  `notification-topic` unsubscribed; Accounting auto-posting needs COA mapping; Reporting has
  only 5 facts.
- ~25 Settings categories are storage+CRUD only (not applied at runtime) — see
  `docs/business/Settings/SettingsOverview.md`.
- Shipping has two fee mechanisms; Sales credit-limit check before approve; Redis `RemoveByPattern`.

**P2 — missing UI / modules**
- Admin SPA: Promotion, Shipping, POS, Notification templates pages (APIs exist); ~169 hardcoded
  permission strings.
- Storefront: reset-password / confirm-email pages (email links point to them), coupons,
  shipment tracking, account security.
- HRM: 7 of 11 BA sub-modules not built; Finance: closing/aging/COA mapping.

**P3 — quality**
- Tests exist only for Identity, Shared, Gateway (unit) and Product (integration). PurchaseOrder
  test folders have **no .csproj** (never compiled).

## 5. Working environment notes
- Verify a change: build each touched `src/services/<Svc>/*.sln` (+ `src/Gateway/Ocelot.API`,
  `src/Shared/ERP.Shared.csproj` for shared changes), then
  `dotnet test tests/unit/ERP.Identity.UnitTests tests/unit/ERP.Shared.UnitTests tests/unit/ERP.Gateway.UnitTests`.
  Last green run: 17 services + Gateway 0 errors; tests 197 / 19 / 6 passed.
- Repo rule: run the `/verify` skill (Docker-based) before committing code; if Docker isn't
  running, say so and fall back to the build/test commands above.
- `dotnet build ERP.Identity.sln` can take >5 min on a cold machine — run it in the background.
- Shell is zsh on macOS: use `sed -i ''`; avoid `echo ====` (zsh `=` expansion).

## 6. Conventions agreed with the project owner
- Communicate in **Vietnamese**; code, comments, commit messages and docs in English.
- One branch per plan; split commits by plan step; don't push or merge without being asked.
- Non-trivial work: plan first (findings + approach), get approval, then implement + tests + docs.
- Data-ownership decisions (e.g. which tenant orphaned rows belong to) are asked, not assumed.
