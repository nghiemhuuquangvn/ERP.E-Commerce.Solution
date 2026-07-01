# External Login & Two-Factor Authentication

**Service:** IdentityService (Port 5001)
**Database:** AuthDB

## Purpose

Lets users sign in via Google/Microsoft/Facebook/Twitter (SSO) instead of a password,
and adds an optional/mandatory second factor (SMS or Email one-time code) on top of
password login. Both are gated by a two-tier Settings hierarchy so a SuperAdmin can
control the whole system while each tenant's own Admin only controls their tenant.

## Settings hierarchy

Two entities, both effective only in combination:

- **`SystemAuthSettings`** — one well-known row, edited only by SuperAdmin
  (`settings:system:manage`). This is the master switch: if a provider or 2FA is off
  here, no tenant can turn it back on.
- **`TenantAuthSettings`** — one row per tenant, edited by that tenant's Admin
  (`settings:tenant:manage`) or by SuperAdmin for any tenant.

A provider is usable, or 2FA is available, only when **both** the System and the
Tenant row have it enabled (`System.XEnabled && Tenant.XEnabled`). 2FA has one more
flag beyond "available": `TenantAuthSettings.TwoFactorRequired` — when true, every
user in that tenant is forced through 2FA at login regardless of their own
`User.TwoFactorEnabled` preference; when false, 2FA is opt-in per user.

`GET /api/auth/login-options?tenantCode=` exposes the resolved (System ∧ Tenant)
booleans without requiring a JWT — this is how a login page knows which SSO buttons
to render before the user has authenticated.

## External login providers

| Provider | Flow | Verified via |
|---|---|---|
| Google | Client SDK → ID token | `GoogleAuthProvider` validates the ID token's audience against `Authentication:Google:ClientId` |
| Microsoft | Client SDK (MSAL) → ID token | `MicrosoftAuthProvider` validates against Azure AD's OIDC signing keys + `ClientId` |
| Facebook | Client SDK → access token | `FacebookAuthProvider` calls Graph API's `/me` (profile) + `/debug_token` (confirms the token's `app_id` matches ours) |
| Twitter/X | Server-side redirect (OAuth 2.0 Authorization Code + PKCE) | `TwitterOAuthService` — Twitter has no client-only SDK yielding a directly verifiable, app-scoped token like the other three, so it needs a full redirect round-trip with `client_secret` token exchange instead of a single verify call |

Google/Microsoft/Facebook all implement `IExternalAuthProvider.VerifyAsync(...)` and
are called from `AuthController.ExternalLogin` (`POST /api/auth/external-login`).
Twitter is **not** an `IExternalAuthProvider` — it has its own two-step endpoint pair
(`GET .../twitter/authorize-url`, `POST .../twitter/callback`) because the flow shape
is fundamentally different (stateful, needs a persisted `OAuthState` row for the PKCE
`code_verifier` between the two requests).

All four providers converge on the same "find existing `ExternalLogin` by
(Provider, ProviderKey), else auto-register a new `User` with default roles for that
tenant" logic, then issue tokens through the same `AuthService.IssueTokensAsync` path
as a normal password login.

Adding a 5th provider (e.g. GitHub, LinkedIn): if it offers a client-side SDK with a
directly verifiable token (like Google/Microsoft/Facebook), implement
`IExternalAuthProvider` and register it in `DependencyInjection.cs` — no controller or
`AuthService` changes needed, `ExternalLoginAsync` already resolves by `.Provider`
string. If it only offers a redirect-based flow (like Twitter), mirror
`TwitterOAuthService` instead.

## Two-factor authentication

1. `POST /api/auth/login` verifies the password as normal. If 2FA is required for
   this user (`System.TwoFactorEnabled && Tenant.TwoFactorEnabled && (Tenant.
   TwoFactorRequired || User.TwoFactorEnabled)`), no tokens are issued yet — instead a
   `TwoFactorChallenge` is created (6-digit code, hashed, 5-minute expiry, max 3
   attempts by default — all configurable under `TwoFactorAuthentication:*`), and the
   response is `{ requiresTwoFactor: true, challengeId, twoFactorMethod,
   expiresInSeconds }`.
2. The plaintext code is carried by `TwoFactorCodeGeneratedDomainEvent`, published to
   Kafka (`identity-topic`) via the Outbox pattern — **never stored in plaintext**,
   and never sent via a direct/synchronous call to NotificationService.
   NotificationService's `IdentityEventConsumer` picks it up and sends it via SMS or
   Email per `Method`, using the `TWO_FACTOR_CODE` template.
3. `POST /api/auth/2fa/verify` (`{ challengeId, code }`) checks the hash, attempt
   count, and expiry; on success, issues tokens through the same shared
   `IssueTokensAsync` path every other login flow uses.
4. `POST /api/auth/2fa/resend` respects `TwoFactorAuthentication:ResendCooldownSeconds`
   and issues a fresh challenge (the old one is left to expire naturally rather than
   being explicitly invalidated).

Users manage their own 2FA preference via `POST /api/users/{id}/2fa/enable`
(`{ method, phoneNumber? }`) and `.../2fa/disable` — both are self-service only (the
route `{id}` must match the caller's own JWT `sub` claim, same check as the existing
`change-password` endpoint). `disable` is rejected if the tenant has
`TwoFactorRequired` set — a user cannot opt out of a tenant-mandated policy.

## Related

- `docs/architecture/event-catalog.md` — `TwoFactorCodeGeneratedEvent` entry
- `docs/architecture/database-map.md` — `SystemAuthSettings`/`TenantAuthSettings`/
  `TwoFactorChallenges`/`OAuthStates` table shapes
- `src/services/IdentityService/README.md` — full endpoint list
- `docs/frontend/README.md` — Admin app's login page / 2FA screen / Auth Settings page
