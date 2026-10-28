# UI design standard (Web + Mobile)

Status: **proposed standard, document only** (2026-10-28, branch `docs/ui-design-standard`). Nothing under `src/` has changed; the apps look as they did. The standard is applied step by step (see [adoption-roadmap.md](adoption-roadmap.md)), each step its own branch chosen by the owner.

## Why
The four frontends share no design foundation: Admin and Staff use sky blue, Storefront and Customer use rose, and each app re-declares its colours. There is no icon library (emoji everywhere), no shared Modal / Toast / FormField / Select / Tabs / EmptyState / Skeleton; 70 of the 131 Admin pages carry their own copy of the `fixed inset-0` modal markup; 38+ files define their own `money` formatter; the mobile apps have no component folder and hard-code hex colours. The result is an interface that works but feels like four products and is expensive to change.

## Decisions (owner, 2026-10-28)
1. **One token set, two themes.** All apps read the same token names; the *Back-office* theme (blue) is used by Admin and Staff, the *Customer* theme (rose) by Storefront and Customer. Switching a theme changes only the brand tokens.
2. **Lucide icons** replace emoji everywhere (`lucide-react`, `lucide-vue-next`, `lucide-react-native`).
3. **Document first.** This round writes the standard and a visual preview; code follows in small steps.
4. Not in this round: dark mode, a full accessibility audit, a shared money / date helper (rules for them are written, implementation is later).

## Principles
- **Consistent before clever.** One component per job, one name in every framework, the same states everywhere.
- **Back-office is dense and calm; customer apps are airy and confident.** Same tokens, different density (see [foundations.md](foundations.md) "Density").
- **State is never silent.** Every list, form and action has loading, empty, error and success presentation ([patterns-web.md](patterns-web.md) page-state matrix).
- **Mobile is not a small web page.** Thumb reach, 44 pt targets, bottom sheets, safe areas, offline ([patterns-mobile.md](patterns-mobile.md)).
- **Accessible by default for new work** ([accessibility.md](accessibility.md)) — the contrast tables in these documents are computed, not guessed.
- **Reuse before adding.** Existing components (`DataTable`, `ConfirmDialog`, `ReasonDialog`, `StatusBadge`, `ProductPickerModal`) become the base of the library; nothing is thrown away.

## Contents
| Document | What it fixes |
|---|---|
| [foundations.md](foundations.md) | Tokens (colour, type, space, radius, elevation, motion, breakpoints), density, **icons** and the emoji → Lucide map |
| [components.md](components.md) | Spec of every shared component with variants, states and the same props in React / Vue / React Native; mapping to today's code |
| [patterns-web.md](patterns-web.md) | Admin shell, list / form / detail / dashboard pages, Storefront shell and journeys, responsive rules, page-state matrix |
| [patterns-mobile.md](patterns-mobile.md) | Customer and Staff navigation, screens, gestures, offline, keyboard, safe areas |
| [content-and-i18n.md](content-and-i18n.md) | Tone, button verbs, error and empty copy, number / date / currency presentation, key naming |
| [accessibility.md](accessibility.md) | Minimum bar for new work, computed contrast table |
| [adoption-roadmap.md](adoption-roadmap.md) | Steps U1–U7, order, risk, test approach |
| [preview.html](preview.html) | Open in a browser: both themes, type scale, buttons, inputs, badges, cards, a sample list page, a phone mock-up |

Related: [../README.md](../README.md) (app stacks), [../i18n-guide.md](../i18n-guide.md), the Admin CRUD guides.

## How to read the names
- **Token** = a design decision with a name (`--color-brand-action`). Tailwind and NativeWind expose the same names as utilities (`bg-brand-action`).
- **Component** names are identical in the three frameworks (`Button`, `FormField`, `Modal` …); props are identical; only the template syntax differs.
- "Today" = how the code does it now; "Standard" = the rule from now on.

## Changelog
- 2026-10-28 — first version (owner decisions above). Colour contrast computed with a script; two existing pairs fail WCAG AA and are corrected in the standard (Back-office button fill, placeholder / input border) — see [accessibility.md](accessibility.md).
