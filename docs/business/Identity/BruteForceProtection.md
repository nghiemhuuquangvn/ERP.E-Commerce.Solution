# Brute-force protection — passwords, two-factor codes, per-tenant lockout, parallel refresh

Owner decisions (2026-11-17):
- Lockout is per device (client IP) plus a total threshold. Values are tenant settings.
- At most **5 two-factor codes per account per hour**.
- A person's own 2FA cannot be switched off by the shop.

Security review: [SecurityReview-2026-11.md](SecurityReview-2026-11.md) wave S3 (SEC-M1, SEC-M2, SEC-M5, SEC-M6).

Code (Identity):
- `AuthService` (`IsDeviceBlockedForAsync`, `RecordGuessFailureAsync`, `VerifyTwoFactorAsync`, `RefreshTokenAsync`);
- `LoginAttemptRepository`, `TwoFactorChallengeRepository` / `RefreshTokenRepository` (atomic updates);
- `SettingsService`, `SettingsController`.

SQL: `Identity/039_BruteForceAndConcurrency.sql`.

Related: [Sessions.md](Sessions.md), [ExternalAuthAndTwoFactor.md](ExternalAuthAndTwoFactor.md).

## 1. Actors

| Actor | Concern |
|---|---|
| Account owner | Must be able to sign in although a stranger guesses at their account |
| Attacker | Knows an e-mail (and maybe the password) and guesses passwords or 2FA codes, from one or many IPs |
| Shop administrator | Sets the shop's values (Admin → Settings → Authentication → Tenant), re-confirming their password |
| Platform operator | Sets the ceilings (→ System) |

## 2. Business rules

| Rule | Statement |
|---|---|
| BR-BF-01 | **Per device.** When one client IP failed **MaxLoginAttempts** times (default 5) for one account within **LockoutDurationMinutes** (15) since its last sign-in from that IP, that IP is refused for that account. The refusal says *"Too many failed attempts from this device"* and comes before the password is compared. Other devices of the owner are not affected, and no account flag is set. |
| BR-BF-02 | **Whole account.** When the failures of an account from every IP together reach **AccountLockThreshold** (default 20) within **AccountLockWindowMinutes** (60) since its last sign-in, the account is locked for that window. The lock is temporary: it ends by itself, the administrator can unlock earlier, and the owner's running sessions stay alive. Audited as `AccountLocked`. |
| BR-BF-03 | A wrong **2FA code** counts exactly like a wrong password in BR-BF-01 / 02. A blocked device or a locked account gets no new code and no code check. A sign-in whose password was right but whose 2FA is pending is **not** a success, so it does not reset the counts. Only a completed sign-in (after the code) does. |
| BR-BF-04 | At most **5 codes per account per hour** (`TwoFactorAuthentication:MaxCodesPerHour`), whatever asked for them: sign-in, external sign-in or resend. Beyond that: *"Too many verification codes were sent to this account. Please try again later."* (400 `TwoFactorTooManyCodes`). |
| BR-BF-05 | A resend is accepted only for the account's **newest** live challenge, after the 60-second cooldown. An older challenge id cannot trigger sends. |
| BR-BF-06 | Ranges: per-device limit 3–20; account threshold above the per-device limit (so one device can never lock the account) and ≤ 1000; window 5 min – 1 day. A shop may lock **sooner and longer**, never later or shorter, than the platform (threshold ≤ platform, window ≥ platform). |
| BR-BF-07 | The OIDC sign-in form (no tenant) **never** locks an account. A failure there is kept by e-mail and IP only and is limited by its own per-(IP, e-mail) guard. It used to lock the same e-mail in every tenant. |
| BR-BF-08 | A shop may **require** 2FA but never switch off the 2FA a person turned on for themselves. Only the platform master switch (an emergency off) does. Saving authentication settings re-asks the caller's password, or needs a sign-in in the last few minutes for an external-only account. A wrong or missing password is refused (400) and audited `AuthSettingsChange`. |
| BR-BF-09 | **Parallel refresh.** Of several refreshes with one token, only one rotates it (one atomic database update). The others, and a token **rotated** less than `Jwt:RefreshReuseGraceSeconds` (10 s) ago, receive a sibling pair of the **same session** (same start, same device). A token presented again after that window, or one revoked on purpose (sign-out, device, account change), is treated as theft: every session of the user ends. |
| BR-BF-10 | A 2FA attempt is taken by one atomic update before the code is compared, and a challenge signs in only once. Parallel requests cannot get more than its 3 attempts. |

## 3. Process (sign-in with 2FA)

```
password form ──► device over its limit? ── yes ──► refused (BR-BF-01)
      │ no
      ▼
password right? ── no ──► failure row (account, IP); total ≥ threshold? ──► account locked (BR-BF-02)
      │ yes
      ▼
2FA needed? ── no ──► signed in (success row: counts restart)
      │ yes
      ▼
codes this hour < 5? ── no ──► refused (BR-BF-04)
      │ yes
      ▼
code sent ──► verify: device / account blocked? ──► refused
               attempt left (atomic)? ── no ──► refused
               code right? ── no ──► failure row (as a wrong password)
                       │ yes
                       ▼
               signed in (success row: counts restart)
```

State of an account: **Open → Temporarily locked (window) → Open**. An administrator lock is separate: it is indefinite and ends sessions.

State of a device for an account: **Allowed ↔ Refused**. This is not stored; it is computed from the login history.

## 4. Use cases

**UC-BF-1 A stranger guesses the password of a known e-mail.**
- After 5 failures their IP is refused.
- The owner signs in from home or office as usual.

**UC-BF-2 A botnet guesses from many IPs.**
- At 20 failures within the hour the account locks for 60 minutes. The owner keeps working in already signed-in apps.
- The administrator sees `AccountLocked` in the audit log and may unlock.

**UC-BF-3 An attacker has the password and guesses 2FA codes.**
- Every wrong code counts. Signing in again does not restart the count.
- At most 5 codes per hour are sent, and the device / account limits stop the guessing.

**UC-BF-4 Two browser tabs refresh at the same moment.** Both keep working, and nobody is signed out.

**UC-BF-5 A copied refresh token is replayed a minute later.** Every session of the user ends (theft signal).

**UC-BF-6 An administrator turns the shop's 2FA switch off.**
- People who turned 2FA on themselves still get a code.
- The change asked for the administrator's password.

## 5. Data

| Table | Change |
|---|---|
| `LoginAttempts` | New index by `UserId`. Every failed password / 2FA attempt of a known account carries `UserId`. |
| `TwoFactorChallenges` | New index by (`UserId`, `CreatedAt`) |
| `RefreshTokens` | `RevokedReason` (existing NVARCHAR column, now `Rotated` / `Revoked`) |
| `SystemAuthSettings` / `TenantAuthSettings` | `AccountLockThreshold` (20), `AccountLockWindowMinutes` (60). `MaxLoginAttempts` / `LockoutDurationMinutes` now mean per device. |

## 6. Configuration

| Key | Meaning |
|---|---|
| `TwoFactorAuthentication:MaxCodesPerHour` | Codes per account per hour (5) |
| `TwoFactorAuthentication:ResendCooldownSeconds` | Resend cooldown (60) |
| `TwoFactorAuthentication:MaxAttempts` | Attempts per code (3) |
| `Jwt:RefreshReuseGraceSeconds` | Refresh twin window (10) |
| `PasswordPolicy:MaxFailedAttemptsPerWindow` / `PasswordPolicy:FailedAttemptsWindowMinutes` | (IP, e-mail) guard before any lookup (10 / 15); unchanged |

## 7. Assumptions and open issues

- **Assumption:** the client IP is correct (`ForwardedHeaders:TrustedHops` matches the proxies; DEFERRED S4 for k8s). Behind one NAT, every user shares the device limit.
- **Open:** no CAPTCHA yet. It is a separate wave (provider keys, all five apps).
- **Open:** the OIDC form is limited per (IP, e-mail) only. A distributed attack there is not counted toward the account threshold.
- **Open:** the account lock is not notified to the owner yet (only audited).

## 8. UAT

| # | Scenario | Expected |
|---|---|---|
| UAT-BF-01 | 5 wrong passwords from device A, then the right one from A | Refused "too many failed attempts from this device" |
| UAT-BF-02 | Then the right password from device B | Signed in |
| UAT-BF-03 | 20 failures from several devices within an hour | Account locked 60 min; audit `AccountLocked`; open sessions still work; admin unlock works |
| UAT-BF-04 | Password right, then 3 wrong codes, sign in again, 3 wrong codes … | Device refused after 5 failures in total; no reset by re-signing in |
| UAT-BF-05 | Six sign-ins with 2FA within an hour | The sixth: "too many verification codes" |
| UAT-BF-06 | Resend with the id of an older challenge | Refused |
| UAT-BF-07 | Two refreshes with one token in parallel | Both 200, same session, nobody signed out |
| UAT-BF-08 | The same token 15 s later | 401, every session of the user ends |
| UAT-BF-09 | Shop 2FA off; a user with own 2FA signs in | Code still asked |
| UAT-BF-10 | Save auth settings without / with a wrong password | 400, nothing changed, audited |
| UAT-BF-11 | OIDC form: wrong passwords for an e-mail present in two tenants | Neither account locked |
