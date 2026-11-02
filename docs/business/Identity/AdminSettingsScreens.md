# Settings screens and sign-in pages — Admin

Business analysis of the **25 Settings screens** and the **3 sign-in pages** of the Admin as shipped on 2026-11-02 (ROADMAP P2-9, U3 part 2; closes DEFERRED R92 and, for the Admin, R69 / R71). This change moved the screens onto the component library and the role colours; **no setting, endpoint, payload or permission changed**. The rules behind each setting are in the domain documents (Identity, Notification, Payment, Shipping, Accounting ...); users and roles are in [AdminBackOffice.md](AdminBackOffice.md); the licence in [License.md](License.md).

## 1. Purpose and scope
Let the platform operator and each shop's administrator read and change configuration in one consistent place, and sign in safely.
- **System tab** (platform operators): what the platform makes available to every shop (the master switches). **Tenant tab**: the shop's own values; a tenant value only takes effect when the platform switch allows it (the two-tier rule of Identity).
- Out of scope: the notification history / templates / webhooks screens ([notification-*](../Notification/WebhooksAndTemplates.md)) and the carrier / delivery-fee rule cards (already on the library in U3 batch 6); changing what a setting does.

## 2. Actors and permissions
| Actor | Permission | Allows |
|---|---|---|
| Platform operator | `settings:<area>:system:view` / `:manage` | see and change the System tab of an area |
| Shop administrator | `settings:<area>:tenant:manage` | change the shop's own values (the Tenant tab) |
| Authentication settings | `settings:system:view|manage`, `settings:tenant:manage` | sign-in providers, two-factor, password and session rules |
| Licence | `license:*` (platform), tenant licence view / issue | modules, plans, a tenant's licence |
| Anyone without the permission | — | the page says "You do not have permission"; with view but not manage every control is disabled and there is no Save |

## 3. The screens
Area (`<area>` in the permission) and what each one controls (labels as shown):
| Screen | What it controls |
|---|---|
| General | Maintenance mode, default language code, site name, time zone, date format |
| Company | Company name, legal name, tax code, address, phone, email, logo URL, website |
| Localization | Multi-language, default locale, supported locales, number / currency format locale, right-to-left |
| Currency | Multi-currency, default currency code, exchange-rate source, decimal digits |
| Tax | Regional tax, company tax code, default tax rate, prices include tax, e-invoice provider and series |
| Store / product | Prices include tax by default, product reviews, store name / description / logo, show out-of-stock |
| Email | Allow custom SMTP; host, port, user, password (sealed), from address / name |
| SMS | Allow custom gateway; provider, API key (sealed), from number |
| Notification | Which channels exist platform-wide; which are on by default for the shop |
| Integration | External integrations platform-wide; base URL, API key (sealed), sync |
| API | API keys platform-wide; rate limits per minute; maximum active keys |
| File storage | Custom storage provider; maximum upload size; provider; CDN base URL |
| Payment | Gateways available platform-wide; the shop's own switch and secret key, sandbox, cash on delivery; the **online payment accounts** cards (VNPay / MoMo / ZaloPay) |
| Shipping | Custom carriers; the shop's pickup address and flat fee / free-shipping threshold; the **carrier accounts** and **carrier rules** and **delivery-fee rules** cards |
| Cart / checkout | Guest checkout, cart hold time, registration at checkout, number of steps |
| Order | Order number format / prefix, auto-cancel of unpaid orders, editing after submission |
| Inventory | Negative stock, low-stock threshold, backorders, default hold period |
| Workflow | Multi-level approval, default levels, auto-approve threshold |
| Scheduler | Background jobs, outbox polling interval, retry attempts |
| Audit | Audit logging, retention, log successful / failed sign-ins, permission changes |
| Advanced | Debug mode, custom CSS URL, data export |
| Authentication | Google / Microsoft / Facebook / Twitter sign-in, two-factor, minimum password length, lockout, session timeout |
| Feature flags | Platform flags (System) and the shop's overrides (Tenant) |
| Licence | Modules, plans, a tenant's licence (issue, renew, suspend, revoke) |

## 4. Rules
- **Sealed values** (SMTP password, SMS / integration / gateway keys, carrier secrets) are written, never read back: the field shows "Not set" or "set — leave blank to keep"; saving with it empty keeps the stored value (the page never sends a blank overwrite).
- A field the platform has switched off for shops stays editable only on the System tab; on the Tenant tab it is disabled with its reason.
- **Feature flags:** changing a **platform** flag asks first and says it applies to every shop that does not override it; a shop's own override is one click (it affects only that shop). Licence suspend / revoke already asked first.
- After a successful save the page says **"Saved."** (toast); a refusal shows **the server's own words** in an alert above the Save button (a rule broken, a value out of range, a gateway key rejected); nothing fails silently.
- **Sign-in:** e-mail and password are required (the browser explains an empty field); a refused sign-in shows "Invalid email or password." as an alert; a code-based second step takes the 6-digit code (the field is announced as a one-time code) and can resend after a 60-second wait; the provider callback page shows a status while it finishes or the error with a button back to sign-in.

## 5. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| ST-1 | One look for every setting | Every control is a library control (labelled, with a linked hint or error), colours come from the role tokens; a test fails the build on a raw palette class or the old numeric colour aliases |
| ST-2 | Saving is acknowledged | Save sends exactly one request, then "Saved."; the button shows progress while it runs |
| ST-3 | Refusals are explained | The server's text appears in an alert; the form keeps what was typed |
| ST-4 | Read-only is clear | Without manage permission all inputs are disabled and Save is absent |
| ST-5 | Sealed values stay sealed | An empty sealed field is never sent; the "set / not set" hint is shown |
| ST-6 | Platform flags ask first | Toggling a System flag opens a confirmation naming the key and the consequence; Esc leaves it unchanged |
| ST-7 | Tabs are keyboard-usable | System / Tenant tabs follow the tablist pattern (arrow keys, roving focus) |
| ST-8 | Sign-in is accessible | Labelled required fields, alert for a refusal, no axe violation, one-time-code autocomplete on the second step |
| ST-9 | Readable | Every text / background pair on these screens is at least 4.5:1 (the muted text colour was darkened to guarantee it) |

## 6. Use cases
**UC-1 Change a shop value.** Settings → area → Tenant tab → edit → Save → "Saved." *Exception:* the platform has switched the feature off → the control is disabled; *refusal:* the alert shows the server's reason.
**UC-2 Rotate a gateway / SMTP secret.** Open the screen, type the new key in the sealed field, Save; leave it blank to keep the old one.
**UC-3 Switch a platform feature.** Settings → Feature flags → System → toggle → read the consequence → Confirm.
**UC-4 Sign in with a second step.** E-mail and password → code page → type the code → (Resend after 60 s if it did not arrive).
**UC-5 Sign in with a provider.** Provider button → the provider's page → the callback page shows "completing sign-in" and lands in the Admin; a failure shows the reason and a Sign in button.

## 7. Data and integrations
Each screen reads and writes its own settings endpoint of the owning service through the Gateway (`api/settings/*` clients); settings changes are published as `SettingsChanged` for the services that cache them. No endpoint or event changed in this work.

## 8. UAT scenarios
1. Open every Settings route as a platform operator and as a shop administrator: no screen shows raw grey / blue / red classes, each has a title, tabs work with the arrow keys.
2. General → Tenant → change Site name → Save → "Saved." → reload → value kept → restore.
3. As a reader (view only) open Company: inputs disabled, no Save.
4. Email → Tenant → leave the password blank → Save: the stored password is unchanged.
5. Feature flags → System → toggle → confirmation names the key; Esc keeps it.
6. Sign out, sign in with a wrong password → alert; with the right one → Dashboard.

## 9. Assumptions and open issues
- The live check saved one throw-away value (General › Site name, restored); the other screens were opened and tested by unit tests with a mocked API (secrets and external test-send are not exercised live).
- The module name "AI ChatBot — Claude AI (Premium)" is stored in the database with a mis-encoded dash (seed data); not a UI issue (DEFERRED R105).
- Storefront and the mobile apps still use their own numeric colour aliases (R69 remains open for them).
