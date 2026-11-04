# ERP Admin mobile app (`src/frontend/mobile/admin`)

The back office on a phone: **Home (Dashboard), Purchasing, Sales, HR**. First release: read, approve, reject, cancel. Nothing is created or edited on the phone. Screens: `docs/frontend/mobile-admin-ui-elements.md`; business view: `docs/business/Mobile/AdminApp.md`; user guide (Vietnamese): `docs/user-guides/mobile-admin.md`.

Stack as the other two apps: Expo SDK 51, expo-router, NativeWind role tokens, zustand, react-query, i18next (en / vi), Jest. `components/ui` is a byte copy of the staff app's (`test/uiParity.test.ts`); admin-only parts are in `components/`.

## Layout
| Path | What |
|---|---|
| `tenant.config.js` | pure function `resolveTenantConfig(env)`: tenant code, API address, app name, Android package, brand colour |
| `app.config.js` | writes the tenant into `extra.tenant`, name, package; cleartext only outside production |
| `lib/tenant.ts` | reads `extra.tenant` (`tenantConfig`) - the one place that knows the tenant |
| `lib/jwt.ts` | reads (never verifies) the token claims: permissions, licensed modules, name |
| `lib/secureStorage.ts` | the refresh token in the keystore (`expo-secure-store`), every call tolerates failure |
| `api/auth.ts`, `api/client.ts` | auth calls (plain axios), `apiClient` (`/api/...`) and `gatewayClient` (`/po/...`, `/hrm/...`); 401 -> one shared refresh -> retry once -> else sign out |
| `stores/auth.store.ts` | `booting / signedOut / signedIn`; access token in memory only; `hasPermission` reads the token, never a role name |
| `modules/` | the module registry and one folder per module |
| `components/` | `ApprovalBar`, `PagedListView`, `DetailState`, `StatusBadge`, `FilterChips`, `SegmentedControl`, `KpiCard`, `RevenueSparkline` ... |
| `app/` | expo-router files; each is a one-line re-export of a module screen |

## Tenant build (one code base, one APK per tenant)
The tenant is the request **host**; the Gateway resolves it, so the only switch is the API address. Variables (env or `eas.json`): `TENANT_CODE` (default `SYSTEM`), `EXPO_PUBLIC_API_URL`, `APP_NAME`, `BRAND_COLOR`, `APP_VARIANT` (`preview` allows cleartext http for the LAN stack, `production` does not).

| Tenant | Address | Android package | App name |
|---|---|---|---|
| SYSTEM (default) | `http://admin.erp.local/api` | `com.erp.admin` | ERP Admin |
| `glcheck` | `http://glcheck.erp.local/api` | `com.erp.admin.glcheck` | ERP Admin - glcheck |

```
powershell -ExecutionPolicy Bypass -File src\frontend\mobile\tools\build-apk-local.ps1 -App admin                       # SYSTEM
powershell -ExecutionPolicy Bypass -File src\frontend\mobile\tools\build-apk-local.ps1 -App admin -Tenant glcheck -ApiUrl http://glcheck.erp.local/api
```
Output `src/frontend/mobile/apk/admin-<tenant>-preview.apk` (git-ignored); different tenants install side by side. The address is baked in at bundle time, so a tenant with its own domain needs its own build. Emulator: `start-emulator.ps1 -HostName glcheck.erp.local` (once per host) maps the host to the PC (`10.0.2.2`); the Windows hosts file is never edited.

## Sign-in
`POST /api/v1/auth/login {email, password, userType: 2}` (the Admin web's user type). `requiresTwoFactor` opens the code step (`2fa/verify {challengeId, code}`, `2fa/resend`). The access token (about 10 KB, it lists every permission) stays in memory; the **refresh token** is the only secret kept (keystore). At start the app refreshes once; a refused token is forgotten, an unreachable server keeps it. Sign out clears both and tells the server.

## Navigation and notifications
- **Header** (tab screens): menu button (`components/MenuButton`), title, bell (`components/NotificationBell`), tenant chip.
- **Side menu** `components/SideMenu` is mounted once in `app/_layout.tsx` while signed in; `stores/menu.store.ts` holds open / closed. It is our own `Modal` + `Animated` panel: no drawer library and no native dependency (a drawer library needs gesture-handler and a new APK). It lists `useVisibleModules().visible`, the inbox, language, tenant / server / version and Sign out.
- **Bottom bar** is `components/ModuleTabBar` (a custom `tabBar` of the one `Tabs` navigator in `app/(tabs)/_layout.tsx`, which registers every screen file the registry names). `barFor(route, modules, permissions)` (`modules/visible.ts`) decides what it shows: on Home one item per module (opening a module lands on `moduleLanding`, its first section the user may open); inside a module Home (the way back) plus that module's sections. At most `MAX_BAR_ITEMS` (5) items; the rest sit behind "More" (on Home it opens the side menu, inside a module a sheet listing the remaining sections). There is no More screen.
- **Notifications** `features/notifications/`: `api.ts` (Gateway `/ntf/api/v1/notifications`), `useUnreadCount` (react-query, polling every 60 s, paused in the background by `focusManager` wired to `AppState` in `app/_layout.tsx`), `NotificationsScreen` (route `app/notifications/index.tsx`). Needs `notifications:view`; without it there is no bell and no call.

## Modules and the registry
`modules/index.ts` lists every `ErpModule` (`modules/types.ts`). A user sees a module in the menu (and, for the first three, in the tab bar) when the token holds **one of its permissions** (an empty list = everybody) and, if the token lists licensed modules at all, the module's code is among them. The first 4 visible modules are tabs (`app/(tabs)/_layout.tsx` builds the tab bar from the registry and hides the rest with `href: null`); the others are listed on **More**.

**Sections.** An `ErpModule` can list `sections` (`ModuleSection`: id, titleKey, icon, route, permissions, order). A section is one business function with one screen file `app/(tabs)/<route>.tsx`; the first section's route is the module route. **Add a section:** (1) add the screen (a component in the module folder) and the one-line route file; (2) add the entry to the module's `sections` and its permission to the module `permissions` (a test keeps them equal); (3) add `sections.<name>` to en + vi. `test/modules.test.ts` fails when a route file or a translation is missing or a route repeats.

## List filters
`hooks/useListFilters.ts` holds the filters of one list (status, period or custom range, search text debounced 400 ms) and returns `params` (`status`, `dateFrom`, `dateTo`, `search`: empty values left out) for the request and the react-query key. `components/ListFilters.tsx` draws the search box, the Filters button (number of active filters), the summary chips and the sheet (status chips, period segments with previous / next, From / To `DateField`s, Reset / Done). `lib/periods.ts` turns a month / quarter / year into a from-to pair. Options: `statuses` (chips), `noDates`, `noSearch`, `kinds`. **Use it:** `const filters = useListFilters({ status?, kind? })`, `usePagedList([key, filters.params], page => api.list(filters.params, page))`, `<ListFilters filters={filters} searchLabel=... statuses=... />` in the `header` of `PagedListView`. Lists the server answers whole use `onePage`; arrays without a total use `pageOfArray` (`modules/usePagedList.ts`).

**Add a module** (e.g. Warehouse):
1. `modules/warehouse/` with `api.ts` (calls through `gatewayClient`/`apiClient`; normalise the server's paged shape to `{items,total}`), the screens, and `index.ts` exporting an `ErpModule` (`id`, `titleKey`, `icon`, `route`, `permissions`, `licensedModule`, `order`, optional `attention` lines for Home).
2. Add it to `MODULES` in `modules/index.ts`.
3. Add `app/(tabs)/<route>.tsx` (and detail routes under `app/<module>/`) as one-line re-exports; register detail routes in `app/_layout.tsx`.
4. Add the i18n keys (en + vi). `test/modules.test.ts` fails when the route file or the title translation is missing, or ids / routes / orders repeat.
5. Use `PagedListView` for lists, `DetailState` + `ApprovalBar` for records, `StatusBadge` (add the status -> tone table).

## API map (all through the tenant host)
| Screen | Calls | Permission |
|---|---|---|
| Home | `GET /api/v1/reports/sales?from&to` | `reports:sales:view` |
| Purchasing | `GET /po/api/v1/purchase-orders?status&pageNumber&pageSize` (bare array today), `/{id}`, `POST .../approve`, `finance-approve`, `reject {reason}`, `cancel` | `purchase_order:read / approve / finance_approve / cancel` |
| Sales | `GET /api/v1/orders?status&page&pageSize` -> `{total,items}`, `/{id}`, `POST .../approve`, `reject {reason}`, `cancel {reason}` | `sales_order:read / approve / cancel` |
| HR | `GET /hrm/api/v1/hrm/employees`, `leave-requests` (+ `approve`, `reject {reason}`, `cancel`), `attendance` | `hrm:employees:view`, `hrm:leave:manage`, `hrm:attendance:view` |

Which buttons show is decided by status **and** permission (`poActions`, `soActions`, `leaveActions`); the server checks both again. Every action goes through one confirm / reason sheet (`ApprovalBar`): nothing runs on the first tap, reject / cancel need a reason, a refusal is shown in the server's own words.

## Tests and checks
`npm test` (Jest: tenant config, token, auth store, refresh queue, registry, actions, screens, translations parity, no raw colours, UI parity), `npx tsc --noEmit`, `npm run check:tokens`. Not covered by tests: a real device / TalkBack run, the two-step code against a real account with 2FA.
