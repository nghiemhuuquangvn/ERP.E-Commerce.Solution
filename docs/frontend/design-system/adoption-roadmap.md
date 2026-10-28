# Adoption roadmap

The standard is applied in small steps, **one branch each, started only when the owner chooses it** (backend work stays first in priority). Every step keeps the apps working and shippable; no step is a rewrite. Test approach for all steps: `tsc -b` / `vue-tsc`, existing unit tests, a Vitest test for each new component (states, keyboard, `axe-core`), a screenshot comparison of the pages touched against the previous look, and a browser walk-through of the main journey.

| Step | Branch (proposed) | Scope | Done when | Risk | Closes |
|---|---|---|---|---|---|
| **U1 Tokens + icons** | `feat/ui-u1-tokens-icons` | `tokens.css` + Tailwind preset per web app and `theme.ts` + NativeWind preset per mobile app, generated from [foundations.md](foundations.md); `.btn*`, `.input`, `.card`, `.badge` read tokens (Back-office action → `#0369a1`, placeholder / control border corrected); `data-theme` set per app; Lucide installed; emoji in sidebar, headers and tab bars replaced using the map; a test that fails when the token copies drift; `contrast.js` kept in `docs/frontend/design-system/tools/` | All four apps render with tokens, no hex left in shells | Visual shift of one shade for Admin buttons; bundle +~20 kB (tree-shaken icons) | DS-1, DS-3 |
| **U2 Core components (Admin)** | `feat/ui-u2-core-components` | `Button`, `FormField` + `Input/Select/Textarea`, `Modal` (base) + rebased `ConfirmDialog` / `ReasonDialog`, `Toast`, `EmptyState`, `Skeleton`, `Badge` + shared status map; used first by the P2-1a Promotion and POS pages (newest, least risk) | Pilot pages migrated, library documented with usage examples | Two ways of doing a modal until U3 | DS-2 |
| **U3 Replace copy-pasted modals** | `refactor/ui-u3-modals-<area>` (one per area) | Replace the `fixed inset-0` markup in the 70 pages by `Modal`, `alert()` by `Toast`, inline errors by `FormField`; areas in order: Accounting, HRM, Sales / Purchasing, Warehouse, Master data, Settings, Identity, rest | No `fixed inset-0` outside `Modal`; no `alert(` | Largest volume; mitigated by area batches and screenshots | DS-4 |
| **U4 Storefront** | `feat/ui-u4-storefront` | Vue versions of the library, shell (header, footer, category bar), product list / detail, cart, checkout steps, account area, auth pages; pairs naturally with ROADMAP P2-2 (reset password, confirm e-mail, coupon UI, tracking, address book) | Journeys match [patterns-web.md](patterns-web.md) §3 | Vue component parity with React | DS-5 |
| **U5 Mobile** | `feat/ui-u5-mobile` | `components/` in both apps (Button, Input, FormField, Card, Badge, Toast, EmptyState, Skeleton, BottomSheet), theme, tab bars with Lucide, safe areas, offline banner, accessibility labels; Staff scan and POS screens; aligns i18next / react-i18next versions with web | Customer and Staff match [patterns-mobile.md](patterns-mobile.md) | Native build and device testing needed (Expo) | DS-6 |
| **U6 Shared formatting** | `feat/ui-u6-formatting` | `formatMoney / formatNumber / formatDate / formatDateTime / formatRelative` per platform per [content-and-i18n.md](content-and-i18n.md); remove the 38+ local `money` helpers and 124 `toLocale*` calls | Single formatter per app, lint rule forbids new local ones | Rounding / locale differences visible in reports | DS-7 |
| **U7 Dark mode + a11y audit** | `feat/ui-u7-dark-a11y` | Dark token set with computed contrast, theme switch (system / light / dark), audit of the 131 Admin pages, Storefront and mobile against [accessibility.md](accessibility.md), fixes | Audit report with zero level-A failures | Charts and images in dark | DS-8 |

Order: U1 → U2 → (U3 in parallel batches) → U4 with P2-2 → U5 → U6 → U7. U1 and U2 are the minimum to stop the drift; U3 onward can be paused without harm.

## Definition of done for any UI change after U1
Tokens only (no hex), components from the library, Lucide icons, the five page states handled, strings in both languages, a11y checklist ticked, screenshots attached, the BA document and the frontend guide updated.

## Open decisions for the owner
1. Whether to create a shared workspace package (monorepo with `packages/ui-tokens`) in U1 or keep copied files with a drift test (recommended: copied files first; a workspace is a bigger tooling change).
2. Whether the Storefront keeps Vue + a Vue component library or shares logic through a framework-neutral package later.
3. Brand shades for tenants: whether shops may pick their own Customer-theme colour (white-label) — the token roles make this a configuration, not a redesign; not planned now.
4. Design tool: this standard is document-first; a Figma file mirroring the tokens can be added when a designer joins.
