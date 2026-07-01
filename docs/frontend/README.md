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
