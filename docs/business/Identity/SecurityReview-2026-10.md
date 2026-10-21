# IdentityService Security Review — 2026-10-19

Scope: IdentityService (auth flows, tokens, OIDC, webhooks, configuration) plus the shared
JWT validation it relies on. Method: source review + unit tests
(`tests/unit/ERP.Identity.UnitTests/Application/Services/AuthServiceTests.cs`,
`.../Security/SecurityHardeningTests.cs`). No live penetration test was run.

## Required operator action

The values below were committed to git and remain in its history. **Treat them as
compromised and rotate them in every environment that ever used them**:

| Secret | Where it leaked | Action |
|---|---|---|
| SQL Server `sa` password | Identity `appsettings.Development.json` / `appsettings.Docker.json` | Change the password; prefer per-service SQL logins over `sa` |
| JWT signing key `97-prKep…` | 16 services' `appsettings.Development.json`, Gateway `appsettings.json`, Identity `appsettings.Docker.json` | **No longer needed (2026-10-21)** — the shared key was removed; tokens are signed with certificates (`SigningKeys.md`). Delete `JWT_SIGNING_KEY` / the `jwt-signing-key` secret from every environment |

The leaked JWT key is still used as the shared **local Development** key; `JwtKeyGuard`
(ERP.Shared.Authorization) makes Identity, the Gateway and every service using
`AddErpJwtAuthentication` refuse to start with it (or with the `.env.example` /
`CHANGE_ME` placeholders) outside Development. Because docker-compose runs as Production,
`docker compose up` now fails until `.env` has a fresh `JWT_SIGNING_KEY`.

## Findings and status

| ID | Severity | Finding | Status |
|---|---|---|---|
| C1 | Critical | Anonymous `/auth/register` honored client `UserType` → self-registration as Admin/SuperAdmin of any tenant. Same pattern in external-login auto-provisioning (not exploitable today only because auto-provisioning currently always throws — see Known gaps) | **Fixed** — always Customer; validator rejects other values |
| C2 | Critical | `/auth/forgot-password` returned the reset token → account takeover with only email + tenant code | **Fixed** — token only in an emailed link (Outbox → Notification); uniform 200; TTL 60 min |
| C3 | Critical | Real SA password + JWT key committed | **Code fixed**; the JWT key no longer exists (2026-10-21); **SA password rotation still required** (above) |
| H1 | High | `/auth/confirm-email` confirmed any account by UserId alone | **Fixed** — single-use hashed `EmailConfirmToken`, issued on register/tenant signup |
| H2 | High | OIDC `/connect/login` bypassed lockout, attempt counter, rate limit, 2FA, IsActive, audit; open redirect via ReturnUrl; no CSRF protection | **Fixed** — shared checks via `IAuthService.VerifyInteractiveCredentialsAsync`, antiforgery, ReturnUrl limited to `/connect/authorize`, 2FA-required users denied |
| H3 | High | Deactivated users/tenants could still log in and refresh; deactivate / password reset / change didn't end sessions | **Fixed** — `EnsureAccountCanAuthenticateAsync` on every token path; `ISessionService.RevokeAllForUserAsync` |
| H4 | High | Rate limits and brute-force counter keyed on the client-supplied `X-Forwarded-For` | **Fixed** — `UseForwardedHeaders`, trusting only `ForwardedHeaders:KnownNetworks`, `ForwardLimit = 1` |
| H5 | High | Webhook SSRF: any http(s) URL incl. internal hosts / cloud metadata | **Fixed** — https + public host only, checked on save and on resolved IPs at connect time, no redirects |
| M4 | Medium | `AssignRole`/`RemoveRole` looked up users without tenant scope (cross-tenant role grant) | **Fixed** |
| — | Medium | Refresh-token reuse detection "revoked" device sessions read `AsNoTracking` → never persisted | **Fixed** (via `RevokeAllForUserAsync`) |
| M1 | Medium | Symmetric HS256 key shared by every service; token blacklist only checked by Identity | **Fixed** — revocation store (phase 3); RS256 via JWKS (phase 4); `Jwt:Key` removed everywhere (2026-10-21) |
| M2 | Medium | OpenIddict signing cert self-generated in the container's user store (lost on restart, differs per replica) | **Fixed — phase 4** — Key Vault (production) / PFX on a persistent volume (local); see `SigningKeys.md` |
| M3 | Medium | 2FA resend doesn't invalidate the previous challenge; OTP plaintext retained in `OutboxMessages` and Kafka | Open |
| M5 | Medium | Tenant `ADMIN` role has `identity:tenants:view` → can list all tenants | Open |
| M6 | Medium | Swagger enabled in Production; every service connects as `sa` with `Encrypt=False` | Open |
| M7 | Medium | Enabling/disabling 2FA or changing its phone number needs no re-authentication / phone verification | Open |
| — | Medium | Webhook `Secret` stored in plaintext (`ISecretProtector` exists but `Security:SettingsEncryptionKey` isn't configured for Identity, so wiring it now would break webhook CRUD and the outbox) | Open |
| L1–L4 | Low | Enumeration via register 409 / timing; Microsoft external-login issuer config can't validate real tokens; `AuthorizePermission` bypass checks role `SuperAdmin` but the code is `SUPER_ADMIN` (fail-closed) | Open |

## Behavior changes to be aware of

- Admin-portal SSO no longer auto-creates **Admin** accounts — new SSO users become
  Customers. Create staff/admins via `POST /users/invite`; existing linked users are unaffected.
- Webhook endpoints pointing to `http://` or internal hosts stop receiving deliveries
  (logged as skipped) and can't be saved anymore.
- Changing your password or resetting it signs you out everywhere (refresh fails); the
  current access token is rejected by every service as well (phase 3 shared revocation store).
- Reset/confirm links default to `http://localhost:5173/reset-password` and
  `/confirm-email` (`Auth:PasswordResetUrl`, `Auth:EmailConfirmationUrl`). **No frontend
  page handles them yet.**
- Identity's own port (5001) is published to the host in docker-compose; requests
  arriving from the Docker host network are inside `KnownNetworks`, so their
  `X-Forwarded-For` is trusted. In production expose only nginx.

## Known gaps found during the review (not security fixes)

- External-login auto-provisioning always fails: `User.Create` rejects the empty
  `PasswordHash` passed by `FindOrCreateExternalUserAsync`.
- 2FA email codes are sent to the **masked** address (`TwoFactorCodeGeneratedDomainEvent.Destination`),
  so email 2FA can't actually be delivered.

## Platform-wide follow-up: multi-tenant data isolation (2026-10-20)

Outside IdentityService, services did not filter by `TenantId`, and the one shared helper
in use (`TenantFilterConfiguration.ConfigureTenantFilter`, PurchaseOrderService) baked a
constant tenant into EF's cached model — the first request's tenant applied to everyone.

| Service | Status |
|---|---|
| IdentityService | Isolated (explicit tenant predicates in repositories) |
| CRMService | Isolated (PR #1); `AddRequestTenantProvider` since phase 2 |
| PurchaseOrderService | Isolated — save-side enforcement on every overload and `PurchaseRequest` tenant-owned (phase 2) |
| All other 13 services | **Isolated — tenant isolation phase 2** (PR #4–#6, see below) |

The old helper now throws `NotSupportedException` (marked `[Obsolete]`) so it can't be reintroduced.

## Follow-up review: user/role/permission isolation & tenant-from-host (2026-10-20)

Requirement: a user of tenant A must never see or change tenant B's data; each tenant has its
own domain and the tenant is decided by the URL.

| ID | Severity | Finding | Fix |
|---|---|---|---|
| A1 | Critical | Anonymous `tenant-signup` gave the new tenant's ADMIN every `Module = "Identity"` permission — incl. `identity:tenants:*`, `identity:permissions:manage`, `settings:system:*` ⇒ administrator of every tenant | Signup grants tenant-scope permissions only; signup allowed only on the platform host |
| A2 | Critical | Tenant admins could add any permission (incl. platform ones) to their own roles | `WhyNotGrantableAsync`: no platform permission outside the platform tenant, only grant what you hold |
| A3 | Critical | `AuthorizePermission` bypassed every check for a role named "SuperAdmin"; any tenant admin could create one | Bypass removed (all services); reserved role codes rejected; platform super-admin gets the full catalog at token issuance instead |
| A4 | Critical | API key scopes were arbitrary permission claims | Scopes validated like role grants |
| A5 | High | `identity:tenants:view` on tenant ADMIN = list users/roles/tenants of every tenant | Now a platform permission; cross-tenant reads go through `[TenantRouteAccess]` |
| A6 | High | User detail leaked the user's roles/permissions in other tenants; role permissions ignored `Role.TenantId`/`IsActive`; `AssignRole` didn't check the role's tenant | Filtered includes; joins on role tenant + active; domain invariant; DB FK `UserRoles(RoleId, TenantId) → Roles(Id, TenantId)` |
| A7 | High | Gateway didn't bind tokens to the host's tenant; client `X-Tenant-Code` was trusted; register/forgot used body tenant; refresh not host-bound | Gateway tenant-from-host enforcement (404 unknown host, header stripping, 401 on tenant mismatch); Identity reads tenant only from Gateway header; refresh bound to host tenant; OIDC tenant = host |
| A8 | Bug | Assigning a role via API always failed (`User.UserTenants` not loaded) | Repository loads memberships |

Data cleanup: `Database/Identity/021_EnforcePlatformPermissionScope.sql` (removes platform
permissions from every role except the platform SuperAdmin, neutralises look-alike platform
roles, removes cross-tenant role links, adds the composite FK, lists API keys to revoke).
`022_AddTenantDomains.sql` adds custom domains.

Deployment notes:
- Set `Tenancy:BaseDomain` and `Tenancy:PlatformHost` on IdentityService and
  `Tenancy:IdentityUrl` on the Gateway. `Tenancy:FallbackTenantCode` (docker-compose: SYSTEM,
  so `http://localhost` works) must stay empty in a real multi-tenant deployment.
- IdentityService (5001) and other services must not be reachable from outside except through
  the Gateway — they trust the Gateway-set `X-Tenant-*` headers.
- Behaviour change: tenant ADMINs (incl. ADMIN of SYSTEM) lose the Tenants page / tenant
  selector; only the platform SuperAdmin on the platform host keeps them.

Open: ChatBotService guest chat takes its tenant from the client (`VITE_TENANT_ID`) and bypasses
the Gateway (SignalR) — should resolve the tenant from the host like the Gateway does.

## Tenant isolation phase 2 — all services (2026-10-20, PR #4–#6)

Pattern: CLAUDE.md "Multi-Tenant Isolation". Every service DbContext is tenant-scoped
(enforced by `tests/unit/ERP.Architecture.Tests`: architecture, real-model filter coverage,
two-tenant behaviour per service); every cross-service event carries `TenantId`
(`docs/architecture/event-catalog.md` "Tenant in every event").

| Wave | PR | Services |
|---|---|---|
| 2a + 2b | #4 | Shared building blocks; SaleOrder, Payment, Accounting (+ CRM/PO alignment) |
| 2c | #5 | Product (+ Elasticsearch), Warehouse (+ PO `PurchaseRequest`), Shipping, Promotion, POS |
| 2d | #6 | HRM, MasterData (shared reference data), Notification (shared templates), ChatBot (host tenant), Reporting |

Additional defects found and fixed while isolating:

| Area | Defect | Fix |
|---|---|---|
| Sales, Accounting, Product, Warehouse, HRM, MasterData | Business codes unique **globally** (order/quotation numbers, tax codes, SKU, slug, brand/category/warehouse/barcode/serial/transfer/stocktake codes, shift names, country/language/company codes) — with per-tenant numbering two tenants collided on insert | Unique per `(TenantId, …)` (EF + migrations) |
| Sales | `CustomerSalesProfiles`, `CommissionPlans`, `CommissionRecords` had no TenantId (credit limits/commissions shared) | TenantId column + per-tenant profile uniqueness |
| Warehouse | Every `StockMovement` hard-coded to the SYSTEM tenant; 10 tables had no TenantId | Stamped with the current tenant; columns added |
| Shipping | Saga shipments used a hard-coded SYSTEM carrier — other tenants hit "Carrier not found" and a nack/requeue loop | Tenant's own active carrier, or skip with warning |
| Payment | VNPay callback/IPN had no tenant context | After signature verification, the payment's tenant (cross-tenant lookup by transaction ref) |
| Product | Elasticsearch search returned every tenant's products | Documents carry TenantId; every search filters; full re-index queued by migration |
| ChatBot | Guest conversations trusted the client's `GuestTenantId`; storefront sent an all-zero id | Tenant = storefront host (Gateway / `HostTenantHubFilter`); client value only cross-checked |
| Reporting | Facts had no tenant — every dashboard summed all tenants | TenantId on facts/dimensions; consumers write in the event's tenant |
| PurchaseOrder | `SaveChanges` (sync) skipped tenant enforcement; `PurchaseRequest` had no tenant | All overloads enforce; StockAlert carries the tenant |
| Notification | Templates seeded only in SYSTEM would vanish for other tenants | Shared defaults + per-tenant override |

Still open:
- Guest chat **within** one tenant: anyone who knows a conversation id can join/post via the
  hub (no guest-session check on hub methods) — phase 5 (ticket-only hub).
- Direct BI-tool queries on ReportingDB bypass the EF filter — they must filter `TenantId`.
- Runtime verification (migrations, consumers, Elasticsearch re-index) not yet done in
  Docker — only builds and unit/architecture tests.

## Service authorization review — all 16 services (2026-10-20)

Token validation (signature/issuer/audience/lifetime, `MapInboundClaims=false`, claims-only
user context) is correct in every service. Authorization and isolation were not.

| ID | Severity | Finding | Status |
|---|---|---|---|
| S1 | Critical | Tenant data isolation missing in 13 services (only Identity, CRM, PurchaseOrder isolated) — by-id reads/writes cross tenants | **Fixed — phase 2** (PR #4–#6, see "Tenant isolation phase 2") |
| S2 | Critical | ProductService catalog endpoints (~45) required only a signed-in user — storefront Customers could edit products, prices, images, import/export, reindex | **Fixed** — `products:view/create/edit/delete/manage` (`023_SeedProductCatalogPermissions.sql`) |
| S3 | Critical | Hosts didn't match tenant-from-host: `api.erp.local` / `shop.erp.local` / `erp.local` hosts in nginx & ingress, hubs on separate hosts | **Fixed** — `<tenant>.erp.local` (admin), `<tenant>.shop.erp.local` / custom domain (storefront); `/api` + `/hubs/*` same-origin via SPA containers |
| S4 | Critical | All 17 service ports published by docker-compose (bypass Gateway; Identity trusts X-Tenant-Code) | **Fixed** — ports removed (`docker-compose.debug.yml` for local), infra bound to 127.0.0.1 |
| S5 | High | Revoked tokens (logout/lock/password/role change) still accepted by other services until expiry | **Fixed — phase 3** (shared Redis revocation store checked by every service, the Gateway and hubs; tokens carry `iat`; admin lock ends sessions) |
| S6 | High | Every service held the token signing key (HS256) and could mint tokens; OIDC (RS256) tokens rejected by all services | **Fixed** — RS256 signed only by Identity, validated via JWKS by every service and the Gateway (which now also checks issuer/audience); the shared `Jwt:Key` no longer exists (2026-10-21). OIDC (`urn:`) tokens deliberately still Identity-only (client_credentials permissions bypass `EffectivePermissions`) |
| S12 | High | Service-to-service calls had no identity: SaleOrder → Warehouse/Promotion used anonymous endpoints; since phase 2 they carried no tenant (stock check answered 404, and compose pointed SaleOrder at localhost — every order was refused) | **Fixed (2026-10-21)** — `/internal` endpoints accept only short-lived RS256 tokens signed with the calling service's own key, carrying the tenant; each caller's key validates only its own tokens; compose/k8s URLs fixed |
| S7 | High | `redeem-coupon` used body UserId; product review used body CustomerId; notification templates anonymous; guest chat history readable by anyone signed in | **Fixed** |
| S8 | High | Accounting / SaleOrder ran without `LicenseEnforcementMiddleware` | **Fixed** (tenants without ACC/SO modules now get 403) |
| S9 | High | RabbitMQ management and kafka-ui published via nginx/ingress | **Fixed** — removed; reach via 127.0.0.1 / port-forward |
| S10 | Medium | Three authorization styles; nothing caught a missing check | **Fixed (guard)** — FallbackPolicy + `EndpointAuthorizationTests`; 57 legacy in-action controllers allowlisted (phase 5) |
| S11 | Medium | Hub accepts raw JWT in query string; `HUBAUTH2` logs at Error | Partly fixed — `HUBAUTH2` diagnostics removed (phase 3); ticket-only hubs: phase 5 |
| — | Note | WareHouseService does check `inventory:*` in every action (initial scan misread) | — |
