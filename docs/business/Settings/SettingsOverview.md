# Platform Settings — Business Analysis (Phases 1–3)

**Services:** IdentityService, NotificationService, ProductService, SaleOrderService,
WarehouseService, PaymentService, ShippingService, AccountingService,
SharedMasterDataService
**Databases:** AuthDB, NotificationDB, ProductDB, SalesDB, WarehouseDB, PaymentDB,
ShippingDB, AccountingDB, MasterDataDB

## Purpose

The platform needs a place for SuperAdmin (platform operator) and each tenant's own
Admin to configure how the system behaves, without a code deploy. This was delivered
across three phases as 22 business-requested categories, each landing in the database
of the service that already owns that business domain — there is no separate
"SettingsService" microservice (see `docs/architecture/domain-map.md` for why each
service owns its own database).

**Current scope: storage + CRUD only.** Every category below can be read and written
through the Admin UI and its REST API, and every value is persisted correctly. What is
**not yet true** for any category is runtime behavior actually changing because of the
stored value — see [Known Limitations](#known-limitations-not-yet-wired-to-behavior)
before assuming a toggle "does" anything today.

## Architecture pattern

### System / Tenant two-tier hierarchy

Nearly every category has two rows:

- **System** — one well-known-GUID row per category, editable only by a SuperAdmin
  (`settings:{category}:system:manage`). This is the platform-wide ceiling: whatever
  is off here, no tenant can turn on for themselves.
- **Tenant** — one row per tenant, editable by that tenant's own Admin
  (`settings:{category}:tenant:manage`) or by a SuperAdmin acting on any tenant's
  behalf.

A capability is only actually available when **both** tiers agree
(`System.XEnabled && Tenant.XEnabled`) — this is the same pattern already used by
Identity's pre-existing Auth Settings (`docs/business/Identity/ExternalAuthAndTwoFactor.md`),
which is what every later category copied.

Two categories deliberately break this pattern, for a stated business reason:

- **Company Settings** has no System tier — there is no such thing as a "default
  company" at the platform level, only real per-tenant company profiles.
- **Feature Flags** is not a fixed set of fields at all — it is a dynamic,
  admin-defined key→boolean map (see [Feature Flags](#feature-flags-dynamic-shape)).

### Secrets

Three categories store a third-party credential (Payment's VNPay key, Email's SMTP
password, Integration's external API key). All three follow the same rule: the
plaintext value is accepted once from the admin form, encrypted with
`ERP.Shared.Security.AesSecretProtector` before being written to the database, and
**never returned** by any GET endpoint — the API only ever reports
`has{Secret}: true/false`. Sending an empty/omitted value on update means "leave the
stored secret unchanged," never "clear it."

### Permission model

Every category uses the same four-permission shape:

```
settings:{category}:system:view
settings:{category}:system:manage
settings:{category}:tenant:view
settings:{category}:tenant:manage
```

A caller may always manage their own tenant's row. Managing another tenant's row, or
any System row, additionally requires the platform-wide `settings:system:manage`
override (IdentityService) or the equivalent `settings:{category}:system:manage`
override (other services) — one SuperAdmin grant is enough to administer every
category, not one grant per category.

## Category reference (all 22, by phase)

| # | Category | Owning service | Gateway route | Tiers | Business purpose |
|---|---|---|---|---|---|
| 1 | General | IdentityService | `/id/api/settings/general` | System+Tenant | Platform/tenant maintenance-mode switch, default language fallback, site name, time zone, date format |
| 2 | Company | IdentityService | `/id/api/settings/company` | Tenant only | Tenant's company profile (legal name, tax code, address, logo, website) |
| 3 | User & Security | IdentityService | `/id/api/settings` (extends Auth Settings) | System+Tenant | Password complexity policy, login lockout threshold/duration, session timeout — extends the pre-existing Auth Settings entity rather than being a new one |
| 4 | Workflow | IdentityService | `/id/api/settings/workflow` | System+Tenant | Generic multi-level approval-chain configuration (approval levels, auto-approve threshold) |
| 5 | Notification | NotificationService | `/ntf/api/settings/notification` | System+Tenant | Per-tenant default channel availability (email/SMS/push), distinct from each user's own `NotificationPreference` opt-in/out |
| 6 | Email | NotificationService | `/ntf/api/settings/email` | System+Tenant | SMTP host/port/credentials, from-address — meant to eventually replace `appsettings.json`'s `Email:*` block |
| 7 | SMS | NotificationService | `/ntf/api/settings/sms` | System+Tenant | SMS gateway provider, API key, from-number — meant to eventually replace `Twilio:*` config |
| 8a | Store/Product | ProductService | `/prd/api/settings/store-product` | System+Tenant | Whether displayed prices include tax by default, product reviews on/off, store name/description/logo, out-of-stock product visibility |
| 8b | Cart/Checkout | SaleOrderService | `/so/api/settings/cart-checkout` | System+Tenant | Guest checkout allowed, cart hold duration, forced registration at checkout, number of checkout steps |
| 9 | Order | SaleOrderService | `/so/api/settings/order` | System+Tenant | Custom order-number format permission, order number prefix, auto-cancel unpaid orders after N hours, post-submit edit permission |
| 10 | Inventory | WarehouseService | `/wh/api/settings/inventory` | System+Tenant | Negative stock permission, low-stock threshold, backorder permission, default reservation hold days |
| 11 | Payment | PaymentService | `/pay/api/settings/payment` | System+Tenant | VNPay/manual-bank-transfer platform availability, VNPay enablement + **encrypted** secret key, sandbox mode |
| 12 | Shipping | ShippingService | `/shp/api/settings/shipping` | System+Tenant | Custom-carrier permission, default shipping fee, free-shipping threshold |
| 13 | Tax | AccountingService | `/acc/api/settings/tax` | System+Tenant | Regional tax configuration permission, company tax code, default tax rate %, prices-include-tax toggle |
| 14 | Currency | SharedMasterDataService | `/mst/api/settings/currency` | System+Tenant | Multi-currency permission, default currency code, exchange-rate source, decimal digits |
| 15 | Localization | IdentityService | `/id/api/settings/localization` | System+Tenant | Deeper i18n than General: supported locale list, number/currency format locale, RTL layout — the supported-language *catalog* itself stays owned by SharedMasterDataService |
| 16 | File Storage | IdentityService | `/id/api/settings/file-storage` | System+Tenant | Storage provider selection, max upload size, CDN base URL — no actual file-storage abstraction exists yet to read this |
| 17 | Scheduler | IdentityService | `/id/api/settings/scheduler` | System+Tenant | Background-jobs kill switch, Outbox polling interval, max retry attempts — config for the real per-service `OutboxProcessor` jobs, not yet read by them |
| 18 | Integration | IdentityService | `/id/api/settings/integration` | System+Tenant | Third-party integration endpoint + **encrypted** API key, sync on/off — distinct from NotificationService's real `WebhookEndpoint` outbound-webhook feature |
| 19 | API Settings | IdentityService | `/id/api/settings/api` | System+Tenant | API-key feature availability, default/tenant rate limits, max active keys per tenant — config for the existing `ApiKey`/`ApiKeysController` feature, does not re-model it |
| 20 | Feature Flags | IdentityService | `/id/api/settings/feature-flags` | System+Tenant (dynamic) | Admin-defined named toggles with per-tenant override — see below |
| 21 | Audit | IdentityService | `/id/api/settings/audit` | System+Tenant | Audit-logging kill switch, retention days, which event categories to log — config for the existing `SecurityAuditLog`/`SecurityAuditService`, does not duplicate it |
| 22 | Advanced | IdentityService | `/id/api/settings/advanced` | System+Tenant | Catch-all: debug-mode permission/toggle, custom CSS URL, data-export permission |

### Feature Flags (dynamic shape)

Every other category is a fixed set of typed fields. Feature Flags is the one
deliberate exception: a SuperAdmin can define **any** flag key at the System tier
(`{key, isEnabled, description}`), and a tenant Admin can override just that key's
effective value for their own tenant without affecting anyone else. A tenant that has
never overridden a key simply inherits the System value. Nothing in the codebase
checks a flag's value to gate behavior yet — this is a pure key/value store today.

## Business rules

1. **Tenant cannot be weaker than the System security baseline** (User & Security
   only): saving a tenant's password/lockout/session policy that is weaker than the
   SuperAdmin-configured System baseline (shorter `MinPasswordLength`, higher
   `MaxLoginAttempts`, shorter `LockoutDurationMinutes`, longer
   `SessionTimeoutMinutes`, or turning off a System-required character class) is
   rejected with a `BusinessRuleException`. This is input validation, not runtime
   behavior wiring — `PasswordPolicyService`/`User.RecordLoginFail()` still don't read
   from these tables (see below).
2. **A tenant is created with one default row per category** — every migration
   backfills a default Tenant Settings row for every existing tenant, so a lookup
   never 404s; new tenants get their default row lazily on first read
   (`GetOrCreate{X}SettingsEntityAsync`, present in every service's Settings service
   class).
3. **Secrets are write-only** — see [Secrets](#secrets) above. This applies to
   Payment's VNPay key, Email's SMTP password, and Integration's external API key.
4. **Company Settings has no System tier** — Tenant-only, by design (see
   [Architecture pattern](#architecture-pattern)).
5. **A SuperAdmin's `settings:system:manage` (or, in non-Identity services, the
   category's own `settings:{category}:system:manage`) authorizes cross-tenant access
   to every category** — this was a deliberate choice to avoid needing a separate
   SuperAdmin grant per category as more categories were added across three phases.

## Known limitations (not yet wired to behavior)

Storage and CRUD are complete and correct for all 22 categories; the following real
runtime code paths still read their **old** hard-coded sources, not these tables:

- `PasswordPolicyService` still reads `PasswordPolicy:*` from `appsettings.json`.
- `User.RecordLoginFail()` still hard-codes a 5-attempt lockout.
- NotificationService's `SmtpEmailSender`/`TwilioSmsSender` still read
  `Email:*`/`Twilio:*` from `appsettings.json`, not Email/SMS Settings.
- Every service's `OutboxProcessor` still polls on its own hard-coded interval, not
  Scheduler Settings' `OutboxPollingIntervalSeconds`.
- `ApiKeysController`/Ocelot Gateway rate limiting do not read API Settings.
- `SecurityAuditService` logs unconditionally; it does not check Audit Settings'
  toggles or honor `RetentionDays`.
- No feature-flag check anywhere in the codebase reads `Feature Flags` at all.
- Store/Product's `priceDisplayIncludesTaxByDefault`, Cart/Checkout's
  `guestCheckoutAllowed`, Order's auto-cancel/custom-format rules, Inventory's
  negative-stock/backorder rules, Shipping's default fee/free-shipping threshold, and
  Tax's default rate — none of these are consulted by their services' real order/cart/
  inventory/shipping/tax logic yet.

Wiring any of the above into real behavior is explicitly the next body of work, not
part of Phases 1–3.

## Phase history

- **Phase 1** — 8 Ecommerce/Order-adjacent pages (categories 8a, 8b, 9–14), one per
  owning service (Product, SaleOrder ×2, Warehouse, Payment, Shipping, Accounting,
  SharedMasterData). Established the System/Tenant two-tier pattern and the
  encrypted-secret pattern (Payment's VNPay key).
- **Phase 2** — General, Company, User & Security (all IdentityService) + Notification,
  Email, SMS (all NotificationService). User & Security extended the pre-existing
  Auth Settings entity instead of creating a new one; Company dropped the System tier;
  Email/SMS reused Phase 1's secret-encryption pattern verbatim.
- **Phase 3** — the remaining 9 categories, all IdentityService: Workflow, File
  Storage, Scheduler, Integration, API Settings, Audit, Advanced, Localization (fixed
  System/Tenant shape) + Feature Flags (dynamic key/value shape).

## See also

- [Settings Admin Guide](../../frontend/settings-admin-guide.md) — how to use every
  Settings page in the Admin app, and how to add a new category following this same
  pattern.
- `docs/business/Identity/ExternalAuthAndTwoFactor.md` — the original System/Tenant
  hierarchy (Auth Settings) that every later category's shape is modeled on.
- `docs/architecture/database-map.md` — which database each service (and therefore
  each category's tables) lives in.
