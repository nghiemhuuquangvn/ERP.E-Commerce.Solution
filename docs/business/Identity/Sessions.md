# Sign-in sessions — idle timeout, session limit, where the tokens live

Owner decision 2026-11-17: **a back-office session ends after 15 minutes without activity**. The values are tenant settings.
Security review: [SecurityReview-2026-11.md](SecurityReview-2026-11.md) wave S2 (SEC-H3, SEC-M3, SEC-M4, SEC-M15).
Code: `SessionPolicy` (Identity Domain), `AuthService.IssueTokensAsync` / `RefreshTokenAsync`, `RefreshTokenCookie` (Identity API).
SQL: `Identity/038_SessionPolicy.sql`.

## 1. Actors

| Actor | Apps | Session class |
|---|---|---|
| Back-office user: Staff, Admin, SuperAdmin, System | Admin web, Admin app, Staff / POS app | **Back office** |
| Customer (user type Customer) | Storefront, Customer app | **Customer** |
| Platform operator | Admin → Settings → Authentication → System | Sets the ceilings |
| Shop administrator (`settings:manage`) | Admin → Settings → Authentication → Tenant | Sets the shop's values within the ceilings |

## 2. Concepts

- **Session:** the period from one sign-in (password, 2FA, Google, …) until it ends. It is carried by a chain of refresh tokens: every refresh replaces the token (rotation) but continues the same session.
- **Idle timeout:** the session ends when it is not refreshed within this time.
- **Session limit (absolute):** the session ends at this time after the sign-in, whatever the activity. The person then signs in again (with 2FA when it applies).
- **Access token:** short-lived proof sent with every call. It lives a **third of the idle timeout** (5 minutes for 15), never longer than `Jwt:Expired`, never past the session limit.
- **Device session:** the row in "where am I signed in" (`DeviceSessions`). Every refresh token belongs to one.

## 3. Business rules

| Rule | Statement |
|---|---|
| BR-SES-01 | A session ends after the idle timeout without activity. Activity = the person uses the app. The Admin web counts mouse / keyboard / touch input, not background calls (the notification bell polls every minute and must not keep an abandoned session alive). |
| BR-SES-02 | A session ends at its limit counted from the sign-in. A refresh never moves the start. |
| BR-SES-03 | Back-office users and customers have separate values. The class is decided by the account's user type, never by the app. |
| BR-SES-04 | Defaults: back office **15 minutes idle, 12 hours limit**; customer **7 days idle, 30 days limit**. |
| BR-SES-05 | Range: idle timeout ≥ 5 minutes; back-office limit 1 hour – 30 days; customer limit 1 – 365 days; idle ≤ limit. A shop's value may not exceed the platform's (business-rule 400 `TenantSessionPolicyWeakerThanSystem` / `SessionPolicyOutOfRange`). |
| BR-SES-06 | A lowered value applies to running sessions at their next refresh (the limit is checked with the current setting). |
| BR-SES-07 | Web apps (Admin, Storefront) never hold the refresh token in page storage. It is an **HttpOnly, SameSite=Strict, Secure** cookie `erp_rt`, path `/api/v1/auth`, sent only on the tenant's own host. The access token lives in memory only. |
| BR-SES-08 | The cookie is used only when the request carries `X-Auth-Transport: cookie`. Another site cannot add this header, which guards against CSRF. Mobile apps omit the header and keep the refresh token in the device keystore (`expo-secure-store`), never in AsyncStorage. |
| BR-SES-09 | *Sign out this device* (Users → sessions) ends that device session: its refresh tokens are revoked at once, and a refresh of that session is refused. Its last access token lives at most one access lifetime (5 minutes by default). |
| BR-SES-10 | Sign-out revokes the refresh token and the access token on the server and deletes the cookie. |
| BR-SES-11 | A refused refresh ends the session in the app. The Admin login page then says *"Your session ended after a period without activity."* |
| BR-SES-12 | Reaching the session limit is audited (`SessionExpired`). An idle expiry is not (too frequent to be useful). |

## 4. Process

```
sign-in ──► session starts (SessionStartedAt = now, device session registered)
   │        refresh token expires at min(now + idle, start + limit); access token = idle / 3
   ▼
working ──► each refresh (access token expired, or keep-alive 2 minutes before) rotates the token:
   │        same start, same device, new expiry min(now + idle, start + limit)
   ├── no activity for idle ─────────────► refresh refused (token expired)        ──► ENDED (idle)
   ├── start + limit reached ────────────► refresh refused + audit SessionExpired ──► ENDED (limit)
   ├── device revoked / account changed ─► refresh refused                         ──► ENDED (revoked)
   └── sign-out ─────────────────────────► tokens revoked, cookie deleted          ──► ENDED (sign-out)
```

State model of a session: **Active → Ended (idle | limit | revoked | signed out)**. A revoked refresh token presented again is treated as theft: every session of the user ends. This rule is unchanged from before.

## 5. Use cases

**UC-SES-1 Work through a long form (back office).**
- **Main flow:** the person types for 20 minutes without saving. The keep-alive refreshes the access token while they type. The save succeeds.
- **Alternative:** they leave for 16 minutes. On return, the first action finds the session ended, and the login page explains why.

**UC-SES-2 Reload the Admin or the Storefront.** The page has no access token after a reload. The first call gets a 401, refreshes once through the cookie and repeats the call. The person stays signed in.

**UC-SES-3 Revoke a lost phone.**
1. The administrator opens Users → the user's sessions → revokes the phone.
2. The phone's next refresh is refused and the app signs out.

**UC-SES-4 Shop tightens its policy.**
- **Main flow:** the shop sets 10 minutes / 8 hours. New sign-ins use it, and running sessions apply it at their next refresh.
- **Exception:** the shop tries 30 minutes while the platform allows 15. The change is refused with the reason.

**UC-SES-5 POS till left idle.** A till without use for more than the shop's idle timeout signs the cashier out. A shop whose tills stay idle longer raises its back-office idle timeout, within the platform ceiling. A POS-specific class is an open item (§9).

## 6. Data

| Table | Column | Meaning |
|---|---|---|
| `RefreshTokens` | `SessionStartedAt` | Sign-in of the session (null on old rows → `CreatedAt`) |
| `RefreshTokens` | `DeviceSessionId` | Device session of the token (indexed) |
| `SystemAuthSettings` / `TenantAuthSettings` | `SessionTimeoutMinutes` | Back-office idle timeout (default now 15; rows still at the old 60 were set to 15 once by migration 038) |
| same | `SessionAbsoluteHours` | Back-office limit (12) |
| same | `CustomerSessionTimeoutMinutes` | Customer idle timeout (10080) |
| same | `CustomerSessionAbsoluteDays` | Customer limit (30) |

## 7. Integrations and configuration

- **API:**
  - `POST /api/v1/auth/login | register | 2fa/verify | external-login | external-login/twitter/callback | refresh`: with `X-Auth-Transport: cookie` the response sets `erp_rt` and returns `refreshToken: null`. Responses carry `refreshTokenExpiresAt`.
  - `POST /api/v1/auth/logout` reads the cookie in cookie mode and deletes it.
- **Configuration:**
  - `Jwt:Expired`: the maximum access lifetime.
  - `Jwt:RefreshTokenExpirationDays`: a hard ceiling on one refresh token.
  - `Auth:RefreshCookie:AllowInsecure=true`: local http stack only; `NoTenantFallbackInDeploymentTests` forbids it under `deployment/`.
  - `Auth:RefreshCookie:Path`: default `/api/v1/auth`.
- **Gateway / nginx:** pass `Cookie` / `Set-Cookie` unchanged. The cookie is host-only, so a tenant's cookie never reaches another tenant's host.

## 8. Permissions

| Action | Permission |
|---|---|
| Change the platform ceilings | Existing system auth settings permission |
| Change the shop's values | Existing tenant auth settings permission |
| Revoke a device session | Unchanged (Users screen) |

## 9. Assumptions and open issues

- **Assumption:** every shared deployment is served over HTTPS (the cookie is `Secure`).
- **Open:** a POS-specific session class (tills idle longer than the back office); today the shop raises its back-office idle timeout.
- **Open:** server-side sign-out from the Staff / Customer apps. They forget the session locally; the server ends it after the idle timeout.
- **Open:** the Admin app (mobile) has no input-based keep-alive. Its refresh-on-401 means background calls can keep a session alive while the app is open.

## 10. UAT

| # | Scenario | Expected |
|---|---|---|
| UAT-SES-01 | Admin: sign in; check browser storage | `erp-auth` has no token; cookie `erp_rt` is HttpOnly |
| UAT-SES-02 | Admin: reload the page | Still signed in, data loads |
| UAT-SES-03 | Admin: leave 16 minutes with the dashboard open, then click | Login page with the "session ended" message |
| UAT-SES-04 | Admin: type in a form for 20 minutes, then save | Saved, still signed in |
| UAT-SES-05 | Sign in at 08:00 (limit 12 h), stay active | At 20:00 the next refresh is refused; audit `SessionExpired` |
| UAT-SES-06 | Revoke a device session | The device's next refresh is refused |
| UAT-SES-07 | Shop sets an idle timeout above the platform's | Refused with the reason |
| UAT-SES-08 | `curl` refresh with the `erp_rt` cookie but no `X-Auth-Transport` header | 401 (CSRF guard) |
| UAT-SES-09 | Storefront: sign in, reload, sign out; replay the old cookie | Signed in after the reload; replay refused |
| UAT-SES-10 | Staff app: work past 5 minutes | Calls keep working (refresh on 401); refresh token only in the keystore |
