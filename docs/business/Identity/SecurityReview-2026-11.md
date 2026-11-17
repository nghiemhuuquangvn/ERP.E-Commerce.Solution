# Security review 2026-11 — authentication, authorization, roles and permissions per tenant and user

Date: 2026-11-17. Branch at review time: `feat/manufacturing-p4-4`. Scope: every layer from the edge (nginx) to the API,
the Gateway, IdentityService, the shared authorization library, tenant data isolation, service-to-service calls,
the web SPAs and the mobile apps. **No code was changed by this review**; every finding is a proposed task (§7).

Previous review: [SecurityReview-2026-10.md](SecurityReview-2026-10.md) (2026-10-19 … 2026-11-02). §8 reconciles
its open items. Risks are also recorded in `docs/DEFERRED-ISSUES.md` §6 (R297 … R316).

Severity scale:
- **Critical:** exploitable now from the internet, with cross-tenant or platform impact.
- **High:** account takeover or platform access with realistic preconditions.
- **Medium:** needs a further weakness, an insider or network access, or weakens a control.
- **Low:** hardening.

No Critical finding was found.

---

## 1. Actors and assets

| Actor | Where they sign in | What they may do | What they must never reach |
|---|---|---|---|
| Anonymous visitor / guest | Storefront host of a tenant | Public catalog, guest chat, coupon check, sign-up | Any other user's data; another tenant |
| Customer (default role CUSTOMER, no permissions) | Storefront / customer app, tenant host | Own orders, own payments, own reviews, own chat | Back-office APIs of their shop; other customers |
| Staff / tenant user (roles with permissions) | Admin SPA, staff app, admin app, tenant host | What their permissions grant inside their tenant | Platform permissions; other tenants |
| Tenant administrator (holds `identity:users:manage`, role management) | Admin SPA, tenant host | Users, roles, settings, API keys of the tenant | Platform permissions (cannot grant what they do not hold, cannot grant platform scopes) |
| Platform operator (SYSTEM tenant, platform permissions) | Platform host | Tenants, licences, system settings | Acting inside another tenant without an explicit `[TenantRouteAccess]` route |
| Internal service | Service-to-service `internal/*` | Calls allowed by `ServiceAuth:TrustedCallers` for the request's tenant | User-facing APIs with a service token; another tenant than the one in its token |
| Carrier / payment gateway | Anonymous webhooks / IPN | Report status of one shipment / payment | Anything without a valid signature / shop secret |

Assets:
- User credentials and sessions: password hashes, refresh tokens, 2FA codes, API keys.
- Tenant business data in 17 databases plus Elasticsearch and ReportingDB.
- Platform control: tenants, licences, system auth settings.
- Signing and encryption keys.

---

## 2. Architecture as it is today (verified in code)

```
Browser / app ──► nginx (SPA container, tenant host)
                   │  /api/*  ──► Ocelot Gateway ──► service API ──► own DB (tenant query filter)
                   │  /hubs/* ──► service hub directly (bypasses the Gateway)
                   └  static SPA files (no security headers)
IdentityService ── RS256 private key (Key Vault / PFX) ── JWKS ──► every service + Gateway validate
Redis ── token revocation markers (jti, user, role, tenant) ── read by every service (fail-open)
Service A ──(own RS256 service token, request tenant)──► Service B internal/* [AuthorizeInternalService]
```

### 2.1 Authentication (who you are)

- **Access token:** RS256 JWT, 60 minutes (5 in Development). It carries `tenant_id`, roles, every effective permission and the licensed modules (`TokenService.cs` L31-46).
- **Key handling:** only IdentityService holds the private key. Every service and the Gateway validate through `ErpJwtBearerOptions` with:
  - issuer, audience, lifetime and signature checked;
  - `ValidAlgorithms=[RS256]`;
  - an exact issuer match.
  - No shared symmetric key exists (`NoSharedTokenKeyTests`).
- **Refresh token:** 64 random bytes, stored as SHA-256 only. Each use rotates it, and a reused token revokes the whole family. Lifetime is 7 days (`AuthService.cs` L686-733).
- **Passwords:** ASP.NET Identity PBKDF2 hasher. The per-tenant policy covers length, complexity and a history of 5. An account locks after 5 failures in 15 minutes.
- **2FA (SMS or e-mail):**
  - a 6-digit code from a cryptographic RNG, hashed with PBKDF2;
  - valid for 5 minutes, 3 attempts per challenge;
  - delivered only through the encrypted Identity outbox → NotificationService.
- **External login:**
  - Google, Microsoft and Facebook: the token the client obtained is verified.
  - Twitter/X: OAuth 2.0 Authorization Code + PKCE.
  - A link by e-mail happens only when the provider says the e-mail is verified (Google only).
- **OIDC (OpenIddict):** Authorization Code + PKCE and client credentials.
- **API keys:** `erpsk_` + 24 random bytes, stored with PBKDF2, scopes limited to what the creator holds. Accepted on IdentityService only.
- **Revocation:** logout, password change, lock, role / permission change and tenant status write markers to Redis after commit (`IUnitOfWork.Revocations`). Every service, the Gateway and the hubs read them.

### 2.2 Tenant resolution (which shop)

- **Tenant = request host.** The Gateway resolves the host through `TenantDomainService` (platform host → SYSTEM, a `TenantDomains` row, or `<code>.<BaseDomain>`).
- The Gateway also:
  - strips every client `X-Tenant-*` / `X-User-*` header;
  - sets its own headers;
  - refuses a JWT whose `tenant_id` differs from the host's tenant (`GatewayAuthorizationMiddleware.cs` L52-63, L89-101).
- Hubs bypass the Gateway. ChatBot resolves the tenant from the host itself (`HostTenantHubFilter`).

### 2.3 Authorization (what you may do)

- **Fallback policy:** every endpoint needs an authenticated user, and every action must declare one of:
  - `[AuthorizePermission]` / `[AuthorizeAnyPermission]`
  - `[AuthorizeAnyAuthenticatedUser]`
  - `[AllowAnonymous]`
  - `[AuthorizeInternalService]`

  `EndpointAuthorizationTests` enforces this.
- **Legacy controllers:** 51 controllers still check permissions inside the action (`InActionCheckAllowlist`, `tests/unit/ERP.Architecture.Tests/EndpointAuthorizationTests.cs` L35-87). The list may only shrink.
- **Permissions are the only authority**, never role names. `EffectivePermissions.ResolveAsync` is the single function that computes token permissions:
  - platform permissions (`PermissionScopes`) work only in the platform tenant;
  - `WhyNotGrantableAsync` refuses granting a platform scope or anything the granter does not hold (role grants and API key scopes).
- **Default roles hold no permissions:** CUSTOMER / STAFF / USER. Customer endpoints use own-resource checks, e.g. `SalesOrdersController.cs` L39 and L103.
- **`AdministratorGuard`** stops a shop from locking itself out.
- **Licensing:** the licence middleware hides modules the tenant has not bought.

### 2.4 Tenant data isolation

- **Every DbContext:**
  - implements `ITenantScopedDbContext`;
  - calls `ApplyTenantQueryFilters` last;
  - calls `EnforceTenantOnSave` on save.
- **Tests:** `TenantIsolationArchitectureTests`, `TenantFilterCoverageTests` and `TenantIsolationBehaviorTests` enforce this.
- **`IgnoreQueryFilters()`** appears in 19 places. All were reviewed: cross-tenant jobs returning tenant ids only, gateway / carrier callbacks finding the tenant of a signed message, and the employee-number generator.
- **Events:** they carry `TenantId` (`ITenantEvent`), and consumers fail closed without it.
- **Cache keys** are tenant-prefixed (`AddTenantKeyedCache`, `CacheKeyTenantTests`).

### 2.5 Clients

| App | Token storage | Logout | Permission gating |
|---|---|---|---|
| Admin SPA (React) | `localStorage` (zustand `persist`, key `erp-auth`) — access + refresh token | Calls `/auth/logout` | `usePermission` in 132 files — cosmetic, the API decides |
| Storefront (Vue) | `localStorage` (pinia `persist: true`) — access + refresh token | **Clears local state only** | n/a (customer) |
| Mobile admin (Expo) | Refresh token in `expo-secure-store`, access token in memory | Calls `/auth/logout` | Module registry from token permissions |
| Mobile customer / staff (Expo) | **`AsyncStorage` (plain file on the device)** | Local | n/a / few |

---

## 3. Findings — High

### SEC-H1 Account pre-hijack through Google linking (AuthN, per user)
- **Status: FIXED 2026-11-17 (wave S1).**
  - The link is **refused** for an account whose e-mail was never confirmed (401 + `ExternalLoginLinkRefused` audit). The owner resets the password first, and a successful reset now confirms the e-mail and revokes every session.
  - Rules BR-EXT-01..06 are in `ExternalAuthAndTwoFactor.md`.
  - Tests: `AuthServiceTests.ExternalLoginAsync_VerifiedEmailOfAnUnconfirmedAccount_IsRefused_SoASquatterKeepsNoAccess`, `ResetPasswordAsync_ConfirmsTheEmail_BecauseTheLinkWasDeliveredToTheMailbox`.
  - Chosen over "clear the password and link" because that path would revoke and issue tokens in the same second-resolution window (SEC-L9), risking a fresh token marked stale.
- **Evidence:**
  - `IdentityService/src/ERP.Identity.Infrastructure/Services/AuthService.cs` L585-598: an existing account with the same e-mail is linked when Google says the e-mail is verified, and `existing.ConfirmEmail()` runs when it was not confirmed.
  - Self-registration issues tokens before the e-mail is confirmed (L804-811).
- **Attack:**
  1. The attacker registers `victim@x.com` on a shop with their own password.
  2. Months later the real owner chooses *Sign in with Google*.
  3. The system confirms and links the account.
  4. The victim uses the account (addresses, orders, saved data), while the attacker's password and refresh token still work.
- **Impact:** silent account takeover in one tenant. For a staff account created by self-registration the impact is the same.
- **Acceptance criteria for the fix:**
  1. Linking to an account whose e-mail was **not confirmed** clears its password (or refuses the link), revokes every session (`RevokeAllForUserAsync`) and records an audit row.
  2. A test proves the old password and refresh token stop working after the link.
  3. Linking to an account whose e-mail **was** confirmed is unchanged.

### SEC-H2 Unknown host falls back to the platform tenant in the compose stack (Gateway, tenant)
- **Status: FIXED 2026-11-17 (wave S1).**
  - IdentityService (which owns `TenantDomainService`) refuses to start when `Tenancy:FallbackTenantCode` is set outside Development (`TenancyConfigurationGuard`, `ServiceMisconfiguredException`), unless `Tenancy:AllowFallbackTenantOutsideDevelopment=true`. `docker-compose.yml` sets that opt-in explicitly for the local stack.
  - `NoTenantFallbackInDeploymentTests` fails the build if any manifest under `deployment/` sets either key.
  - Tests: `TenancyConfigurationGuardTests`.
  - Checked live: Production + fallback without the opt-in stops at startup with the message.
- **Evidence:**
  - `docker-compose.yml` L282 sets `Tenancy__FallbackTenantCode: "SYSTEM"`, and the Gateway runs with `ASPNETCORE_ENVIRONMENT: Production` (L205).
  - `TenantDomainService.cs` L58-59 applies the fallback to every unknown host.
  - The comment at L277 says "never in a real deployment", but nothing enforces it.
- **Attack:** if this compose file is used on a server (it is the documented way to run the platform), any IP or host name becomes the platform tenant. That exposes the SuperAdmin login form and anonymous tenant sign-up there.
- **Impact:** platform-level exposure. Kubernetes does not set the value (`configmap.yaml` L21).
- **Acceptance criteria:**
  1. The Gateway refuses to start when `FallbackTenantCode` is set and the host is not Development, or a dedicated `Tenancy:AllowFallback=true` local flag is required.
  2. The compose file uses the local flag explicitly.
  3. A startup test covers it.

### SEC-H3 Long-lived tokens readable by any script on the page (frontend, per user)
- **Status: FIXED 2026-11-17 (waves S1 + S2).**
  - S1: Storefront sign-out revokes the tokens on the server.
  - S2: Admin and Storefront get the refresh token as the HttpOnly cookie `erp_rt` (`X-Auth-Transport: cookie`, `RefreshTokenCookie`), keep the access token in memory only and persist no token. The Admin counts idleness by user input (`useSessionKeepAlive`, `sessionActivity`).
  - Rules: [Sessions.md](Sessions.md) BR-SES-07..11.
- **Evidence:**
  - `src/frontend/admin/src/stores/auth.store.ts` L27-50 and `src/frontend/storefront/src/stores/auth.store.ts` L20-63 persist both tokens to `localStorage`.
  - The refresh token renews itself for 7 days on every use, so a session has no end (SEC-M3).
  - Storefront `logout()` (L54-58) does not call `/auth/logout`, so the refresh token stays valid on the server.
- **Attack:** one XSS (a third-party script, a future `v-html`, a compromised npm package) reads `erp-auth` and keeps a session alive indefinitely from another machine.
- **Mitigations present:** no `dangerouslySetInnerHTML` / `v-html` in the sources today. However, there is no Content-Security-Policy (SEC-M14).
- **Acceptance criteria:**
  1. The refresh token is set by IdentityService as an `HttpOnly; Secure; SameSite=Strict` cookie scoped to `/api/v1/auth`. The access token lives in memory only; a page reload calls refresh.
  2. Storefront logout calls `/auth/logout`.
  3. After logout, the old refresh token is refused (test).
  4. The mobile apps keep their body-based flow (no cookies).

---

## 4. Findings — Medium

| ID | Layer | Finding | Evidence | Acceptance criteria for the fix |
|---|---|---|---|---|
| SEC-M1 | AuthN | **FIXED 2026-11-17 (S3): per-device refusal + account threshold per tenant; OIDC form never locks (BruteForceProtection.md BR-BF-01/02/07).** **Anyone can lock out a known user.** The account locks after 5 failures; the IP guard allows 10. The OIDC form counts a failure against the same e-mail in **every tenant**. | `AuthService.cs` L132-137, L397-402; `SystemAuthSettings.cs` L31-32 | Lockout per (tenant, user) only; progressive delay instead of a hard lock for unknown IPs; a CAPTCHA after N failures. |
| SEC-M2 | AuthN | **FIXED 2026-11-17 (S3): atomic UPDATE for rotation and 2FA attempts; 10 s twin window (BR-BF-09/10).** **No concurrency control on refresh and 2FA verify.** Two parallel refreshes with one token can both succeed, and two legitimate tabs can trip reuse detection and sign the user out everywhere. Parallel 2FA verifies can exceed 3 attempts. | `AuthService.cs` L686-733; `TwoFactorChallenge.cs` L75-84; no `RowVersion` in Identity | An atomic `UPDATE … WHERE RevokedAt IS NULL` (rows affected = 1) for refresh and an atomic attempt counter; a grace window of a few seconds returns the same successor token; a concurrency test. |
| SEC-M3 | AuthN | **FIXED 2026-11-17 (S2): idle timeout + session limit per tenant, back office vs customer (`SessionPolicy`, Sessions.md).** **A session never ends.** Each rotation gives a new 7-day refresh token; `SessionTimeoutMinutes` (60) is not applied anywhere. | `AuthService.cs` L115; `SystemAuthSettings.cs` L33 | An absolute session lifetime (setting, e.g. 30 days) carried from the first login through every rotation; idle timeout applied; a test. |
| SEC-M4 | AuthN | **FIXED 2026-11-17 (S2): refresh tokens carry `DeviceSessionId`; revoking a device revokes them and a refresh of a revoked device is refused. Live check found the revoke endpoint never committed (`RevokeSessionAsync` now saves).** **"Revoke session" does not revoke.** Only the `DeviceSession` row is flagged; refresh tokens are not tied to it, and refresh creates "unknown device" sessions. | `SessionService.cs` L49-57; `UsersController.cs` L240; `RefreshToken.cs` | `RefreshToken.DeviceSessionId`; revoking a session revokes its tokens and publishes a revocation marker; refresh keeps the device session. |
| SEC-M5 | AuthN | **FIXED 2026-11-17 (S3): 2FA failures count as password failures, pending 2FA is no success, 5 codes / hour, resend newest only (BR-BF-03..05).** **2FA code guessable by spreading over IPs.** Every new login gives 3 more attempts; 2FA failures do not count toward lockout; every login sends an SMS (cost, bombing). | `AuthService.cs` L217; `Program.cs` L315-332 | 2FA failures counted per user and added to the lockout; a maximum of codes sent per user per hour; resend only for the newest, unexpired challenge. |
| SEC-M6 | AuthZ / tenant | **FIXED 2026-11-17 (S3): own 2FA kept; saving auth settings re-asks the password (BR-BF-08).** **A tenant setting switches off 2FA that users enabled for themselves.** No step-up is asked. | `SettingsService.cs` L154-159 | A user's own 2FA stays on regardless of the tenant switch (the tenant may *require* it, not *forbid* it); changing auth settings re-asks the password. |
| SEC-M7 | AuthN | **Token size and stale authority.** ~240 permissions (>10 KB) in the token plus an `X-User-Permissions` header; a permission change takes effect only through the revocation markers. | `TokenService.cs` L31-46; `GatewayAuthorizationMiddleware.cs` L124-141 | ADR: the token carries roles + a permission version; services resolve permissions through a tenant-keyed cache. No header over 8 KB in a test with the SuperAdmin token. |
| SEC-M8 | Revocation | **Revocation fails open and can be lost.** Redis down = revoked tokens accepted; a failed publish is logged and dropped; with no `ConnectionStrings:Redis`, an in-process store is used silently. | `TokenRevocationValidator.cs` L56-64; `TokenRevocationQueue.cs` L59-67, L137-142 | Revocations go through an outbox with retry; Production refuses to start without Redis; a metric / alert on fail-open; sensitive permissions (identity, payment refund) fail closed. |
| SEC-M9 | Infra | **Redis, Elasticsearch and Kafka have no authentication.** Redis has no `requirepass`; Elasticsearch has `xpack.security.enabled: "false"`; Kafka listeners are `PLAINTEXT`. RabbitMQ has a password. Anything on the Docker / cluster network can delete revocation markers, read every tenant's logs and product index, or inject events. | `docker-compose.yml` L96-98, L122-127, L147 | Redis ACL with a password per service; Elasticsearch security on with a service account; Kafka SASL/SCRAM; Kubernetes NetworkPolicy so only the owning services reach each broker. |
| SEC-M10 | API keys | **API keys outlive their owner.** Expiry is optional; a key keeps working after its creator is deactivated or loses permissions; every request runs PBKDF2 and writes to the DB (CPU / DB DoS). | `ApiKeyService.cs` L54, L93-106; `ApiKeyRepository.cs` L14-18 | Expiry required (max e.g. 1 year); each request re-checks the creator is active and still holds the scopes; the verification result is cached and `LastUsedAt` is written at most once a minute; a rate limit per key. |
| SEC-M11 | AuthZ | **51 legacy controllers check permissions inside the action.** A new action added to one of them without its check is open to any signed-in user of the tenant, **including CUSTOMER accounts** (the fallback policy only requires authentication). Areas: Accounting (8), Payment (5), POS (5), Sales (6), Warehouse (12), Shipping (4), Promotion (3) and others. | `EndpointAuthorizationTests.cs` L35-87 | Migrate per service to attributes and shrink the allowlist (the ratchet exists); a test that a CUSTOMER token gets 403 on every non-own-resource action of the migrated controllers. |
| SEC-M12 | Hubs | **Hub weaknesses:** a raw JWT is still accepted in `?access_token=` (it lands in nginx access logs); the Notification hub has no host-vs-token tenant check (ChatBot has `HostTenantHubFilter`); ticket consumption is get-then-remove (not atomic) and per instance; a guest chat can be joined by anyone who knows the conversation id. | `AuthenticationExtensions.cs` L85-89; `HubTicketStore.cs` L41-55; `admin/nginx.docker.conf` L83-116 | Hubs accept tickets only; tickets in Redis with atomic `GETDEL`, bound to user + tenant; the host-tenant filter on every hub; guest conversations bound to the guest session id. |
| SEC-M13 | Infra / data | **One database login for everything.** The `sa` password `YourPassword123` is committed in the `appsettings*.json` of ACC, CRM, HRM, PRD, MST and WH, with `Encrypt=False`; every service connects as `sa`. One compromised service reads every database of every tenant. | e.g. `AccountingService/src/ERP.ACC.API/appsettings.json` L7; DEFERRED S1 | Rotate the password; one login per service database with the least rights (P0-10); `Encrypt=True`; no connection string with a password in tracked files (a test). |
| SEC-M14 | Edge | **No browser security headers, CORS open.** nginx sets no CSP, HSTS, X-Frame-Options, X-Content-Type-Options or Referrer-Policy (no `add_header` in any SPA config); 15 services use `AllowAnyOrigin` / `WithOrigins("*")`. | `src/frontend/*/nginx.docker.conf`; `Program.cs` of 15 services | Headers in nginx (CSP `default-src 'self'` + the needed hosts, `frame-ancestors 'none'`, HSTS on TLS hosts, `nosniff`); CORS removed or limited to the tenant hosts (SPA and API share the origin). |
| SEC-M15 | Mobile | **FIXED 2026-11-17 (S2): staff and customer apps keep the refresh token in expo-secure-store, the access token in memory, and refresh on 401.** **Tokens in plain storage on the device.** The customer and staff apps persist the access + refresh token in `AsyncStorage`. The admin app already uses `expo-secure-store`. | `mobile/customer/stores/auth.store.ts` L31; `mobile/staff/stores/auth.store.ts` L47; `mobile/admin/lib/secureStorage.ts` | Same pattern as the admin app (refresh token in SecureStore, access token in memory); a parity test. |
| SEC-M16 | Gateway / tenant | **Custom domains are not proven.** A `TenantDomains` row is looked up before `<code>.<BaseDomain>`, with no ownership check and no rule against registering a sub-domain of the base domain. An operator mistake (or insider) can route another tenant's host to the wrong shop. | `TenantDomainService.cs` L32-37, L69-90 | DNS TXT verification before a domain becomes active; refusal of `*.<BaseDomain>` and of a host already resolving to another tenant; audit row. |

---

## 5. Findings — Low (hardening)

| ID | Finding | Evidence |
|---|---|---|
| SEC-L1 | User enumeration: register answers 409 for a known e-mail; an unknown user returns before PBKDF2 (timing). | `AuthService.cs` L152-159, L755 (old L1) |
| SEC-L2 | Register / tenant sign-up hash `Password.Trim()` while login and reset use the raw value — a password with surrounding spaces cannot sign in after registering. | `AuthService.cs` L763, L869 vs L173, L949 |
| SEC-L3 | No maximum password length, no breached-password check; a tenant admin can lower the tenant policy. | `PasswordPolicyService.cs` L38-55 |
| SEC-L4 | Forgot-password does not cancel earlier reset tokens and has no per-e-mail limit (mail bombing). Missing URL settings fall back to `http://localhost:5173`. | `AuthService.cs` L480, L916-923 |
| SEC-L5 | Google ID tokens are checked without a nonce (replay until expiry); the Microsoft issuer cannot match real tenant-specific issuers (old L2). | `GoogleAuthProvider.cs` L21-27; `MicrosoftAuthProvider.cs` L31 |
| SEC-L6 | Twitter `state` is not consumed nor bound to the browser (login CSRF); each authorize-url call stores a row. | `TwitterOAuthService.cs` L59-63; `AuthController.cs` L180-187 |
| SEC-L7 | OpenIddict `DisableTransportSecurityRequirement`; the OIDC cookie has no `SecurePolicy=Always`. | Identity `Program.cs` L183-185, L266-277 |
| SEC-L8 | Services keep the default 5-minute `ClockSkew` (Gateway 60 s); `typ` is not checked; `RequireHttpsMetadata=false`. | `ErpJwtBearerOptions.cs`; `configmap.yaml` L28 |
| SEC-L9 | `iat` has one-second resolution and the check is `iat < changedAt`: a token issued in the same second as a revocation survives; markers live 24 h. | `TokenRevocationValidator.cs` L74-78 |
| SEC-L10 | Forwarded headers trust all private ranges (10/8, 172.16/12, 192.168/16); k8s `TrustedHops=1` (S4). | compose L213-217; `configmap.yaml` L30-32 |
| SEC-L11 | Anonymous `POST /api/v1/logs/client` can flood or inject log lines (batch limit + Gateway rate limit only). | `LogsController.cs` L85-99 |
| SEC-L12 | Anonymous `GET pos/terminals/{id}/customer-display` shows the current basket to anyone with the terminal GUID (accepted: GUID, read-only). | `POSController.cs` L216-229 |
| SEC-L13 | Anonymous tenant sign-up (platform host) has no CAPTCHA and no e-mail confirmation: mass tenant creation. | `AuthController.cs` L57-65 |
| SEC-L14 | Product image upload trusts the client's content type (allow-list jpeg/png/webp/gif, 10 MB) and keeps the client file extension on the blob name; no magic-byte check. Blobs are served with the declared image content type, so no script execution today; without `nosniff` (SEC-M14) a file with HTML content and an image type is a weak spot. | `ProductImageFileConstraints.cs`; `AzureBlobFileStorageService.cs` L46-55 |
| SEC-L15 | Client credentials (OIDC) copy permission claims from the application properties without the "only grant what you hold" check (accepted only by Identity; clients created by a dev seeder). | `ClientCredentialsClaimsHandler.cs` L61-67 |
| SEC-L16 | Service keys `Ephemeral` (gateway, chatbot, sales …) work with a single replica only; a second replica breaks service-token validation (availability, not confidentiality). | `SigningKeys.md` |

---

## 6. Verified as sound (no action)

- **Tokens:**
  - RS256 only, exact issuer / audience, no shared key (`NoSharedTokenKeyTests`).
  - Refresh, reset and confirmation tokens are hashed and single-use.
  - One-time secrets travel encrypted in the outbox.
- **Gateway:**
  - strips client `X-Tenant-*` / `X-User-*` headers;
  - refuses a token of another tenant;
  - never routes `/internal`;
  - `StreamingProxyMiddleware` runs after the same authentication and tenant checks.
- **Platform vs tenant permissions:**
  - platform permissions are stripped outside the platform tenant;
  - "only grant what you hold" applies to role grants and API key scopes;
  - platform role codes are reserved;
  - no authority from role names.
- **Anonymous sign-up** always creates a Customer; the default roles hold no permissions; the customer order endpoints check ownership (`SalesOrdersController.cs` L39, L103).
- **`[AllowAnonymous]`:** 44 uses, all reviewed. They are:
  - the public catalog (product, category, price, variant, review, search, stock availability without quantities);
  - coupon validation;
  - Identity sign-in flows;
  - payment IPN / callback through `GatewayNotificationService` (signature + amount check);
  - carrier webhooks through `CarrierWebhookService` (per-shop secret);
  - guest chat start;
  - the POS customer display;
  - client log ingestion.
- **`IgnoreQueryFilters()`:** 19 uses, all cross-tenant jobs that return tenant ids only, signed-callback tenant lookups, or the employee-number generator.
- **XSS sinks:** no `dangerouslySetInnerHTML` / `v-html` / `innerHTML` in the frontend sources (only in tests and built bundles of libraries).
- **Swagger:** Development only.
- **Secrets in git:** `deployment/k8s/secrets.yaml` is untracked; only `secrets.yaml.example` is in git.
- **Published ports:** backend service ports are not published; local infrastructure ports are bound to `127.0.0.1`; only nginx `:80` is public.

---

## 7. Risk matrix per tenant and user

| Threat | Controls in place | Remaining gaps |
|---|---|---|
| A user of tenant A reads tenant B's data | Query filters + save enforcement + architecture tests; Gateway token-vs-host check; tenant-keyed cache; tenant on every event | Hubs bypass the Gateway (M12); one `sa` login for all DBs (M13); brokers / Elasticsearch without auth (M9); BI tools on ReportingDB must filter `TenantId` themselves |
| A tenant administrator gains platform power | `PermissionScopes`, `WhyNotGrantableAsync`, reserved platform roles | Unknown host = SYSTEM in compose (H2); custom-domain mistakes (M16) |
| A customer calls back-office APIs of their shop | Fallback = authenticated + explicit attributes; default roles empty | 51 controllers with in-action checks (M11) |
| Account takeover | Hashing, lockout, 2FA, rotation + reuse detection, revocation | H1, H3, M2, M3, M5, M15 |
| A user removed / locked keeps access | Revocation markers on every change | Fail-open and lost publishes (M8), Redis without auth (M9), API keys (M10), session revoke (M4) |
| Denial of service against a user | Gateway rate limits | Lockout by strangers (M1), 2FA SMS bombing (M5), reset mails (L4) |

---

## 8. Reconciliation with the 2026-10 review

| Old item | Status now | Note |
|---|---|---|
| M5 (tenant ADMIN `identity:tenants:view`) | **Closed** — the permission became a platform scope (`PermissionScopes`), stripped outside SYSTEM | The old table still says "Open"; this review supersedes it. |
| M6 (`sa`, `Encrypt=False`) | Open → **SEC-M13** | |
| M7 (phone verification for SMS 2FA) | Open | Keep with SEC-M5. |
| L1-L4 | L1 → SEC-L1, L2 → SEC-L5; others unchanged | |
| S11 (raw JWT in hub query) | Open → **SEC-M12** | |
| "Identity port 5001 published to the host" | **Stale** — backend ports are not published | |
| External login linking policy (P0-7) | Policy correct for unverified provider e-mails, but **misses unconfirmed local accounts** → SEC-H1 | |

---

## 9. UAT / penetration scenarios (to run after each fix)

| # | Finding | Scenario | Expected |
|---|---|---|---|
| UAT-SEC-01 | H1 | Register `x@test` with password P (no confirmation); sign in with Google as `x@test` | Old password P and old refresh token are refused; an audit row exists |
| UAT-SEC-02 | H2 | Call the Gateway with `Host: unknown.example` in a non-Development configuration | 404 / refusal, never the SYSTEM tenant |
| UAT-SEC-03 | H3 | Sign in on the Admin and Storefront; inspect `localStorage` | No refresh token; refresh works after a reload; Storefront logout makes the old refresh token fail |
| UAT-SEC-04 | M1 | From one IP, 5 wrong passwords for a user of tenant A | The user of tenant B with the same e-mail is not locked; tenant A user gets a delay, not a lock |
| UAT-SEC-05 | M2 | Send the same refresh token twice in parallel | Exactly one new pair; the second gets the same pair within the grace window or 401 — never two families |
| UAT-SEC-06 | M3 / M4 | Revoke a device session in the Admin; use its refresh token | 401 |
| UAT-SEC-07 | M5 | 10 logins from 10 IPs, each with 3 wrong codes | The account locks; SMS count capped |
| UAT-SEC-08 | M8 / M9 | Stop Redis, lock a user, start Redis | The lock still takes effect (outbox replays); Redis refuses unauthenticated clients |
| UAT-SEC-09 | M10 | Deactivate the creator of an API key; call with the key | 401 |
| UAT-SEC-10 | M11 | With a CUSTOMER token, call every action of each migrated controller | 403 except own-resource routes |
| UAT-SEC-11 | M12 | Open the Notification hub of tenant B with a tenant A token / with `?access_token=<JWT>` | Refused |
| UAT-SEC-12 | M14 | `curl -I` the Admin and Storefront | CSP, X-Frame-Options/`frame-ancestors`, `nosniff`, Referrer-Policy present; HSTS on TLS |
| UAT-SEC-13 | M15 | Inspect the customer / staff app storage on a device | No token in AsyncStorage |
| UAT-SEC-14 | M16 | Register a custom domain that is not verified / a sub-domain of the base domain | Not active / refused |

---

## 10. Proposed remediation plan (each wave = one task / PR, owner approves first)

| Wave | Items | Area touched | Notes |
|---|---|---|---|
| S1 — quick, High | H2, H1, Storefront server logout (part of H3) | Identity startup + `AuthService`, storefront store | **Done 2026-11-17** |
| S2 — sessions | H3 (cookie refresh token), M3, M4 (+ M15) | Identity `AuthController` / `AuthService` / `RefreshToken`, Admin + Storefront auth stores and API client, nginx | **Done 2026-11-17** |
| S3 — brute force | M2, M5, M1, M6 | Identity | **Done 2026-11-17** |
| S4 — infrastructure | M8, M9, M13, M14, M15 | Shared `TokenRevocation`, compose + k8s, nginx, mobile stores | M13 = ROADMAP P0-10 |
| S5 — structure | M11 (allowlist), M12 (ticket-only hubs), M10, M16, M7 | Many services; Identity | M7 needs an ADR |
| Low | SEC-L1 … SEC-L16 | — | Picked up with the wave that touches the same file |

## 11. Assumptions and open questions

- A production deployment runs behind TLS with an ingress that enforces `server_name`. The review assumes the compose stack is local only, but nothing enforces that (H2).
- Not covered by this review:
  - certificate pinning and deep links of the mobile apps;
  - Serilog redaction of request bodies (no body logging was found, but sinks were not audited field by field);
  - Kubernetes NetworkPolicies (none found in `deployment/k8s`).
- Owner decision needed:
  - the absolute session lifetime (M3);
  - whether e-mail confirmation becomes mandatory before the first sign-in (H1 / L13);
  - whether a tenant may lower the password policy (L3).
