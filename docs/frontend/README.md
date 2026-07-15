# Frontend Apps

Four independent apps live under `src/frontend/`. None of them call each other; each
talks only to the Ocelot Gateway (`http://localhost:5000`) over REST.

| App | Path | Stack | Audience |
|---|---|---|---|
| Admin | `src/frontend/admin` | React 18 + TypeScript + Vite + Tailwind + React Router v6 + Zustand + React Query + react-hook-form | Back-office staff (orders, products, inventory, payments, reports, master data) |
| Storefront | `src/frontend/storefront` | Vue 3.5 + TypeScript + Vite + Tailwind + Vue Router v4 + Pinia + Vue Query | End customers (web) |
| Mobile Customer | `src/frontend/mobile/customer` | Expo ~51 + React Native 0.74.5 + Expo Router + React Query + Zustand + NativeWind | End customers (iOS/Android) |
| Mobile Staff | `src/frontend/mobile/staff` | Expo ~51 + React Native 0.74.5 + Expo Router + React Query + Zustand + NativeWind | Field/warehouse/retail staff (iOS/Android) |

## Guides

- [i18n-guide.md](i18n-guide.md) — how language switching + `Accept-Language`
  propagation works in all 4 apps, and how to add a new translation key or a new
  language.
- [masterdata-admin-guide.md](masterdata-admin-guide.md) — the pattern used for the
  Admin app's SharedMasterDataService CRUD pages (Departments, Positions, Employees,
  Units of Measure, Geography, Languages), for adding a new master-data entity page
  without re-deriving the shape from scratch.
- [settings-admin-guide.md](settings-admin-guide.md) — all 22 Settings categories
  (General, Company, Auth & Security, Notification/Email/SMS, Store/Product,
  Cart/Checkout, Order, Inventory, Payment, Shipping, Tax, Currency, Localization,
  File Storage, Scheduler, Integration, API, Feature Flags, Audit, Advanced): how to
  use each page in the Admin app, and how to add a new category following the same
  System/Tenant pattern. Business analysis: `docs/business/Settings/SettingsOverview.md`.

## Auth: login, 2FA, SSO, and settings (Admin app)

- `src/pages/auth/LoginPage.tsx` collects `tenantCode` alongside email/password (a
  pre-existing gap — the login form never collected it before, even though the
  backend's `LoginRequest` always required it) and queries `GET
  /v1/auth/login-options?tenantCode=` to know which SSO buttons to render.
- `src/hooks/useAuth.ts`'s `useCompleteLogin()` is the single shared "store tokens +
  user, navigate to dashboard" step — every login path (password, 2FA-verified,
  every SSO provider) must route through it rather than duplicating the
  token-storage logic. `tenantId` is decoded once from the JWT `tenant_id` claim at
  this point (via `jwt-decode`) and stored on `auth.store.ts`'s `User` — don't
  re-decode the JWT elsewhere, read `user.tenantId` instead.
- `LoginResponse.requiresTwoFactor` short-circuits every login path to
  `/login/2fa?challengeId=...&method=...` instead of completing login — see
  `src/pages/auth/TwoFactorPage.tsx`.
- `src/pages/settings/AuthSettingsPage.tsx` is the template for any future
  System/Tenant toggle-style settings page (as opposed to master-data CRUD — see
  `masterdata-admin-guide.md` for that different pattern): two tabs, each gated by
  its own `settings:{scope}:view`/`:manage` permission pair, read-only render when
  the caller only has `:view`. See [settings-admin-guide.md](settings-admin-guide.md)
  for the full 22-category reference and the checklist for adding a new one.
- SSO client libraries: `@react-oauth/google` (Google), `@azure/msal-browser` +
  `@azure/msal-react` (Microsoft), the Facebook JS SDK loaded via a script tag
  (`src/lib/facebookSdk.ts`, no npm package), and a plain redirect for Twitter/X (no
  client library — it's a server-driven OAuth Authorization Code + PKCE flow, see
  `docs/business/Identity/ExternalAuthAndTwoFactor.md`). Provider client IDs are
  Vite env vars (`VITE_GOOGLE_CLIENT_ID`, `VITE_MSAL_CLIENT_ID`, `VITE_MSAL_AUTHORITY`,
  `VITE_FACEBOOK_APP_ID`) — placeholders only until real app registrations exist.

## User & Role Management (Admin app)

- `src/pages/identity/UsersPage.tsx` and `RolesPermissionsPage.tsx` — CRUD/management
  UI for IdentityService's Users/Roles/Permissions/Tenants, gated on the hierarchical
  `identity:{resource}:{action}` permission codes (see
  `docs/business/Identity/Permissions.md`/`docs/business/ERP-Business-Analysis.md` §3.1
  for the backend tenant-isolation model these pages assume). File-layout convention
  borrowed from `masterdata-admin-guide.md` (one `.api.ts` per entity mirroring the real
  controller + one page combining list/modals), but the data source is IdentityService,
  not SharedMasterDataService — don't reuse `mstClient.ts`.
- Tenant selector on both pages only renders for callers holding
  `identity:tenants:view` (re-verified server-side against the DB, never trusted from
  the JWT alone) — everyone else only ever sees their own tenant's data.
- `permissions.api.ts` is deliberately **list-only** — `PermissionsController`'s
  Create/Update/Delete never persist (a known backend bug, not yet fixed), so the
  frontend ships a read-only permission catalog instead of CRUD against a broken
  endpoint.

## Conventions that apply to all 4 apps

- **No synchronous app-to-app or app-to-non-gateway calls.** Every app talks only to
  the Gateway; the Gateway routes to services. This mirrors the backend's own
  "no direct cross-service calls" rule (see root `CLAUDE.md`).
- **Locale persistence**: each app has exactly one persisted store for UI preferences
  (Admin: `src/stores/ui.store.ts` Zustand; Storefront: `src/stores/locale.store.ts`
  Pinia; Mobile: `stores/locale.store.ts` Zustand + AsyncStorage in each app). Add new
  UI-preference fields to that existing store — don't create a second one.
- **`Accept-Language` header**: injected by each app's axios request interceptor in
  its `api/client.ts`, mapping the app's short locale (`en`/`vi`) to the backend's
  actual supported culture codes (`en-US`/`vi-VN`). This mapping must stay in sync
  with each backend service's `Localization:SupportedCultures` config
  (`src/Shared/Localization`, see root `CLAUDE.md`).
- **Two locales today**: `en` and `vi`. Adding a third locale means updating the
  mapping in every app's `client.ts` *and* every backend service's
  `appsettings.json` `Localization:SupportedCultures` — it is not purely a frontend
  change.
