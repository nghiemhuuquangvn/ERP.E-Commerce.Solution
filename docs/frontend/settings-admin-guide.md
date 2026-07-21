# Settings pages — Admin app guide

All 22 Settings categories (see `docs/business/Settings/SettingsOverview.md` for the
business analysis) follow one of two UI patterns in the Admin app. This guide covers
**using** the existing pages and **adding a new category** to either pattern.

## Using the Settings pages

### Navigation and permissions

All 24 categories live as children of a single collapsible **Settings** group in the
sidebar (`Sidebar.tsx`, `SETTINGS_CHILDREN` array) rather than as flat top-level items
— this keeps the main menu short. Each child is gated on the
`settings:{category}:{system|tenant}:view` permission pair — a user who holds neither
permission never sees that child at all, and the group itself only renders if at least
one child is visible. The group auto-expands when the current route is under
`/settings/*`. Inside a page:

- The **System** tab only renders if the caller holds
  `settings:{category}:system:view`; it defaults to hidden for ordinary tenant Admins.
- The **Tenant** tab always renders for the caller's own tenant. Fields are read-only
  (all inputs `disabled`) unless the caller also holds
  `settings:{category}:tenant:manage`.
- **Company Settings** and **Feature Flags** are the two exceptions to this System/
  Tenant tab shape — see [Category-specific pages](#category-specific-pages) below.

| Category | Nav path | System permission | Tenant permission |
|---|---|---|---|
| General | `/settings/general` | `settings:general:system:*` | `settings:general:tenant:*` |
| Company | `/settings/company` | — (no System tier) | `settings:company:tenant:*` |
| Auth & Security | `/settings/auth` | `settings:system:*` | `settings:tenant:*` |
| Workflow | `/settings/workflow` | `settings:workflow:system:*` | `settings:workflow:tenant:*` |
| Notification | `/settings/notification` | `settings:notification:system:*` | `settings:notification:tenant:*` |
| Email | `/settings/email` | `settings:email:system:*` | `settings:email:tenant:*` |
| SMS | `/settings/sms` | `settings:sms:system:*` | `settings:sms:tenant:*` |
| Store/Product | `/settings/store-product` | `settings:store-product:system:*` | `settings:store-product:tenant:*` |
| Cart/Checkout | `/settings/cart-checkout` | `settings:cart-checkout:system:*` | `settings:cart-checkout:tenant:*` |
| Order | `/settings/order` | `settings:order:system:*` | `settings:order:tenant:*` |
| Inventory | `/settings/inventory` | `settings:inventory:system:*` | `settings:inventory:tenant:*` |
| Payment | `/settings/payment` | `settings:payment:system:*` | `settings:payment:tenant:*` |
| Shipping | `/settings/shipping` | `settings:shipping:system:*` | `settings:shipping:tenant:*` |
| Tax | `/settings/tax` | `settings:tax:system:*` | `settings:tax:tenant:*` |
| Currency | `/settings/currency` | `settings:currency:system:*` | `settings:currency:tenant:*` |
| Localization | `/settings/localization` | `settings:localization:system:*` | `settings:localization:tenant:*` |
| File Storage | `/settings/file-storage` | `settings:file-storage:system:*` | `settings:file-storage:tenant:*` |
| Scheduler | `/settings/scheduler` | `settings:scheduler:system:*` | `settings:scheduler:tenant:*` |
| Integration | `/settings/integration` | `settings:integration:system:*` | `settings:integration:tenant:*` |
| API Settings | `/settings/api` | `settings:api:system:*` | `settings:api:tenant:*` |
| Feature Flags | `/settings/feature-flags` | `settings:feature-flags:system:*` | `settings:feature-flags:tenant:*` |
| Audit | `/settings/audit` | `settings:audit:system:*` | `settings:audit:tenant:*` |
| Advanced | `/settings/advanced` | `settings:advanced:system:*` | `settings:advanced:tenant:*` |

(`*` = both `:view` and `:manage` — manage additionally required to submit the form.)

### Configuring a secret-bearing category (Payment, Email, Integration)

The password-style field (`vnPaySecretKey`, `smtpPassword`, `externalApiKey`) always
renders **empty** on load, regardless of whether a secret is already stored — the
`has{Secret}` boolean from the GET response only drives the placeholder text
(`"•••••••• (set — leave blank to keep)"` vs `"Not set"`). Leaving the field blank and
saving keeps the previously stored secret; typing a new value replaces it. There is no
way to view a previously-saved secret through the UI — this is intentional.

### Configuring Feature Flags

Feature Flags is the one category with no fixed fields:

1. On the **System** tab, a SuperAdmin adds a new flag via the "Add flag" form (key +
   description + initial enabled state) or toggles an existing flag's checkbox.
2. On the **Tenant** tab, every System-defined flag appears with its effective value.
   Toggling a tenant's checkbox creates (or updates) that tenant's own override row —
   the "Overridden"/"Inherited" column shows whether the tenant has diverged from the
   System default.
3. There is currently no delete/deprecate action for a flag — once created it stays
   in the System list.

## Category-specific pages

- **`CompanySettingsPage.tsx`** has no tab bar at all (no System tier) — it renders the
  Tenant form directly.
- **`AuthSettingsPage.tsx`** is the original template (`src/pages/settings/
  AuthSettingsPage.tsx`) that every later System/Tenant page copied; User & Security's
  Phase 3 fields (password policy, lockout, session timeout) were added to this same
  page rather than a new one, because they extend the existing `SystemAuthSettings`/
  `TenantAuthSettings` backend entities.
- **`FeatureFlagsPage.tsx`** replaces the form-per-tab layout with a table + inline
  "add flag" form — see [Configuring Feature Flags](#configuring-feature-flags).

## Adding a new Settings category

Follow this checklist end-to-end; every step references a real existing file to copy
from. Steps 1–8 are backend (repeat once per service if the category needs one on
each side, though in practice every category to date lives in exactly one service).

### Backend (example uses IdentityService; other services follow the same shape
minus the repository/UnitOfWork layer where the service doesn't have one — see
NotificationService's `EmailSettingsService.cs` for the lighter DbContext-direct
variant)

1. **Domain entities** — `System{X}Settings`/`Tenant{X}Settings`, copy
   `SystemWorkflowSettings.cs`/`TenantWorkflowSettings.cs`. Pick a **new** well-known
   GUID prefix for the System row (grep existing entities for `WellKnownId =
   Guid.Parse` first to avoid a collision). Extend `BaseEntity`; use a standalone POCO
   only if the category is a dynamic list like Feature Flags (see `SystemFeatureFlag.cs`).
2. **DTOs** — one file, System/Tenant × Dto/UpdateRequest records, copy
   `WorkflowSettingsDto.cs`. If a field is a secret, the DTO exposes `Has{Secret}: bool`
   only (see `IntegrationSettingsDto.cs`).
3. **Repository interfaces + impls** — `I{X}SettingsRepository`/`{X}SettingsRepository`
   pairs, copy `ISystemWorkflowSettingsRepository.cs` +
   `SystemWorkflowSettingsRepository.cs`. Add the two new properties to
   `IUnitOfWork.cs` and `UnitOfWork.cs` (constructor param, field assignment, property
   declaration — three edits, same file each time).
4. **Service interface + impl** — `I{X}SettingsService`/`{X}SettingsService`, copy
   `WorkflowSettingsService.cs`. This is where the lazy-create-on-read
   (`GetOrCreate{X}SettingsEntityAsync`) pattern lives — every category needs both a
   System and Tenant version of this private method.
5. **Permissions class** — `{X}SettingsPermissions.cs`, copy
   `WorkflowSettingsPermissions.cs` (4 constants:
   `settings:{x}:{system|tenant}:{view|manage}`).
6. **Controller** — `{X}SettingsController.cs`, copy `WorkflowSettingsController.cs`
   verbatim except the route prefix and DTO/permission types — every controller in
   this family is structurally identical (`CallerCanAccessTenantAsync` helper
   included).
7. **Wire it up** — add the repo/service registrations to `DependencyInjection.cs`
   (mirrors the existing block exactly) and the entity `DbSet`s +
   `modelBuilder.Entity<>()` config to `AuthDbContext.cs`.
8. **Migration SQL** — new numbered file in `src/Database/Identity/` (check the
   highest existing number first), following `010_AddPhase3FixedSettingsCategories.sql`'s
   shape: guarded `CREATE TABLE`, a well-known-GUID System row insert, a
   backfill-one-Tenant-row-per-existing-tenant insert, and 4 new permission rows
   granted to SuperAdmin (all 4) + Admin (the tenant-scoped 2).

Build (`dotnet build`) after step 7 — every category in Phases 1–3 compiled clean on
the first try once the pattern was copied exactly, so a compile error at this point
usually means a step was skipped, not that the pattern itself is wrong.

### Frontend

9. **API client** — `src/api/settings/{x}.settings.api.ts`, copy
   `workflow.settings.api.ts`. `SETTINGS_BASE` must match the new controller's Gateway
   route prefix (`/id/api/...`, `/ntf/api/...`, `/prd/api/...`, etc. — see
   `docs/architecture/port-map.md` for every service's Gateway prefix).
10. **Page** — `src/pages/settings/{X}SettingsPage.tsx`, copy
    `WorkflowSettingsPage.tsx` for the standard tab shape, `CompanySettingsPage.tsx`
    for a Tenant-only category, or `FeatureFlagsPage.tsx` for a dynamic key/value
    category.
11. **Wire it up** — import + route in `router/index.tsx`, a new entry appended to the
    `SETTINGS_CHILDREN` array in `Sidebar.tsx` (with the `permissions: [...]` array
    gating visibility — do **not** add it as a separate top-level `NAV_ITEMS` entry),
    and the `settings.{x}.*`/`nav.{x}Settings` keys in **both** `i18n/locales/en.json`
    and `vi.json`.
12. **Verify** — `npx tsc --noEmit`; the only pre-existing, unrelated failures you
    should ever see are `LogsPage`/`logs.api` (a known gap, out of scope for Settings
    work).

Do **not** wire the new category's values into real runtime behavior as part of
adding it — every category to date is storage+CRUD only (see
`docs/business/Settings/SettingsOverview.md#known-limitations-not-yet-wired-to-behavior`).
That is deliberately a separate, later piece of work.
