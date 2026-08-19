# Roadmap: IdentityService as a Reusable "Common" Identity Service

**Service:** IdentityService (Port 5001)

## Purpose

Tracks what's needed to make IdentityService reusable as a standalone identity/auth
service for future, unrelated projects — not just this ERP. A full capability audit
(2026-08) found it already has most of what a real identity service needs: JWT
login/refresh/logout, RBAC (roles/permissions), multi-tenancy, 2FA (SMS/Email), SSO
(Google/Microsoft/Facebook/Twitter), API key CRUD, a queryable security audit log,
rate limiting, account lockout, health checks, and self-service tenant provisioning
(see [MultiTenantLogin.md](MultiTenantLogin.md) for the tenant-resolution redesign
and the `POST /api/auth/tenant-signup` endpoint added alongside this roadmap). This
document tracks what's still missing, prioritized, for future sessions.

**Status (2026-08):** All seven roadmap items are done. §1 (OAuth2/OIDC, all four
phases A0-A3), §2 (API key enforcement), §3 (webhooks), §4 (Tenant admin UI), §5
(localization infrastructure + representative coverage), §6 (password-policy
duplication), and §7 (packaging — `ERP.Shared` restructured into per-concern
projects, plus a working `export-identity-service.ps1` tool, verified with both an
isolated build and a full Docker/SQL Server runtime pass — the latter caught and
fixed two real pre-existing migration bugs) are all complete. Remaining work is
scoped, smaller follow-ups documented inline in each section below (e.g. a real
OAuth client-management API, production signing-certificate provisioning) — none of
it blocks calling this service "reusable."

## 1. OAuth2/OIDC provider ✅ Done (all phases A0-A3), verified live end-to-end

**Why**: today the only way to authenticate is this service's own proprietary JWT
REST API (`/api/auth/login` + `/api/auth/refresh`). A third-party app in a different
tech stack (not .NET, doesn't know this API's exact shape) has no standard way to
federate against it. Standing up a real OAuth2/Authorization-Server + OIDC layer is
what makes this genuinely pluggable into an unrelated project the way
Auth0/Keycloak/Okta are.

**Approach**: adopt **OpenIddict** 7.6.0 (`OpenIddict.Server`/`.Server.AspNetCore` +
`OpenIddict.EntityFrameworkCore`) layered on top of the existing `User`/`AuthTenant`
model — do **not** hand-roll OAuth2/OIDC; it's a security-critical protocol with
well-known implementation pitfalls (PKCE downgrade, redirect URI validation, token
replay) that a mature library already handles correctly and keeps patched.

### Phase A0 — package/schema foundation ✅ Done

`OpenIddict.EntityFrameworkCore`/`OpenIddict.Server` 7.6.0 added to
`ERP.Identity.Infrastructure`, `OpenIddict.Server.AspNetCore` 7.6.0 to
`ERP.Identity.Api` (EF Core pins bumped to `10.0.10` to satisfy OpenIddict's floor).
Schema hand-transcribed into `src/Database/Identity/020_AddOpenIddictSchema.sql`
(`Guid`-keyed via `modelBuilder.UseOpenIddict<Guid>()` in `AuthDbContext`, matching
every other table's PK type) rather than generated via `dotnet ef migrations add` —
this repo doesn't use EF Core Migrations. Verified idempotent against the live AuthDB
(applied twice, no errors) and confirmed `/health` boots clean.

### Phase A1 — `client_credentials` flow ✅ Done, verified live end-to-end

`ERP.Identity.Infrastructure/OpenIddict/ClientCredentialsClaimsHandler.cs` (a scoped
`IOpenIddictServerHandler<HandleTokenRequestContext>`) mints `tenant_id` + one
`"permission"` claim per entry in the client's custom `Properties["permissions"]`
(comma-separated — OpenIddict's own scope/permission system governs which OAuth2
flows a client may use, kept separate from this platform's RBAC permission codes),
mirroring `ApiKeyAuthenticationHandler`'s claim shape. Signs with the same symmetric
`Jwt:Key` `TokenService` already uses (`Program.cs`'s `.AddServer(...)`), so tokens
validate against the existing `"Bearer"` JwtBearer scheme unchanged — flagged tech
debt, paid down at Phase A3's asymmetric/JWKS cutover. `ValidIssuers` on the Bearer
scheme now accepts both the legacy plain-string `Jwt:Issuer` and the `urn:`-wrapped
form OpenIddict requires (an absolute URI). `OpenIddictClientSeeder` (dev-only,
`IsDevelopment()`-gated) seeds one `dev-test-client` application since OpenIddict has
no admin UI and a real client-management API is still a gap — noted below.

**Verified live** against the local dev AuthDB: `POST /connect/token` with
`grant_type=client_credentials` issues an HS256 JWT carrying `tenant_id`/`permission`;
that token was accepted by a real `[AuthorizePermission]`-gated endpoint
(`GET /api/v1/tenants`); wrong client secret correctly 401s; the pre-existing
`X-Api-Key` path was regression-checked and still works.

**Bug found and fixed while regression-testing** (pre-existing, not introduced by
A1): `ApiKeyAuthenticationHandler` deliberately omitted a `sub` claim ("no user
behind a service key"), but `ERP.Shared.Authorization.UserContext.IsAuthenticated`
requires a non-empty `UserId` sourced from `sub` — so every `[AuthorizePermission]`-
gated endpoint actually 401'd for every API-key caller despite a valid key/scope
match, since whichever session did §2's original "Done" pass evidently never tested
against a permission-gated endpoint live. Fixed by setting `sub` to the API key's own
id (not a real user, but enough to satisfy `IsAuthenticated`).

**Gap carried forward**: no real client-management API — `OpenIddictClientSeeder` is
a dev/test-only scaffold. A minimal admin endpoint (or extending `ApiKeysController`'s
pattern) for creating/rotating `client_credentials` applications is unstarted.

### Phase A2 — Authorization Code + PKCE ✅ Done, verified live end-to-end

`TokenService` and `/api/v{version}/auth/login`/`/refresh` stay completely unchanged
— this is a new, parallel login surface for third-party apps
(`ERP.Identity.Api/Controllers/OidcAuthorizationController.cs`), not a replacement for
this ERP's own frontends. Shape:
- A dedicated cookie scheme (`OidcInteractiveAuthenticationDefaults.Scheme`, added in
  `Program.cs` alongside — not instead of — the default `"SmartAuth"` chain) tracks
  the interactive browser session across the redirect chain.
- `GET/POST /connect/authorize` checks the cookie; if absent, redirects to
  `/connect/login` (a plain HTML form — no view engine/Razor added, just
  `ContentResult`) preserving the full original request as `ReturnUrl`. Credential
  check reuses the same `IPasswordHasher.VerifyPassword` primitive
  `AuthService.LoginAsync` uses, resolved via `IUserRepository.FindAllByEmailGloballyAsync`
  (no upfront tenant context, unlike the REST login) — first password match wins.
- Tenant resolution: explicit `?tenant_id=`, else a previously-picked claim cached on
  the cookie, else auto-picked if the user has exactly one `AuthUserTenant` row, else
  redirected to `/connect/tenant` (another plain HTML form) to choose. The picked
  tenant is written back onto the cookie so a later re-visit this session doesn't
  re-prompt.
- Once resolved, builds the final principal via the *same* `_unitOfWork.Roles.GetRolesAsync`/
  `_unitOfWork.Permissions.GetPermissionAsync`/`ILicenseService.GetEffectiveModulesAsync`
  calls `AuthService.IssueTokensAsync` uses for the REST login, then
  `SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)` — OpenIddict
  handles the rest (code issuance, redirect to the client's `redirect_uri`) with no
  extra event-handler code, unlike A1's client_credentials (there's a real interactively
  signed-in principal to draw from here).
- `RequireProofKeyForCodeExchange()` enforced platform-wide in `Program.cs`'s
  `.AddServer(...)`. ID tokens (issued when the client requests the `openid` scope)
  are signed with the asymmetric ephemeral key A0/A1 already registered to satisfy
  OpenIddict's own validation — access tokens still ride the symmetric key, unaffected.
- `OpenIddictClientSeeder` extended with a second, public (no secret) client
  `dev-test-spa` for verification, redirect URI `https://spa.example.local/callback`
  (intentionally non-resolving — verification captures `code` from the redirect
  `Location` header without following it).

**Explicit, documented gaps** (not oversights — called out in
`OidcAuthorizationController`'s own doc comment): no 2FA enforcement in this
interactive flow (the REST login enforces it via `TenantAuthSettings`; this flow
doesn't yet); no CSRF/antiforgery token on the login/tenant-picker POSTs (this project
only calls `AddControllers()`, no antiforgery middleware wired up); the
ambiguous-email-across-tenants edge case (a user with the same password on two
different tenants' accounts) is unresolved, same class of gap as the REST login's own
`FindAllByEmailGloballyAsync` design.

**Verified live end-to-end** (scripted curl with a real PKCE `code_verifier`/
`code_challenge` pair, cookie jar carried across requests): both the single-tenant
auto-select path and the multi-tenant picker path (verified by temporarily adding a
second tenant membership to the seeded SuperAdmin user, confirming the picker page
listed both tenants, and confirming the issued token carried the *picked* tenant, not
the default) — full round trip from unauthenticated `GET /connect/authorize` through
login, tenant resolution, `SignIn`, the redirect carrying `code`, and
`POST /connect/token` with the matching `code_verifier` returning both an access token
(`aud=ERP.Services`, `tenant_id`/`permission`/`role`/`licensed_module` claims matching
the REST-issued JWT's shape — HS256 at the time A2 was verified, RS256 after A3's
cutover below) and an `id_token` (`aud=dev-test-spa`, the OIDC-standard
client-as-audience, RS256 throughout since OIDC requires it). The access token was
then used successfully against a real `[AuthorizePermission]`-gated endpoint
(`GET /api/v1/tenants`, 200 OK).

### Phase A3 — discovery/userinfo endpoints + asymmetric signing cutover ✅ Done, verified live end-to-end

- `SetConfigurationEndpointUris("/.well-known/openid-configuration")`,
  `SetUserInfoEndpointUris("/connect/userinfo")`, and
  `SetJsonWebKeySetEndpointUris("/.well-known/jwks.json")` added to `Program.cs`'s
  `.AddServer(...)` — all three handled entirely by OpenIddict itself, no controller.
- **Asymmetric signing cutover**: `ERP.Identity.Infrastructure/OpenIddict/DevSigningCertificateProvider.cs`
  gets-or-creates a self-signed RSA certificate persisted in the current user's
  certificate store under a fixed subject name — replacing A0-A2's
  `AddEphemeralSigningKey()` stopgap, which regenerated a new key (and so orphaned
  every previously-issued token and any cached JWKS) on every process restart.
  Deliberately hand-rolled instead of OpenIddict's own
  `AddDevelopmentSigningCertificate()`: that method manages the certificate entirely
  internally with no way to retrieve its public key afterward, and the existing
  `"Bearer"` JwtBearer scheme needs that exact public key to keep validating
  OpenIddict-issued tokens now that they're RS256. `options.AddSigningKey(symmetricKey)`
  was removed from `.AddServer(...)` entirely — every OpenIddict-issued token (both
  access and ID) is now signed with this one certificate, closing A1/A2's flagged
  debt. `TokenService`'s proprietary `/api/auth/login`-issued JWTs are unaffected
  (still symmetric, per A2's explicit "stays unchanged" decision) — the `"Bearer"`
  scheme's `TokenValidationParameters.IssuerSigningKeys` now lists *both* keys
  (symmetric for proprietary tokens, the certificate's `X509SecurityKey` for
  OpenIddict-issued ones) so both validate side by side with no new scheme.
- `ERP.Identity.Infrastructure/OpenIddict/UserinfoClaimsHandler.cs` (a singleton
  `IOpenIddictServerHandler<HandleUserInfoRequestContext>`) populates
  `/connect/userinfo`'s response from the presented access token's own claims —
  OpenIddict's built-in userinfo handling only forwards a small OIDC-standard set by
  default, this adds the platform-specific ones (`tenant_id`/`tenant_name`/
  `permission`/`role`/`licensed_module`) matching what's already on the token itself.
- Still explicitly a development/local stopgap, not production PKI (self-signed,
  2-year validity, no rotation policy, no Key Vault/HSM) — real production
  certificate provisioning is flagged as a follow-up decision, not designed here.

**Verified live end-to-end**: `GET /.well-known/openid-configuration` returns a
complete, correct discovery document (all endpoint URIs, `grant_types_supported`,
`scopes_supported`, `code_challenge_methods_supported`, etc.); `GET /.well-known/jwks.json`
returns a real RSA public key (`kty: RSA`, `alg: RS256`, matching `x5c` certificate
chain) — confirmed NOT the HMAC secret. Re-ran both A1 (`client_credentials`) and A2
(Authorization Code+PKCE, both single- and multi-tenant paths) after the cutover:
both now issue RS256 access tokens whose `kid` matches the JWKS document, both still
validate successfully against the existing `"Bearer"` scheme and a real
`[AuthorizePermission]`-gated endpoint, and `GET /connect/userinfo` with either
token's access token returns the full expected claim set. The proprietary REST
`/api/auth/login` flow was re-verified unaffected (still issues a symmetric HS256
token, still works). Certificate persistence was confirmed by restarting the service
and checking the JWKS `kid` was unchanged (unlike the old ephemeral key, which
rotated — and so broke — on every restart).

### Deferred, not designed in detail

Bridging the existing `ExternalLogin` SSO providers (Google/Microsoft/Facebook)
*through* this service's own OIDC layer, so a third-party integrator gets "login with
Google via us" without knowing Google is involved — only makes sense with real
production traffic to justify it, not designed further here. Also deferred:
migrating the other 13 services off `AddErpJwtAuthentication`'s symmetric-key
validation onto JWKS-based asymmetric validation (now unblocked by A3, but a
15-services-wide change out of scope for this pass); deprecating `X-Api-Key`
(explicit decision: keep it indefinitely, A1's `client_credentials` is a new front
door for external integrators, not a replacement); real production certificate
provisioning (Key Vault/HSM, rotation policy) in place of A3's self-signed dev
certificate; a real client-management API (OpenIddict application CRUD) to replace
`OpenIddictClientSeeder`'s dev-only scaffolding.

## 2. API key enforcement at the service level ✅ Done

`ApiKeyAuthenticationHandler` (`ERP.Identity.Api/Authorization/ApiKeyAuthentication.cs`)
is a second `"ApiKey"` auth scheme alongside JWT bearer — a caller presents
`X-Api-Key: <raw key>`, validated via the already-existing
`IApiKeyService.ValidateAsync`. An `AddPolicyScheme("SmartAuth", ...)` picks between
the two per-request (`Program.cs`) so no controller needed
`[Authorize(AuthenticationSchemes=...)]` changes. The resulting `ClaimsPrincipal`
carries `tenant_id` + one `"permission"` claim per key scope — deliberately no
`sub`/role claims, so this scheme is for service-to-service calls only, not
user-self-service endpoints.

## 3. Webhooks for external event consumption ✅ Done

`WebhookEndpoint` (`ERP.Identity.Domain/Entities/WebhookEndpoint.cs`, tenant-scoped
URL + comma-separated event-type filter + HMAC secret) + `WebhooksController` (CRUD,
`security:webhooks:{view,manage}`) + `IWebhookSender`/`WebhookSender`
(`ERP.Identity.Infrastructure/Services/`). Delivery is fire-and-forget from
`OutboxProcessor` right after each successful Kafka publish — **deliberately no
separate delivery/retry/dead-letter table**, mirroring NotificationService's actual
existing `WebhookEndpoint`/`WebhookSender` pattern (same HMAC-SHA256 signing,
`X-Event-Type`/`X-Timestamp`/`X-Hub-Signature` headers) rather than the heavier
persisted-retry-queue design originally sketched here — Kafka remains the durable
guarantee, webhooks are best-effort, consistent with the one other place in this
codebase that already does this. `TwoFactorCodeGeneratedDomainEvent` is hard-excluded
(plaintext OTP, never webhook-eligible). Migration `019_AddWebhookEndpoints.sql`.

## 4. Admin UI: tenant management page ✅ Done

`TenantsPage.tsx` (`src/frontend/admin/src/pages/identity/`), mirroring
`RolesPermissionsPage.tsx`'s pattern exactly — list/create/edit/activate/deactivate,
gated on `identity:tenants:{view,create,edit,manage}`. Routed at `/identity/tenants`,
nav entry added to `Sidebar.tsx`.

## 5. Real localization ✅ Infrastructure + representative coverage done

Culture resolution (`RequestLocalizationMiddleware`, Accept-Language/`?culture=`
negotiation, en-US/vi-VN) already existed platform-wide via
`ERP.Shared.Localization.AddSharedLocalization`/`UseSharedLocalization`, already
called by IdentityService — the actual gap was that nothing ever called
`IStringLocalizer` to look up a translated string; `ERP.Shared`'s own
`SharedResource.resx`/`.vi-VN.resx` (generic `Validation_*`/`Error_*` keys) existed
too but was equally unused. Now wired up:
- `GlobalExceptionMiddleware` (shared, benefits all 16 services) — Titles and the
  generic 500 Detail fallback are localized via `SharedResource`, resolved
  **per-request** from `HttpContext.RequestServices` (not constructor-injected —
  Gateway and LoggingService call `UseGlobalExceptionHandling()` without
  `AddSharedLocalization()`, so a hard constructor dependency would have crashed
  both at startup; falls back to the original hardcoded English if no localizer is
  registered).
- New IdentityService-local `AuthMessages.resx`/`.vi-VN.resx`
  (`ERP.Identity.Infrastructure/Resources/Localization/`) for domain-specific
  messages — wired into `PasswordPolicyService` (all 6 complexity/history messages)
  and `UserService.InviteToTenantAsync`'s ambiguous-email message.
- `LoginRequestValidator`/`RegisterRequestValidator`/`TenantSignupRequestValidator`
  localized via `SharedResource`'s generic keys, using FluentValidation's
  lambda `.WithMessage(x => ...)` form so the message is resolved at validation
  time (per-request culture), not baked in once at validator construction.

Verified end-to-end (scratch console harness resolving both resources under
`en-US`/`vi-VN`): real Vietnamese text returned, not English fallback.

**Not done — scoped out of this pass, mechanical follow-up**: the other ~15
validator files across the service (roles/permissions/tenants/settings/API
keys/webhooks/etc.) still have hardcoded English `.WithMessage(...)` strings. Same
pattern as above, just repetitive; not blocking, no design decision needed.

## 6. Password-policy duplication ✅ Done

`PasswordPolicyService.ValidateNewPasswordAsync` now takes a `tenantId` and reads
complexity rules (`MinPasswordLength`/`RequireUppercase/Lowercase/Digit/Symbol`) from
`ISettingsService.GetOrCreateTenantSettingsEntityAsync` — the tenant's own
`TenantAuthSettings` row — instead of the single global `appsettings.json` default.
`PasswordPolicy:HistoryCount` stays system-wide (no per-tenant equivalent exists;
not actually duplicated).

## 7. Packaging / extraction (in progress — Part B1 done)

This service currently lives in a monorepo, referencing `ERP.Shared.*` for
cross-cutting concerns. Turning it into something a different team can actually drop
into their own project needs: (B1) an explicit, minimal dependency surface instead of
"the whole Shared library"; (B2-B8) an actual export mechanism, a generic (non-ERP)
RBAC seed, self-contained Docker packaging, and an external-consumer-facing
integration guide.

### Part B1 — restructure `ERP.Shared` into per-concern projects ✅ Done, verified

`src/Shared/ERP.Shared.csproj` was a single monolithic project (only `Core`,
`Messaging`, `Contracts` had their own `.csproj`; everything else — `Authorization`,
`Localization`, `Middleware`, `Extensions`, `Observability`, `Security`,
`Versioning`, `Infrastructure/{Caching,Exceptions,Data}` — compiled directly into the
root assembly). Split into 10 new per-concern projects
(`ERP.Shared.Authorization`/`.Caching`/`.Exceptions`/`.Data`/`.Localization`/
`.Middleware`/`.Observability`/`.Extensions`/`.Security`/`.Versioning`, each with its
own explicit `PackageReference`/`ProjectReference` list matching its actual internal
dependency graph — e.g. `Middleware` → `Exceptions`+`Localization`, `Extensions` →
`Middleware`+`Observability`, `Data` → `Core`), while `ERP.Shared.csproj` itself
became a zero-code umbrella (`<Compile Remove="**\*.cs" />` +
`<ProjectReference>` to all 13 sub-projects) — so the other 14 services, which still
reference only `ERP.Shared.csproj`, see zero change; everything they used to get from
one assembly they now get transitively through the umbrella instead. IdentityService's
own 3 `.csproj` files were then retargeted to reference only the specific
sub-projects they actually use (`Authorization`, `Contracts`, `Caching`, `Exceptions`,
`Messaging`, `Security`, `Localization`, `Extensions`, `Versioning` — determined by
grepping every `using ERP.Shared.*` across the service, not guessed), no longer the
umbrella at all — making its real dependency surface on Shared explicit and
mechanically verifiable instead of "everything."

**Non-obvious bug hit and fixed during this move**: `IStringLocalizer<SharedResource>`
silently stopped resolving real translations (fell back to raw key names, no
exception) after moving `SharedResource`/`LocalizationServiceCollectionExtensions`
into their own `ERP.Shared.Localization` project. Root cause, found via reflecting
into `ResourceManagerStringLocalizer`'s internal `_resourceBaseName` field:
`Microsoft.Extensions.Localization`'s factory computes the expected resource manifest
name as `{AssemblyName} + {ResourcesPath} + (TypeFullName minus AssemblyName prefix)`
— NOT via any `RootNamespace` MSBuild property or assembly attribute (none gets
generated for a plain `Microsoft.NET.Sdk` classlib). Since the new project's
`AssemblyName` ("ERP.Shared.Localization") is *identical* to `SharedResource`'s C#
namespace, that subtraction left nothing extra, while the physically-nested
`Resources/Localization/SharedResource.resx` path still produced an embedded resource
name *with* that extra segment — a mismatch invisible until actually queried at
runtime. Fixed by moving the `.resx` files up one level (`Resources/SharedResource.resx`,
no nested `Localization/` subfolder) so the embedded name's segment count matches what
the factory computes for this specific project. Documented in
`ERP.Shared.Localization.csproj` itself so it isn't reintroduced. (Contrast with
`ERP.Identity.Infrastructure`'s `AuthMessages`, which does need — and correctly has —
the extra `Resources/Localization/` nesting, because *its* `AssemblyName` is shorter
than `AuthMessages`' full namespace, so the subtraction leaves a real remainder there.)

**Verified**: `dotnet build` on the restructured `ERP.Shared.csproj` (all 13
sub-projects + umbrella, 0 errors); a scratch console app re-confirming
`IStringLocalizer<SharedResource>` resolves real English/Vietnamese text post-fix;
every one of the other 14 services' API projects + the Gateway built individually
against the new umbrella (0 errors, only a pre-existing unrelated AutoMapper
advisory warning in PurchaseOrderService); IdentityService's retargeted build (0
errors/warnings, umbrella no longer even in its dependency tree); and a full live
run — `/health`, OpenIddict discovery, `client_credentials` token issuance, the
proprietary REST login, and a permission-gated endpoint call — all still working
against the restructured dependency graph.

### Parts B2-B8 — ✅ Done, verified via an isolated build

`deployment/scripts/export-identity-service.ps1` — a manual, on-demand tool (never
run automatically) that produces a standalone, droppable copy of IdentityService.
Output goes to `export/identity-service-standalone/` (gitignored, never committed)
by default, or `-OutputPath` anywhere else.

**What it does:**
- Copies IdentityService's 4 projects + the 10 specific `ERP.Shared.*` sub-projects
  it depends on (Part B1's explicit reference lists — not the umbrella) into a
  flatter folder layout, then retargets each copied `.csproj`'s `ProjectReference`
  paths for the new depth (`..\..\..\..\Shared\` → `..\..\Shared\`). The `Shared.*`
  sub-projects' *own* internal `ProjectReference`s to each other need no changes —
  their relative nesting is preserved as-is.
- **Trims dead weight** from the copied `Authorization` folder: `AuthenticationExtensions.cs`
  (built for the *other 13 services* to validate Identity-issued JWTs — Identity
  itself never calls it), `HubTicketStore.cs`/`UserContextHeaderMiddleware.cs`/
  `HeaderUserContextService.cs` (Gateway/SignalR-hub patterns Identity doesn't use),
  `ModuleCodes.cs`/`LicenseEnforcementMiddleware.cs` (ERP module-license gating —
  Identity is the license *issuer*, not a consumer of this), and `RoleDefinitions.cs`
  (confirmed dead even in the source monorepo — referenced only in a comment).
- **Replaces `PermissionConstants.cs`** with a hand-curated version
  (`deployment/scripts/export-templates/PermissionConstants.cs`) keeping exactly the
  ~72 `identity:*`/`security:*`/`license:*`/`settings:*` constants IdentityService's
  own controllers reference by name (verified by grepping every
  `PermissionConstants.X` usage in the service, not guessed), dropping the ~230
  business-domain constants (`crm:*`, `sales_order:*`, `payments:*`, ...) meant for
  the other 13 services, plus a `Sample_*` pair demonstrating the pattern. The
  original's `GetAllPermissions()`/`GetTenantSettingsPermissions()` helpers are
  dropped too — confirmed unused by IdentityService's own runtime code.
- **Replaces the database seed**: seven ERP-specific migrations excluded
  (`013_SeedRoleDefinitionsIntoRolePermissions.sql`, `015_SeedDemoUsersAndRoles.sql`,
  `015_test_wrapped.sql`, `016_SeedFinanceApprovePermission.sql` — a
  PurchaseOrderService-only permission code, found while auditing this pass, not
  previously flagged — `017_AddChatBotLicenseModules.sql`,
  `018_AddChatBotAssistantPermission.sql`), replaced by a generic
  `013_SeedGenericRolePermissions.sql` seeding `sample:widgets:{view,manage}`. The
  pre-existing `008` filename collision is disambiguated in the export only
  (`008b_AddPermissionsAuditColumns.sql`) — left as-is in the monorepo, not a live
  bug there (both already run fine via alphabetical tiebreak).
- **Self-contained Docker packaging**: its own `Dockerfile` (`context: .` now means
  "the export folder," resolving the root Dockerfile's monorepo-wide `context: .`
  blocker) and `docker-compose.yml` (own `sqlserver`/`redis`; Kafka behind a
  `with-kafka` profile, opt-in — 2FA/webhooks already degrade gracefully without a
  broker, verified fire-and-forget behavior from earlier investigation).
- **New external-consumer README** (`deployment/scripts/export-templates/README.md`):
  written assuming zero monorepo familiarity — JWT validation instructions for both
  login surfaces (proprietary HS256 REST login vs. OIDC RS256 via JWKS), the
  `X-Tenant-Code` header, the two rate-limit policies, and an explicit "what's not
  included" section (OAuth client-management UI, production cert provisioning,
  CSRF on the OIDC interactive pages, 2FA not enforced on that same flow).
- **Negative-check**: greps the generated export for monorepo-specific strings
  (`ChatBot`, `PurchaseOrderService`, `crm:`, `sales_order:`, etc.) and warns if any
  are found — a repeatable pre-export sanity check. Currently reports 4 matches, all
  confirmed to be prose *explaining* what was excluded (in `AuthService.cs`'s own
  pre-existing code comments and this export's own template files), not actual
  leaked functional content.

**Verified — build-level isolation**: ran the script for real; copied its output to
a location with zero path back to the monorepo; `dotnet build
src/ERP.Identity.Api/ERP.Identity.API.csproj` there succeeded with 0 warnings/0
errors, using only the 14 projects physically present in that isolated copy.

**Verified — full Docker/SQL Server runtime** (done in a later pass, same session):
`docker compose build` + `up -d` from inside the export folder (port-remapped via a
verification-only `docker-compose.override.yml`, since the dev machine already had
something bound to host 1433) — `db-migrate` exited 0, `/health` returned 200,
`GET /.well-known` wasn't tested here (that's Part A, already verified elsewhere),
`POST /api/v1/auth/tenant-signup` → `POST /api/v1/auth/login` →
`GET /api/v1/tenants` with the resulting token all returned the expected
200s (and 401 with no token), Swagger loaded, and querying `Permissions` directly
confirmed only Identity's own ~72 codes + the `sample:widgets:*` pair exist — zero
business-domain codes. Kafka was intentionally left out (no `--profile with-kafka`)
to confirm the "degrades gracefully" claim in the README holds — it does (logged
`KafkaLogSink`/`rdkafka` warnings, no crash, `/health` still 200).

**Two real, pre-existing migration bugs were found and fixed by this run** — neither
was introduced by the export; they'd never been caught because no environment had
ever run this migration set against a truly empty database before:
- `004_AddTenantStatusColumn.sql`: `ALTER TABLE Tenants ADD Status ...` followed by
  `UPDATE Tenants SET Status = ...` **in the same batch** — SQL Server compiles an
  entire `GO`-delimited batch before executing any of it, so the `UPDATE` failed
  deferred name resolution ("Invalid column name 'Status'") since the column didn't
  exist yet *at compile time*, only after the `ALTER` had executed. Fixed by moving
  the `UPDATE` into `sp_executesql`, deferring its compilation to execution time.
- `006_AddCustomerAndStaffRoles.sql`: missing `USE AuthDB;` entirely (every other
  migration has it) — `sqlcmd` opens a fresh connection per file, whose login
  default database is `master`, not `AuthDB`, so every statement failed ("Invalid
  object name 'Tenants'").
- (Found while investigating the above, same root-cause class) `002_AddExternalAuthSettingsAndTwoFactor.sql`
  hardcoded permission IDs `30000000-…-000005` through `…008` for its own
  `settings:system/tenant:*` permissions — but those exact IDs were already claimed
  by unrelated permissions in `001_CreateAuthDB.sql` (copy-paste GUID reuse), so the
  insert hit a primary-key violation on every fresh run. Fixed by moving to
  `…000101`–`…000104`, past the highest ID (66) already claimed anywhere in the
  folder.

All three are fixed in the actual source migrations (not just the export's copies),
so every *future* export — and any other environment that runs this migration set
against a genuinely fresh database — gets the fix too. Re-ran the full build → up →
functional-check cycle after the fixes; everything passed cleanly the second time.

### What stays untouched in the monorepo

Confirmed by construction, not just by intent: the export script only ever writes
under `export/` (or an explicit `-OutputPath`), reads from the existing source tree,
and modifies nothing in `src/`, `docker-compose.yml`, the root `Dockerfile`, or
`src/Database/`. `git status` after running it shows zero changes outside the new
`deployment/scripts/` files and the `.gitignore` entry for `/export/`.

## Related

- [MultiTenantLogin.md](MultiTenantLogin.md) — tenant resolution + the
  `tenant-signup` self-service endpoint this roadmap's §7 packaging work would
  need to keep working outside this monorepo's Gateway.
- [ExternalAuthAndTwoFactor.md](ExternalAuthAndTwoFactor.md) — the existing SSO/2FA
  system §1's OIDC work would eventually sit alongside.
- `docs/deployment/manual-build-deploy-guide.md` — current per-service build/run
  instructions, relevant background for §7's extraction work.
- [docs/architecture/common-services-roadmap.md](../../architecture/common-services-roadmap.md) —
  this service's §7 packaging pattern applied platform-wide; see
  [../Notification/CommonServiceRoadmap.md](../Notification/CommonServiceRoadmap.md)
  for the second service (NotificationService) packaged the same way.
