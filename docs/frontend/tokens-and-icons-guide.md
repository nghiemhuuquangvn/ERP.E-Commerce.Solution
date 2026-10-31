# Tokens and icons in the four apps (U1)

Done 2026-10-31 (branch `feat/ui-u1-tokens-icons`). Standard: [design-system/foundations.md](design-system/foundations.md). Read this before writing a colour, a button or an icon in any app.

## 1. Where the values live
- **One source:** [design-system/tokens/tokens.json](design-system/tokens/tokens.json) (neutral, semantic, the two brand themes, z-index layers, radii, icon size / stroke).
- **Generated copies** (committed, never edited by hand): `node docs/frontend/design-system/tools/build-tokens.js`.

| App | Theme | Files written |
|---|---|---|
| Admin | back-office | `src/styles/tokens.css`, `src/styles/tokens.ts`, `tokens.preset.cjs` |
| Storefront | customer | the same three files |
| Customer app | customer | `theme/theme.ts`, `tailwind.tokens.js` |
| Staff app | back-office | `theme/theme.ts`, `tailwind.tokens.js` |

- **Check:** `npm run check:tokens` in each app (= `build-tokens.js --check`) fails when a copy differs from the source, when a required contrast pair falls below its ratio (white on action 4.5, soft text on soft 4.5, control border 3, placeholder 4.5 …), or when a `#rrggbb` literal sits in app code (`src` / `app` / `components`, outside the generated files). Run it before every commit that touches colours; it is part of the definition of done.
- Change a colour: edit `tokens.json`, run the generator, commit the source and the four copies together.

## 2. Using the roles
Tailwind class names (the standard's `border` is `line`, `text` is `ink`, the neutral status role is `muted`):

| Need | Class |
|---|---|
| Primary button | `bg-brand-action text-on-brand hover:bg-brand-action-hover` (or `.btn-primary` / `.btn-brand`) |
| Selected row / soft badge | `bg-brand-soft text-brand-soft-text`; very light tint `bg-brand-subtle` |
| Page, card | `bg-page`, `bg-surface` + `border-line`; control border `border-line-strong` |
| Text | `text-ink`, `text-ink-secondary`, `text-ink-muted` (placeholder, timestamps) |
| Status | `bg-success-soft text-success-soft-text`, same for `warning`, `danger`, `info`, `muted`; solid `bg-success`, `bg-danger hover:bg-danger-hover` |
| Charts (cannot use a class) | `import { colors } from '@/styles/tokens'` → `colors.brandAccent`, `colors.line`, `colors.success` |
| Mobile JS props (tab bar, ActivityIndicator) | `import { colors, icon } from '@/theme/theme'` |

A hex in a component is a review finding and fails `check:tokens`. `brand-accent` is for decoration only (charts, illustrations), never text.

## 3. Compatibility aliases (temporary)
Existing pages still use the old numeric names; the presets map them to the roles so nothing had to be mass-edited: Admin `primary-50 / 100 / 500 / 600 / 700` → subtle / soft / action / action / action-hover; Storefront and Customer app `brand-100 / 500 / 600` → soft / action / action-hover; Staff `primary-50 / 500 / 600`. **Do not use them in new code.** They go away page by page in U3 (DEFERRED R69). `primary-300` and `primary-400` exist in one Admin page each and have no role; they stay unstyled as before.

## 4. Icons (Lucide)
- Packages: Admin `lucide-react`, Storefront `lucide-vue-next`, mobile `lucide-react-native` + `react-native-svg`. Import by name (`import { Truck } from 'lucide-react'`), never the whole set.
- Size 20 in nav and buttons, 16 inline / child items, 24 tab bar and Storefront header, 40-64 for empty placeholders; stroke **1.75** (`icon.stroke` on mobile); the icon inherits the text colour. Decorative: `aria-hidden="true"`; an icon-only control needs an accessible name (`aria-label`, and `title` while the Admin sidebar is collapsed).
- **Admin sidebar:** `NavItem.icon` is a `LucideIcon` component; add an item with `icon: Truck`. The emoji → icon choices are in foundations.md §6.1.
- Typographic glyphs inside translated sentences (`→ ← ✓ ★`) and emoji typed by users are not icons and stay (DEFERRED R70).

## 5. Checklist for a UI change
Role classes only (no hex, no new alias use) · Lucide icon with a name where it stands alone · `npm run check:tokens` · `tsc -b` / `vue-tsc -b` · both languages for any new text.

Next layer: [components-guide.md](components-guide.md) (U2) - the components that use these roles.
