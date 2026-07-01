# i18n Guide (all 4 frontend apps)

Two locales exist today: `en` (default) and `vi`. This covers **UI-string**
translation (buttons, labels, messages). It does not cover translated **content**
(Product name/description in the DB) — that's a separate backend-only mechanism, see
`src/Shared/Core/Domain/TranslationEntity.cs` and ProductService's
`ProductTranslation`/`CategoryTranslation`.

## Libraries

- Admin, Mobile Customer, Mobile Staff: `i18next` + `react-i18next`
- Storefront: `vue-i18n` v9

## Where things live (per app)

| Concern | Admin | Storefront | Mobile (customer & staff) |
|---|---|---|---|
| i18n bootstrap | `src/i18n/index.ts` | `src/i18n/index.ts` | `i18n/index.ts` |
| Resource files | `src/i18n/locales/{en,vi}.json` | `src/i18n/locales/{en,vi}.json` | `i18n/locales/{en,vi}.json` |
| Locale persisted store | `src/stores/ui.store.ts` (`locale` field) | `src/stores/locale.store.ts` (Pinia) | `stores/locale.store.ts` (Zustand + AsyncStorage) |
| Switcher UI | `src/components/layout/Header.tsx` (`<select>`) | `StorefrontLayout.vue` header | Customer: Profile tab. Staff: Dashboard header (no profile tab exists) |
| `Accept-Language` injection | `src/api/client.ts` axios interceptor | `src/api/client.ts` axios interceptor | `api/client.ts` axios interceptor (both apps) |

`ACCEPT_LANGUAGE_MAP = { en: 'en-US', vi: 'vi-VN' }` (or equivalent) lives next to
each `client.ts` interceptor — it is duplicated per app on purpose (no shared JS
package exists between the 4 apps), so when adding a locale, update all 4 copies.

## Resource key structure

Flat, feature-namespaced JSON, one file per locale, mirrored 1:1 (every key in
`en.json` must exist in `vi.json`, and vice versa):

```jsonc
{
  "common": { "save": "...", "cancel": "...", "loading": "..." },
  "nav": { "dashboard": "...", "orders": "..." },
  "auth": { "signIn": "..." },
  "<feature>": { "title": "...", "...": "..." },
  "masterdata": {
    "departments": { "title": "...", "failedToSave": "..." },
    "positions": { "...": "..." },
    "employees": { "...": "..." }
  }
}
```

Rules:
- Namespace by page/feature (`orders`, `products`, `masterdata.departments`, ...),
  not by component — multiple components on the same page share a namespace.
- Interpolation uses `{{placeholder}}` (i18next) — e.g.
  `"showingRange": "Showing {{from}}–{{to}} of {{total}}"`,
  `"terminateConfirm": "Are you sure you want to terminate \"{{name}}\"?..."`.
  Vue's `vue-i18n` also accepts `{name}` — check the existing Storefront resource
  file for the syntax actually in use there before adding new interpolated strings.
- Don't create a translation key for a string that's never rendered (no
  speculative/future keys).

## Adding a new translated screen or master-data entity page

1. Add the feature's keys to **both** `en.json` and `vi.json` under a new or
   existing namespace — never add a key to only one locale file.
2. In the component, `const { t } = useTranslation()` (React apps) /
   `const { t } = useI18n()` (Vue), replace literal strings with `t('namespace.key')`.
3. If the page is genuinely new (e.g. a new Admin Master Data entity), also see
   [masterdata-admin-guide.md](masterdata-admin-guide.md) for the non-i18n scaffolding
   (API module, route, sidebar entry, permission string).
4. Run the app's typecheck/build (`npm run build` for Admin/Storefront,
   `npx tsc --noEmit` for the mobile apps) — none of the 4 apps fail the build on a
   missing key at compile time (keys are just strings), so a missing key silently
   renders the raw key at runtime. Grep the new component for stray string literals
   as a manual check instead of relying on the build to catch it.

## Known limitation

Both mobile apps have a pre-existing (not i18n-related) NativeWind v4 `className`
typing gap that surfaces as TS errors on every screen. It predates this i18n work,
is unrelated to translation keys, and was intentionally left unresolved as
out-of-scope. Don't attempt to "fix" it as part of an i18n change — it needs a
dependency-resolution investigation of `react-native-css-interop`, not a translation
fix.
