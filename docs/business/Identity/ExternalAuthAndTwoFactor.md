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

`GET /api/auth/login-options` exposes the resolved (System ∧ Tenant) booleans without
requiring a JWT — this is how a login page knows which SSO buttons to render before
the user has authenticated. The tenant is resolved from the `X-Tenant-Code` header
(injected by the Gateway from the request's subdomain, or sent directly by the
client) rather than a `?tenantCode=` query param — see
[MultiTenantLogin.md](MultiTenantLogin.md) for the full redesign.

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
tenant" logic, then run through the same `AuthService.IssueTokensOrChallengeAsync`
gate as a normal password login — this checks `TwoFactorEnabled` and returns a
`RequiresTwoFactor` challenge instead of tokens when applicable. **Fixed 2026-07-08:**
until then, `ExternalLoginAsync`/`TwitterCallbackAsync` called `IssueTokensAsync`
directly with no 2FA check at all, so a 2FA-enabled user could bypass 2FA entirely by
signing in via an external provider instead of `/auth/login`.

### Linking an external sign-in to an existing account (rules, 2026-11-17, SEC-H1)

Actors: the person signing in with a provider; possibly an earlier self-registrant who used the same e-mail.

| Rule | Statement |
|---|---|
| BR-EXT-01 | An external identity already linked in the tenant `(provider, providerKey, tenant)` signs in as that user. |
| BR-EXT-02 | No account with the e-mail in the tenant → a new **Customer** is created (client `UserType` ignored); its e-mail is confirmed only when the provider verified it (Google). |
| BR-EXT-03 | An account with the e-mail exists and the provider did **not** verify the e-mail (Facebook, Microsoft, Twitter) → refused (401). |
| BR-EXT-04 | An account with the e-mail exists, the provider verified it, **and the account's e-mail is confirmed** → the identity is linked to that account; account status and 2FA apply as for a password sign-in. |
| BR-EXT-05 | An account with the e-mail exists, the provider verified it, but **the account's e-mail was never confirmed** → refused (401, "reset the password through *Forgot password*, then sign in again"), nothing is linked or confirmed, a `ExternalLoginLinkRefused` audit row is written. Reason: anyone can self-register with another person's address; confirming and linking that account would leave the registrant's password and sessions working (account pre-hijack). |
| BR-EXT-06 | A successful **password reset confirms the e-mail** (the link was delivered to the mailbox) and ends every session of the account. |

Use case UC-EXT-L1 "Owner of a squatted address signs in with Google":
- **Main flow:** Google sign-in → refused per BR-EXT-05.
  1. The person uses *Forgot password* and opens the mail.
  2. They set a new password. The account is confirmed and every other session ends (BR-EXT-06).
  3. They sign in with Google again, and it links (BR-EXT-04).
- **Alternative:** the person simply signs in with the password they set themselves; nothing is linked.
- **Exception:** the reset link expired (60 min) → request a new one.

UAT:
- **UAT-EXT-01:** register `x@test` without confirming, then Google sign-in with `x@test`. Expect 401 with the *Forgot password* hint. No link row; the account is still unconfirmed.
- **UAT-EXT-02:** continue with a password reset, then Google sign-in. Expect the sign-in to succeed and the account to be linked. The old password and the old refresh token are refused.
- **UAT-EXT-03:** Google sign-in for an account that confirmed its address. Expect the account to be linked as before.

Adding a 5th provider (e.g. GitHub, LinkedIn): if it offers a client-side SDK with a
directly verifiable token (like Google/Microsoft/Facebook), implement
`IExternalAuthProvider` and register it in `DependencyInjection.cs` — no controller or
`AuthService` changes needed, `ExternalLoginAsync` already resolves by `.Provider`
string. If it only offers a redirect-based flow (like Twitter), mirror
`TwitterOAuthService` instead.

## Two-factor authentication

1. `POST /api/auth/login` (and, since 2026-07-08, `POST /api/auth/external-login` and
   the Twitter callback too — all three share `IssueTokensOrChallengeAsync`) verifies
   the password/provider token as normal. If 2FA is required for this user
   (`System.TwoFactorEnabled && Tenant.TwoFactorEnabled && (Tenant.
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

- [MultiTenantLogin.md](MultiTenantLogin.md) — how the tenant is resolved for
  `login-options`/`external-login`/Twitter authorize-url (subdomain/header-based, no
  manual Tenant Code field)
- `docs/architecture/event-catalog.md` — `TwoFactorCodeGeneratedEvent` entry
- `docs/architecture/database-map.md` — `SystemAuthSettings`/`TenantAuthSettings`/
  `TwoFactorChallenges`/`OAuthStates` table shapes
- `src/services/IdentityService/README.md` — full endpoint list
- `docs/frontend/README.md` — Admin app's login page / 2FA screen / Auth Settings page

## Brute-force protection (2026-11-17)

Lockout per device and per account, wrong 2FA codes counting as wrong passwords, at most 5 codes per hour, resend of the newest challenge only, and "a shop may require 2FA but never remove a user's own": see [BruteForceProtection.md](BruteForceProtection.md) (BR-BF-01..10).
