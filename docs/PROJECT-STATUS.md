# Project Status & Handoff

> Read this right after `CLAUDE.md` when picking the work up on another machine.
> Keep it current: update it at the end of every work session (what changed, what's next).
> Last updated: **2026-10-20** (service authorization phase 4 — RS256/JWKS, release 1).

## 0. Start here (new machine / new developer)

1. `git fetch && git switch master && git pull` — all finished work is on `master` (§1).
   Branches not merged yet are listed in §1; never start from them unless told to.
2. Read, in order: `CLAUDE.md` (rules — especially "Multi-Tenant Isolation", "API
   Authorization", "Outbox rules"), this file (§7 decisions, §8 next step), then the service
   README / `docs/business/...` of whatever you touch.
3. Verify the baseline (§5). If anything is red before you change code, stop and report it.
4. Work the agreed way (§6): plan → owner approval → implement + tests + docs, one branch per
   plan, one commit per step, never push/merge without being asked. Update this file at the
   end of the session.
5. Before changing a DbContext, a consumer, a background job or an event: re-read CLAUDE.md
   "Multi-Tenant Isolation" — `tests/unit/ERP.Architecture.Tests` fails the build if a
   tenant-owned entity is not filtered, and two-tenant tests exist for every service.

## 1. Where things stand

| Branch | State | Content |
|---|---|---|
| `master` (origin) | includes PR #1–#8 | Outbox/EventId fixes + Identity hardening (`73970b5`); CRM tenant isolation (PR #1); user/role/permission isolation + tenant = request host (PR #2); service authorization phase 1 (PR #3); tenant isolation phase 2 for all services (PR #4 Shared+Sales/Payment/Accounting, PR #5 supply chain, PR #6 cross-cutting); handoff docs (PR #7); token revocation, phase 3 (PR #8) |
| `feature/rs256-jwks` | **not merged yet** — service authorization phase 4, release 1 (push/PR when the owner asks) | RS256 signing in Identity (Key Vault / file certificates), JWKS validation in every service and the Gateway, infra, docs (see §2) |

All feature branches above are merged; start new work from `origin/master`.
Verify the baseline first (§5) — everything below was green on 2026-10-20.

## 2. Completed work (newest first)

### 2026-10-20 — Service authorization phase 4, release 1: RS256 / JWKS (`feature/rs256-jwks`)
Only IdentityService can sign access tokens now: RS256 with a certificate from Azure Key Vault
(production) or a PFX on a persistent volume (local/Compose) — `SigningKeys:Source`; the
X509Store dev certificate (new key on every container re-creation) is gone. Every service and the
Gateway validate through `ERP.Shared.Authorization.ErpJwtBearerOptions` with the public keys from
Identity's `/.well-known/jwks.json` (`Jwt:Authority`), only `RS256` (+ `HS256` while `Jwt:Key` is
still configured), exact issuer/audience (the Gateway skipped both before). Outside Identity only
login tokens (issuer `ERP.Identity`) are accepted — OpenIddict `urn:` tokens stay Identity-only.
Identity `Jwt:AccessTokenSigning` (`Hmac`/`Rsa`) makes a rolling k8s cutover 401-free. Also fixed:
LoggingService had no Jwt settings in docker-compose (would not start outside Development).
Runbook (cutover, rotation, emergency): `docs/business/Identity/SigningKeys.md`.

### 2026-10-20 — Service authorization phase 3: token revocation (PR #8)
Logout, account lock/deactivation, password change/reset, role assign/remove, role permission
edits, tenant deactivation and license changes now take effect in EVERY service at once (was:
only in IdentityService; elsewhere the old JWT worked for up to 60 min). Shared
`ERP.Shared.Authorization.TokenRevocation` (Redis, prefix `erp:authz:`): revoked `jti` + "user /
tenant changed at" markers; `AddErpJwtAuthentication` rejects a revoked jti or a token whose `iat`
is older than the marker (5 s local cache, fail-open when Redis is down); Gateway checks too;
hubs re-check on every call (`RevocationHubFilter`). Identity: tokens carry `iat`;
`IUnitOfWork.Revocations` publishes after commit; admin lock now ends sessions and blocks refresh
(failed-login lockout does not). Clients get new permissions via their normal refresh-on-401.

### 2026-10-20 — Tenant isolation, phase 2 (PR #4–#6)
Every service DbContext is tenant-scoped (CLAUDE.md "Multi-Tenant Isolation" — read it before
touching any DbContext, consumer or background job). Shared: `ITenantOwned`,
`IPlatformWideEntity`, `ISharedReferenceData`, `AddRequestTenantProvider` (JWT → Gateway host
tenant for anonymous calls), `ITenantEvent` + `EventTenant`, `TenantHostResolver` (moved from
the Gateway). Every cross-service event carries `TenantId`; consumers skip events without one.
Cross-tenant jobs run per tenant. Business codes are unique per tenant. Product search filters
Elasticsearch by tenant; ChatBot guests get the storefront host's tenant (`HostTenantHubFilter`);
MasterData reference data and Notification templates are shared SYSTEM defaults + tenant rows.
Defects found on the way (hard-coded SYSTEM tenant in stock movements and shipping carrier,
globally unique codes, Reporting summing all tenants, …): SecurityReview "Tenant isolation
phase 2". Tests: `tests/unit/ERP.Architecture.Tests` (architecture, EF filter coverage,
two-tenant behaviour per service).

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
6. **Tenant isolation migrations (phase 2)** — run BEFORE deploying the new services:
   Sales `011`, Payment `006`, Accounting `006`, Product `015`, Warehouse `007`, Purchasing
   `009`, Shipping `005`, PromotionService `003`, POS `005`, HRM `005`, MasterData `007`,
   Notification `009`, ChatBot `002`, Reporting `006` (all `*_TenantIsolation*.sql`). They add
   missing TenantId columns, make codes unique per tenant and assign empty-tenant rows to SYSTEM.
7. **Deploy publishers and consumers together.** Consumers now skip events without `TenantId`;
   events already sitting in an Outbox from before the release have none — let Outboxes drain
   before upgrading, or accept that those events are skipped (logged as warnings).
8. **Elasticsearch re-index:** Product `015` queues every product; product search returns
   nothing for a tenant until `SearchIndexProcessor` has re-indexed its products (documents now
   carry `TenantId`; the filter uses `tenantId.keyword` from dynamic mapping).
9. ChatBot needs `Tenancy:IdentityUrl` (default `http://identityservice:5001`, same as the
   Gateway) to resolve guest/hub tenants from the host. The storefront no longer sends
   `VITE_TENANT_ID`.
10. Notification templates in the SYSTEM tenant are shared defaults: tenants can't edit them,
    they create their own template with the same code to override. ReportingDB: direct BI-tool
    queries must filter on `TenantId`.
11. **Redis is now part of authorization**: every service, the Gateway and Identity need
    `ConnectionStrings:Redis` (shared store `erp:authz:*`). Without it revocation only works inside
    one process (warning at startup); if Redis is down tokens are accepted (fail-open, logged).
    Tokens issued before this release have no `iat`: until they expire (≤ 60 min) only their `jti`
    is checked.
12. **RS256 signing (phase 4)** — follow `docs/business/Identity/SigningKeys.md`:
    - every service + Gateway: `Jwt:Authority` (internal Identity URL) and, for in-cluster http,
      `Jwt:RequireHttpsMetadata=false` (in the k8s configmap / compose). Without it services can't
      fetch the JWKS and answer 401;
    - Identity: `SigningKeys:Source` is **required** (start-up fails without it). k8s: Key Vault
      certificate `identity-token-signing`, Workload Identity client id in the `identity-service`
      ServiceAccount, vault permissions certificates/list + secrets/get. Compose: PFX auto-created
      on the `identity_signing` volume;
    - k8s cutover: deploy with `Jwt__AccessTokenSigning=Hmac`, switch to `Rsa` once every pod runs
      this release; keep `Jwt__Key` everywhere until release 2.

## 4. Backlog (prioritised — from the 2026-10-20 system inventory)

**P0 — security / data integrity**
- Service authorization phase 4 **release 2** (§8) — remove the shared HS256 key outside
  Identity once release 1 runs with `Rsa`. Later: accept OpenIddict (`urn:`) tokens in services
  after client_credentials permissions go through `EffectivePermissions`.
- Service authorization phase 5 (plan in SecurityReview "Service authorization review"):
  migrate the 57 allowlisted controllers to attributes, hub ticket-only (also closes guest chat
  within one tenant: hub methods don't check the guest session), `ValidAlgorithms`.
- NuGet: `System.Security.Cryptography.Pkcs` 9.0.4 high-severity advisory (transitive, every service).
- Identity Phase 2: invalidate old 2FA challenge on resend & keep OTP out of `OutboxMessages`; Swagger off in
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
- Tests exist only for Identity, Shared, Gateway, Architecture (incl. two-tenant tests for every
  service DbContext) and Product (integration, no .csproj) PurchaseOrder
  test folders have **no .csproj** (never compiled).

## 5. Working environment notes
- Verify a change: build each touched `src/services/<Svc>/*.sln` (+ `src/Gateway/Ocelot.API`,
  `src/Shared/ERP.Shared.csproj` for shared changes), then
  `dotnet test tests/unit/ERP.Identity.UnitTests tests/unit/ERP.Shared.UnitTests tests/unit/ERP.Gateway.UnitTests tests/unit/ERP.Architecture.Tests`.
  Last green run (2026-10-20, phase 4 release 1): all APIs + Gateway build with 0 errors; Shared 62,
  Identity 218, Gateway 10, Architecture 46 passed. Docker runtime not verified yet — first run
  must check: Identity log "Generated a new self-signed token signing certificate", login token
  header `RS256` with a `kid` listed at `/.well-known/jwks.json`, a call through the Gateway to
  e.g. SaleOrder answers 200, and after logout 401. The Key Vault source is untested (no Azure).
- Repo rule: run the `/verify` skill (Docker-based) before committing code; if Docker isn't
  running, say so and fall back to the build/test commands above.
- `dotnet build ERP.Identity.sln` can take >5 min on a cold machine — run it in the background.
- Shell is zsh on macOS: use `sed -i ''`; avoid `echo ====` (zsh `=` expansion).
- `dotnet test tests/unit/ERP.Architecture.Tests` builds every API project — 3–10 min; run it in
  the background. It is the safety net for authorization and tenant isolation.
- Docker was not running on the development machine during phases 1–2: migrations, consumers,
  nginx host routing and the Elasticsearch re-index were verified by build + unit/architecture
  tests only. The first Docker run should follow §3 and check service logs for
  `without TenantId ignored` warnings and `TenantIsolationViolationException`.
- New tenant-owned entity / service checklist = CLAUDE.md "Multi-Tenant Isolation" + add a
  two-tenant case to `TenantIsolationBehaviorTests`; new migration = next number in
  `src/Database/<Svc>/`, idempotent (`IF NOT EXISTS` / `COL_LENGTH`).

## 6. Conventions agreed with the project owner
- Communicate in **Vietnamese**; code, comments, commit messages and docs in English.
- One branch per plan; split commits by plan step; don't push or merge without being asked.
- Non-trivial work: plan first (findings + approach), get approval, then implement + tests + docs.
- Data-ownership decisions (e.g. which tenant orphaned rows belong to) are asked, not assumed.

## 7. Decision log (do not re-litigate without the owner)

| Date | Decision | Where it shows up |
|---|---|---|
| 2026-10-20 | Rows with an empty TenantId belong to the **SYSTEM** tenant (`…0001`) | Every `*_TenantIsolation*.sql` / CRM `005` |
| 2026-10-20 | **Tenant = request host**: subdomain `<code>.erp.local` / `<code>.shop.erp.local` **and** custom domains (`TenantDomains`); platform admin = SYSTEM tenant's own domain (`admin.erp.local`) | Identity `ITenantDomainService`, Gateway, nginx/ingress |
| 2026-10-20 | Platform permissions only in the SYSTEM tenant; no authority from role names | `PermissionScopes`, `EffectivePermissions` |
| 2026-10-20 | Two host families (admin vs storefront); API and hubs same-origin on the tenant host; no service ports published | nginx, ingress, docker-compose |
| 2026-10-20 | Service authz in phases; phase 1 = explicit authorization everywhere + infra; phases 2–5 one at a time | SecurityReview "Service authorization review" |
| 2026-10-20 | **Tenant travels in the event payload** (`ITenantEvent`), not in message headers | event-catalog "Tenant in every event" |
| 2026-10-20 | MasterData reference data = **shared SYSTEM rows + tenant additions** (only platform edits shared rows); same model reused for Notification templates | `ISharedReferenceData` |
| 2026-10-20 | Consumers **skip** events without a tenant (fail closed), never guess | all consumers |
| 2026-10-20 | Guest (anonymous) tenant = host's tenant; client-sent tenant ids are only cross-checked | ChatBot, Product catalog, POS customer display |
| 2026-10-20 | Business codes unique per tenant | migrations phase 2 |
| 2026-10-20 | Token revocation: Redis down → **fail-open** (accept + warn); role permission edit → only the role's holders refresh; **admin lock ends sessions** (failed-login lockout does not); hubs re-check on **every call** | `ERP.Shared.Authorization.TokenRevocation`, Identity `IUnitOfWork.Revocations` |
| 2026-10-20 | Token signing: certificate in **Azure Key Vault** with rotation (newest signs, previous versions stay in the JWKS); HS256 → RS256 in **two releases**; the **Gateway keeps validating** (same code as services); services accept **only login tokens** (issuer `ERP.Identity`), OpenIddict `urn:` tokens stay Identity-only for now | `docs/business/Identity/SigningKeys.md`, `ErpJwtBearerOptions` |

## 8. Next step: phase 4 release 2 — remove the shared HS256 key (NOT started; only after release 1 is deployed)

Precondition: `feature/rs256-jwks` merged and deployed, Identity running with
`Jwt__AccessTokenSigning=Rsa` for at least `Jwt:Expired` minutes (60). Then, on a new branch:
- `ErpJwtBearerOptions`: drop the HS256 path (`Jwt:Key`) — RS256 only; `JwtKeyGuard` only in Identity.
- Identity: drop `Hmac` mode and the symmetric key from its own bearer scheme (keep `Jwt:Key`
  only as OpenIddict's encryption key — or move that to its own setting).
- Remove `Jwt__Key` from every service/Gateway in docker-compose and `deployment/k8s/deployments/*`,
  and the `Jwt:Key` placeholders from their `appsettings*.json` (a leftover placeholder would trip
  nothing after the code change, but keeps the secret's footprint wide).
- Docs: SigningKeys.md (cutover done), SecurityReview S6/M1 → fully fixed, this file.
Plan + owner approval first, as usual. Phase 5 (attribute migration of 57 controllers,
ticket-only hubs, guest chat sessions) follows; open item: OpenIddict `urn:` tokens in services.
