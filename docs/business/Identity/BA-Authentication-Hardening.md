# Identity — Business Analysis: external sign-in, two-factor codes and authentication hardening

| | |
|---|---|
| **Domain** | Identity (IdentityService, port 5001), with the Gateway, NotificationService and the Admin / Storefront sign-in screens |
| **Covers** | ROADMAP **P0-7** (external login provisioning, 2FA delivery) and **P0-8** (security hygiene: 2FA resend, re-authentication, secrets in messages, rate limiting, client IP, Swagger). The two-tier settings, the providers and the 2FA flow are described in [ExternalAuthAndTwoFactor.md](ExternalAuthAndTwoFactor.md); the review that found the problems is [SecurityReview-2026-10.md](SecurityReview-2026-10.md). |
| **Status** | Implemented and verified on the local Docker stack, 2026-10-23. External-provider paths were verified by unit tests only (no real Google / Microsoft tokens). Written afterwards, 2026-10-25, to the standard of [BA-Accounting.md](../Accounting/BA-Accounting.md). |
| **Audience** | Product owner, security officer, tenant administrators, support, QA, developers |
| **Companion documents** | [MultiTenantLogin.md](MultiTenantLogin.md), [License.md](License.md), [SigningKeys.md](SigningKeys.md), [event-catalog](../../architecture/event-catalog.md) |

> This document describes what a **user, an administrator and the business** can rely on. It does not repeat the token, key and tenant-isolation design (SecurityReview, SigningKeys, CLAUDE.md "Security invariants").

---

## 1. Business context and objectives

### 1.1 Problem
Sign-in looked complete but several paths **did not work or leaked**:
* **External login never created a user.** A first-time Google / Microsoft / Facebook / X user could not be provisioned (an exception instead of "not found", an empty password hash rejected, the provider link saved without its tenant), so the very first SSO login failed.
* Identity events did not reach Notification: the Kafka message was serialised twice, so the consumer received a JSON string; default notification templates lay under the wrong tenant ("Template 'TWO_FACTOR_CODE' not found").
* The 2FA message used the **masked** destination and an SMS without a phone number failed silently.
* A resend left the earlier 2FA code valid; switching 2FA on or off needed no password, so a stolen session could weaken the account.
* The one-time **code and the reset / confirmation links sat in clear text** in the outbox table and on the Kafka topic; webhook secrets sat in clear text in the database.
* Swagger was reachable in every environment; the gateway had no limit on sign-in attempts.
* Behind the two nginx proxies, Identity recorded the **proxy's** address as the client's, so the per-IP limits and the failed-login window were shared by every user.

### 1.2 Objectives
| # | Objective | How we know it is met |
|---|---|---|
| O1 | A new user can sign in with Google, Microsoft, Facebook or X, becomes a **Customer**, and never gains rights from what the provider or client claims. | IDN-FR-01 … 05; UAT-IDN-01 |
| O2 | A 2FA code reaches the user on the channel they chose, once, and only the newest code works. | IDN-FR-06 … 09; UAT-IDN-02, 03 |
| O3 | Weakening an account's protection needs the current password. | IDN-FR-10; UAT-IDN-04 |
| O4 | One-time secrets and webhook secrets never rest in clear text. | IDN-FR-11, 12; UAT-IDN-05, 06 |
| O5 | Sign-in endpoints resist guessing and scripted abuse per real client address. | IDN-FR-13, 14; UAT-IDN-07, 08 |
| O6 | Nothing exposes the API schema outside development. | IDN-FR-15; UAT-IDN-09 |

### 1.3 Scope
**In scope:** external sign-in (four providers), account linking and provisioning rules, two-factor challenge creation / delivery / resend / verification, re-authentication for 2FA changes, encryption of one-time secrets and webhook secrets, gateway rate limiting, client address handling, API schema exposure.
**Out of scope:** roles and permission design, tenant resolution and token revocation (SecurityReview, MultiTenantLogin), key management (SigningKeys), per-service SQL logins and encrypted connections (ROADMAP **P0-10**), SMS number verification (P0-10), email-confirmation message for externally provisioned users (open, see 15).

---

## 2. Stakeholders and actors

| Actor | Who | What they do |
|---|---|---|
| Customer | Storefront user | Signs up / in with an external provider or a password; may switch 2FA on. |
| Staff user | Admin portal user | Signs in with a password or a linked provider; 2FA may be required by the tenant. |
| Tenant administrator | `settings:tenant:manage` | Enables providers and 2FA for the tenant; may make 2FA mandatory. |
| Platform administrator (SuperAdmin) | `settings:system:manage` | Master switch for providers and 2FA across all tenants. |
| Security officer | — | Reviews audit entries and the behaviours below. |
| Notification service (system) | NotificationService | Delivers the code by SMS or email. |
| Gateway (system) | API Gateway | Enforces the per-IP sign-in limits. |

---

## 3. Glossary

| Term | Meaning |
|---|---|
| External login / SSO | Signing in with an account of another provider (Google, Microsoft, Facebook, X). |
| Provisioning | Creating a local user the first time an external identity signs in. |
| Linking | Attaching an external identity to an existing local user with the same **verified** email. |
| 2FA challenge | A one-time code (6 digits, 5 minutes, 3 attempts) sent after a correct password. |
| Re-authentication | Asking for the current password again before a sensitive change. |
| Sealed value (`enc:v1:`) | A secret stored encrypted; only the services that share the settings key can open it. |
| Redacted | A delivered secret overwritten with `[redacted]` in the outbox. |
| Trusted hops | The number of proxies in front of a service; used to read the real client address. |

---

## 4. End-to-end business processes

### 4.1 First sign-in with an external provider
1. The tenant and the platform both allow the provider (login options show the button only then).
2. The user signs in at the provider; the client sends the provider's token; Identity **verifies it with the provider**.
3. If the identity is already linked → sign in. Otherwise, if the provider confirms the **email is verified** and a local user of the tenant has it → **link** to that user. If the email is not verified and a local user has it → refused. If nobody has it → **provision** a new **Customer** with an unusable random password.
4. The sign-in then goes through the same gate as a password login: if 2FA applies, a challenge is issued instead of tokens.

### 4.2 Sign in with two-factor
Password (or provider) correct → challenge created (code hashed in the database) → the real code goes to Notification through the outbox (sealed) → Notification sends it by SMS or email to the **real** address / number (the stored copy is masked, e.g. `ad***@…`) → the user enters the code → tokens. **Resend:** a new code ends the previous open challenge; a challenge already verified cannot be resent; a cooldown applies.

### 4.3 Switch 2FA on or off
The user opens their security settings and enters the **current password** (an account that only uses an external provider has none: it may send none while its access token is at most 5 minutes old). Failures are audited. A user cannot switch 2FA off when the tenant requires it.

### 4.4 Protecting secrets and the front door
Codes, reset links and confirmation links are sealed in the outbox, delivered, then overwritten. Webhook secrets are sealed in the database. The Gateway answers **429** when a client address exceeds 30 requests a minute on the sign-in family of endpoints. The client address is read from the forwarded header using the configured number of trusted proxies.

---

## 5. Functional requirements

| ID | Requirement | Pri | Source |
|---|---|---|---|
| IDN-FR-01 | A first-time external user is provisioned as a **Customer** with an unusable random password; the account is tenant-scoped. | M | P0-7 |
| IDN-FR-02 | The link between the user and the provider identity carries the tenant, so the next sign-in finds it. | M | P0-7 |
| IDN-FR-03 | A **verified** provider email matching an existing tenant user links to that user; an **unverified** email matching a user is refused. | M | P0-7 |
| IDN-FR-04 | Facebook and Microsoft report the email as **not verified** (Microsoft's `preferred_username` is a changeable name, not proof of the mailbox). | M | P0-7 |
| IDN-FR-05 | Anonymous sign-up and external provisioning never trust a client-supplied role or user type; the result is always Customer. | M | review |
| IDN-FR-06 | External sign-in and the X callback go through the same 2FA gate as a password sign-in. | M | existing |
| IDN-FR-07 | A 2FA challenge keeps only the **masked** destination; the event carries the real recipient. | M | P0-7 |
| IDN-FR-08 | 2FA by SMS for a user with no phone number fails with a clear error (`TwoFactorNoDestination`). | M | P0-7 |
| IDN-FR-09 | A resend ends the user's earlier open challenges; a verified challenge cannot be resent. | M | P0-8 |
| IDN-FR-10 | Enabling or disabling 2FA requires the current password (or a fresh token for password-less accounts); failures are audited. | M | P0-8 |
| IDN-FR-11 | The code, reset link and confirmation link are sealed in the outbox, delivered, then redacted; Identity refuses to write them without the key. | M | P0-8 |
| IDN-FR-12 | Webhook secrets are stored sealed; saving an endpoint without a secret keeps the stored one. | M | P0-8 |
| IDN-FR-13 | The Gateway limits sign-in, sign-up, reset, refresh, 2FA, external login and token endpoints to 30 requests per client address per minute (429 with `Retry-After`); non-GET chatbot calls likewise. | M | P0-8 |
| IDN-FR-14 | The client address is read with the configured trusted hops, not from the raw forwarded header. | M | P0-8 |
| IDN-FR-15 | The API schema (Swagger / OpenAPI) is served only in the Development environment. | M | P0-8 |
| IDN-FR-16 | Identity events reach Notification as JSON objects with the right template under SYSTEM or the tenant. | M | P0-7 |

### 5.1 Acceptance criteria
| ID | Acceptance criteria | UAT |
|---|---|---|
| IDN-FR-01 … 04 | Given a new Google user with a verified email nobody has, then a Customer exists, no admin role, and the second sign-in finds the same user; given a Facebook user whose email matches a staff user, then sign-in is **refused** (email not verified); given a Google user with a verified email of an existing user, then it links to that user. | UAT-IDN-01 (unit) |
| IDN-FR-05 | Given a sign-up body with `userType` Admin, then the user is a Customer. | UAT-IDN-01 |
| IDN-FR-07, 08 | Given an email-2FA login, then the challenge stores `ad***@…`, the event and the Notification record use the real address; given SMS with no number, then the error. | UAT-IDN-02 |
| IDN-FR-09 | Given two resends, then only the last code verifies; the first is rejected. | UAT-IDN-03 |
| IDN-FR-10 | Given enable / disable without the password, then refused and audited; with it, accepted; an external-only account within 5 minutes of its token, accepted without. | UAT-IDN-04 |
| IDN-FR-11 | Given a delivered code, then the outbox row shows `[redacted]` while Notification received the real code; the Kafka topic holds `enc:v1:`. | UAT-IDN-05 |
| IDN-FR-12 | Given a webhook secret, then the stored value starts with `enc:v1:`; given an update with no secret, then it is kept. | UAT-IDN-06 |
| IDN-FR-13 | Given 31 sign-in requests in a minute from one address, then the 31st is 429 even if `X-Forwarded-For` rotates. | UAT-IDN-07 |
| IDN-FR-14 | Given two clients behind the proxies, then each has its own failed-login window. | UAT-IDN-08 |
| IDN-FR-15 | Given Identity outside Development, then `/swagger` is 404. | UAT-IDN-09 |

---

## 6. Business rules

| ID | Rule |
|---|---|
| IDN-BR-01 | **Provider and tenant must both allow** a method (`System ∧ Tenant`); with 2FA, the tenant can additionally make it mandatory; a user cannot opt out of a mandatory policy. |
| IDN-BR-02 | The provider's token is **verified with the provider** (audience / app id checked); the client's claims about role, tenant or email are not trusted. |
| IDN-BR-03 | **Provisioning is always Customer.** Staff and administrators are created by invitation, never by SSO or sign-up. |
| IDN-BR-04 | **Linking by email only if the provider says the email is verified.** An unverified email that matches a user is refused to prevent account takeover. |
| IDN-BR-05 | An account is "email confirmed" only if the provider verified the email. |
| IDN-BR-06 | A 2FA code: 6 digits, stored as a hash, valid 5 minutes, 3 attempts, resend after a cooldown; the newest challenge is the only valid one. |
| IDN-BR-07 | The destination is masked in storage and in the UI; the real recipient exists only in the delivery event. |
| IDN-BR-08 | **Re-authentication** for security changes; an external-only account uses token age (≤ 5 minutes) instead of a password. A refresh renews the token age, so this bounds a stolen access token, not a stolen refresh token. |
| IDN-BR-09 | One-time secrets exist in clear text only in memory and in the delivery; they are sealed at rest and in transit on the topic, and overwritten after delivery. Identity and Notification must share the settings encryption key. |
| IDN-BR-10 | Changing the shared key makes undelivered sealed messages and stored webhook secrets unreadable (codes live 5 minutes; webhook secrets must be re-entered). |
| IDN-BR-11 | Rate limits are **per client address**, per Gateway replica; Identity adds finer per-IP limits and a per-account lockout. |
| IDN-BR-12 | The client address is `RemoteIpAddress` after the forwarded-header middleware with `ForwardedHeaders:TrustedHops` proxies (2 in the local stack). **A value too low** puts all users in one bucket; **too high** lets clients spoof the header. It must match the real ingress chain on Kubernetes. |
| IDN-BR-13 | The API schema is a development tool: it is not exposed in any other environment. |
| IDN-BR-14 | Every token-issuing path first checks that the user and the tenant are active and not administratively locked. |

---

## 7. Use cases

### UC-IDN-01 Sign in with an external provider
*Actor:* customer / staff user. *Pre:* provider enabled by platform and tenant. *Main flow:* see 4.1. *Alternatives:* already linked → sign in; verified email of an existing user → link; 2FA applies → challenge (UC-IDN-02). *Exceptions:* provider token invalid → refused; unverified email matching a user → refused ("sign in with your password and link the provider"); provider disabled → refused; tenant or user locked → refused.

### UC-IDN-02 Sign in with a one-time code
*Actor:* any user with 2FA. *Main flow:* password → code sent → enter code → tokens. *Alternatives:* resend after the cooldown → new code, old one dead. *Exceptions:* wrong code (up to 3 attempts), expired, no phone number for SMS → clear errors.

### UC-IDN-03 Change the 2FA setting
*Actor:* signed-in user. *Main flow:* choose method and enter the current password → saved → audit. *Exceptions:* wrong password → refused and audited; tenant mandates 2FA → cannot disable.

### UC-IDN-04 Configure sign-in options for a tenant
*Actor:* tenant administrator (`settings:tenant:manage`) / SuperAdmin. *Main flow:* turn providers and 2FA on or off for the tenant; mark 2FA mandatory. *Constraint:* cannot enable what the platform has switched off.

### UC-IDN-05 Handle a blocked client
*Actor:* support. *Trigger:* a user reports "too many requests". *Flow:* the 429 carries `Retry-After`; wait; check that the user is not behind a shared address with scripted traffic. *Note:* limits are per address per Gateway replica.

---

## 8. Data requirements

| Entity | Key attributes |
|---|---|
| User | tenant, email, email confirmed, password hash (unusable for SSO-only), type, 2FA enabled and method, phone |
| ExternalLogin | tenant, user, provider, provider key |
| TwoFactorChallenge | user, method, **masked destination**, code hash, expires at, attempts, status (open, verified, superseded) |
| SystemAuthSettings / TenantAuthSettings | per provider and 2FA: enabled; tenant: 2FA required |
| OutboxMessage (Identity) | event content (sealed values `enc:v1:`, redacted after delivery) |
| WebhookEndpoint | URL, events, **sealed secret** |

---

## 9. Integrations

| Direction | Event | Transport | Content / effect |
|---|---|---|---|
| Identity → Notification | `TwoFactorCodeGenerated` | Kafka `identity-topic` (outbox) | method, real recipient, **sealed** code → SMS / email with template `TWO_FACTOR_CODE` |
| Identity → Notification | `PasswordResetRequested`, `EmailConfirmationRequested` | same | sealed link |
| Identity → others | `UserCreated`, `RoleAssigned`, `PermissionGranted` | same | — |
| Providers → Identity | token verification | HTTPS | Google / Microsoft / Facebook tokens; X authorisation-code + PKCE |

Codes and links are excluded from tenant webhooks. Notification opens sealed values and still accepts plain values from older messages.

---

## 10. Reports and audit

Audit entries for sign-in, 2FA enable / disable (including failures) and lockouts; the Notification history shows delivery status of codes (status Failed locally because no SMTP server is configured).

---

## 11. Security, audit and non-functional requirements

| Area | Requirement |
|---|---|
| Permissions | `settings:system:manage` (platform switch), `settings:tenant:manage` (tenant switch); sign-in itself is anonymous but rate limited. |
| Confidentiality | Secrets sealed at rest (AES secret protector) and redacted after delivery; keys never in tracked configuration. |
| Availability | Rate limiting fails open per replica; token revocation fails open if the store is down (platform invariant). |
| Deployment | `SETTINGS_ENCRYPTION_KEY` required (same for Identity and Notification); `ForwardedHeaders:TrustedHops` set for the real proxy chain; Identity migrations 027 and 028; Notification 012. |
| Testing | Unit tests for the provisioning and linking rules (no real provider tokens); `SwaggerOnlyInDevelopmentTests`. |

---

## 12. UAT scenarios

Verified on the local Docker stack on 2026-10-23 unless marked unit test.

| # | Given / When / Then | Requirement | Result |
|---|---|---|---|
| UAT-IDN-01 | A new external user is provisioned as a Customer; an unverified email matching a user is refused; a verified one links; a client-supplied Admin type is ignored. | IDN-FR-01 … 05 | Unit test (`ExternalLoginAndTwoFactorTests`, in-memory repository); real Google / Microsoft tokens not available |
| UAT-IDN-02 | Email 2FA login: the challenge stores `ad***@…`; the event and the Notification record use the real address (delivery Failed: no SMTP server locally). | IDN-FR-07, 08, 16 | Passed |
| UAT-IDN-03 | After a resend the old code is rejected, the new one works. | IDN-FR-09 | Passed |
| UAT-IDN-04 | 2FA enable / disable without the password fails; with it succeeds. | IDN-FR-10 | Passed |
| UAT-IDN-05 | The delivered code shows `[redacted]` in the outbox while Notification still got it. | IDN-FR-11 | Passed |
| UAT-IDN-06 | A webhook secret is stored as `enc:v1:` and survives an update without a secret. | IDN-FR-12 | Passed |
| UAT-IDN-07 | The 31st sign-in request in a minute is 429, even with a rotating `X-Forwarded-For`. | IDN-FR-13 | Passed |
| UAT-IDN-08 | The client address is the real one behind the two proxies (`TrustedHops` 2), so the per-IP limits are no longer shared by all users. | IDN-FR-14 | Passed (the 429 test with a rotating `X-Forwarded-For` is UAT-IDN-07); separate failed-login windows per user were not measured separately |
| UAT-IDN-09 | Identity Swagger returns 404 outside Development. | IDN-FR-15 | Passed |

---

## 13. Roles and permissions matrix

| Action | Who |
|---|---|
| Sign in / sign up / reset password | anonymous (rate limited) |
| Enable / disable own 2FA | the signed-in user (own account only) |
| Set tenant sign-in options and mandatory 2FA | `settings:tenant:manage` |
| Set platform-wide sign-in options | `settings:system:manage` |

---

## 14. Assumptions and constraints

1. Each tenant has its own host; the tenant of a sign-in is the request host, never a value sent by the client.
2. Providers are reachable from the server; Google / Microsoft ID tokens are verified against their published keys.
3. SMS / email delivery is the deployment's responsibility (SMTP / SMS gateway configured in Notification).
4. Rate limits are per Gateway replica (no shared counter); the number of trusted proxies is configured, not detected.

---

## 15. Out of scope and open issues

* ROADMAP **P0-10**: per-service SQL logins and encrypted DB connections, Notification's own webhook secrets (still plain), SMS phone-number verification.
* **Open:** users created by external login get **no email-confirmation message**; external-provider paths are covered by unit tests only; per-replica rate-limit counters; `TrustedHops` must be set for the Kubernetes ingress chain (the configmap keeps the default 1).
* The local `sa` password is in the repository history and every service connects as `sa` (DEFERRED S1, resolved by P0-10): rotate it. See [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md).

---

## 16. Traceability and change log

| Task | Date | Requirements | Technical documents | Main tests |
|---|---|---|---|---|
| P0-7 external login and 2FA OTP | 2026-10-23 | IDN-FR-01 … 08, 16; IDN-BR-01 … 07 | ExternalAuthAndTwoFactor.md | `ExternalLoginAndTwoFactorTests`, `ExternalLoginRepositoryTests` (Identity) |
| P0-8 security hygiene | 2026-10-23 | IDN-FR-09 … 15; IDN-BR-08 … 13 | SecurityReview-2026-10.md "Phase 2 hardening" | `SwaggerOnlyInDevelopmentTests` (Architecture), `OutboxSecretsTests`, `WebhookSecretTests` (Identity), `SecretEnvelopeTests` (Shared), `GatewayRateLimitingTests` (Gateway) |
| BA baseline (this document) | 2026-10-25 | All | — | — |
