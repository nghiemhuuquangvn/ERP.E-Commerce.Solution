# Accessibility — the minimum bar for new work

Target: **WCAG 2.2 level AA** for everything built or touched under this standard. A full audit of the existing 131 Admin pages, Storefront and the mobile apps is *not* part of this round (future step U7); this document fixes the rules and the colour maths so new work starts right.

## 1. Contrast (computed with `contrast.js`, WCAG relative luminance)
Text needs 4.5:1 (3:1 for ≥ 24 px or ≥ 19 px bold); boundaries of controls and meaningful icons need 3:1.

| Pair | Ratio | Result |
|---|---|---|
| `text` #111827 on white / on page #f9fafb | 17.74 / 16.98 | AAA |
| `text-secondary` #4b5563 on white | 7.56 | AAA |
| `text-muted` #6b7280 on white / on page | 4.83 / 4.63 | AA (min 14 px) |
| white on Back-office action **#0369a1** / hover #075985 | 5.93 / 7.56 | AA |
| white on Back-office #0284c7 (**today's button**) | **4.10** | **fails AA for 14 px text → replaced** |
| white on Back-office #0ea5e9 | 2.77 | fails — decoration only |
| Back-office action #0369a1 on `brand-soft` #e0f2fe | 5.17 | AA |
| `brand-soft-text` #0c4a6e on #e0f2fe | 8.24 | AAA |
| white on Customer action #e11d48 / hover #be123c | 4.70 / 6.29 | AA |
| Customer #e11d48 on `brand-soft` #ffe4e6 | **3.91** | large text / icons only; use #be123c (5.24) for small text |
| `brand-soft-text` #881337 on #ffe4e6 | 7.97 | AAA |
| white on success #15803d / warning #b45309 / danger #dc2626 | 5.02 / 5.02 / 4.83 | AA |
| soft texts: success #166534 on #dcfce7 / warning #92400e on #fef3c7 / danger #991b1b on #fee2e2 / info #075985 on #e0f2fe | 6.49 / 6.37 / 6.80 / 6.59 | AA |
| Sidebar text #d1d5db on #111827 | 12.04 | AAA |
| Placeholder **#9ca3af** on white (**today**) | **2.54** | **fails → placeholder uses `text-muted` #6b7280 (4.83)** |
| Control border **#d1d5db** on white (**today**) | **1.47** | **fails 3:1 → `border-strong` #6b7280 (4.83)** |

Consequences adopted in [foundations.md](foundations.md): Back-office action is `#0369a1`; placeholders `#6b7280`; control borders `#6b7280`; small brand-coloured text on Customer soft backgrounds uses `#be123c`; `brand-accent` is decoration only. Decorative borders (cards, table rows) may stay `#e5e7eb`.

## 2. Rules (checklist for review)
1. **Focus visible:** every interactive element shows the 2 px `brand-action` ring with 2 px offset; never `outline: none` without a replacement. Focus order = reading order; modals trap focus and return it.
2. **Keyboard:** everything usable without a mouse; menus, tabs, comboboxes follow the WAI-ARIA authoring patterns (arrow keys, Home / End, Esc); a skip-to-content link is the first focusable item on web pages.
3. **Names:** every control has an accessible name; icon-only buttons have `aria-label` / `accessibilityLabel` and a tooltip on web; images have `alt` (empty for decoration); form controls use real `<label>` (web) or `accessibilityLabel` (mobile).
4. **Structure:** landmarks, one `h1`, headings in order, lists are lists, tables have header cells with `scope` and `aria-sort` when sortable.
5. **Forms:** errors are text (not only red), linked with `aria-describedby`, announced (`role="alert"`) on submit; required fields marked in text and `aria-required`; autocomplete attributes on address / email / phone fields; do not clear input on error.
6. **Colour is never the only signal:** statuses show text, charts have labels or patterns, required / error also use an icon or text.
7. **Targets:** 24 × 24 CSS px minimum on web with spacing (44 on touch devices), 44 × 44 pt on mobile.
8. **Motion:** nothing flashes more than 3 times a second; `prefers-reduced-motion` and the OS setting replace slide / scale by fade; auto-moving content has a pause control.
9. **Text:** works at 200 % zoom (web) and 130 % OS font scale (mobile) without clipping or horizontal scroll; line length ≤ 80 characters for prose.
10. **Live updates:** toasts use `role="status"` (`alert` for errors); loading regions use `aria-busy`; route changes move focus to the new `h1`.
11. **Time limits:** session expiry warns before it happens and lets the user extend it; payment pages show the remaining time in text.

## 3. Testing for new work
- Automated: `eslint-plugin-jsx-a11y` (web React), `axe-core` in component tests (Vitest), `eslint-plugin-vuejs-accessibility` (Storefront); in the pull request description, the checklist above ticked for the pages touched.
- Manual (per page, 5 minutes): tab through with the keyboard only, zoom to 200 %, turn on a screen reader on one flow (NVDA / VoiceOver / TalkBack for mobile), check reduced motion.
- Contrast of any new colour pair is added to the table in §1 by re-running `contrast.js` (kept in the adoption step U1).

## 4. Not covered yet (future U7)
Dark mode and its contrast table; audit of existing pages; Vietnamese screen-reader pronunciation review; captions / transcripts for future video; high-contrast mode.
