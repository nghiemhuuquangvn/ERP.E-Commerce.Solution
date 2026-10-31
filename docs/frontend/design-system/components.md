# Components

One library, three implementations (React for Admin, Vue for Storefront, React Native for the two apps). **Names, props and states are identical**; only the syntax differs. Tokens come from [foundations.md](foundations.md). Today's code that each component builds on is listed so adoption is incremental.

## Conventions
- **Props:** `variant`, `size` (`sm | md | lg`), `disabled`, `loading`, `error`, `id`, `className` / `class` / `style`, `aria-*` / `accessibilityLabel`. Callbacks `onX` (React / RN), events `x` (Vue).
- **States every interactive component defines:** default, hover (web), pressed (mobile), focus-visible (2 px ring in `brand-action`, 2 px offset), disabled (50 % opacity, no pointer events, `aria-disabled`), loading (spinner replaces the icon, width does not change, click ignored), error (danger border and message).
- **Never** build a modal, toast or select inline in a page: use the component.
- Text is passed already translated (`t('…')`); components own no strings except accessible names for their own controls (close, next page) which use `common.*` keys.

## Actions
### Button
Variants: `primary` (brand-action fill, white text), `secondary` (neutral soft fill), `outline` (border-strong, transparent), `danger` (danger fill), `ghost` (text only, for toolbars), `link`. Sizes `sm` 32 / `md` 36 / `lg` 44 (Storefront default `lg`; mobile `md` = 48). Props: `leftIcon`, `rightIcon`, `loading`, `fullWidth`, `iconOnly` (requires `aria-label`). One `primary` per view region. Label = verb + object ("Save promotion"), never "OK".
Today: classes `.btn`, `.btn-primary`, `.btn-outline`, `.btn-danger` (Admin), `.btn-brand`, `.btn-outline` (Storefront), repeated `TouchableOpacity className="bg-brand-500 rounded-xl py-4"` (mobile). Adoption: first make the classes read the tokens (no page change), then introduce the component.

### IconButton / MenuButton
Icon-only `Button` with tooltip; `MenuButton` opens a menu of actions ("…" in table rows). Destructive items last, in danger colour, separated.

## Inputs
### FormField
Wraps any control: `label`, `hint`, `error`, `required`, `children`. Layout: label above, control, hint or error below (error replaces hint). Required marks with `*` and `aria-required`. The error is linked with `aria-describedby`; on submit failure focus moves to the first invalid field. This is the single place validation text is rendered (today every page writes `<p className="mt-1 text-xs text-red-600">`).

### Input, Textarea, NumberInput, MoneyInput, SearchInput
Height per density; border `border-strong`; placeholder `text-muted` (never carries the label); focus ring `brand-action`; error border `danger`. `NumberInput` aligns right; `MoneyInput` formats per [content-and-i18n.md](content-and-i18n.md) and returns a number; `SearchInput` has a leading `Search` icon and a clear button, debounce 300 ms.
Today: `.input` class (Admin and Storefront), raw `TextInput` (mobile).

### Select, Combobox, MultiSelect
`Select` for ≤ 7 fixed options (native on mobile: action sheet). `Combobox` for long or remote lists (type to filter, async loading state, empty text, keyboard: arrows / Enter / Esc). Replaces ad-hoc `<select>` and the picker modals where only one id is needed. `ProductPickerModal` stays for rich multi-select of products.

### Checkbox, Radio, Switch
24 × 24 hit area on web, 44 on mobile; label is clickable; `Switch` for immediate settings (with toast on result), `Checkbox` for form choices.

### DatePicker, DateRangePicker, TimePicker
Web: native input with the standard look until a calendar popover is adopted; mobile: platform picker in a bottom sheet. Value is ISO date; display follows the content rules. Range pickers are used in all report and list filters (today two separate `<input type="date">`).

## Containers and display
### Card
`surface`, `border`, radius per theme, padding 16 (compact) / 24. Optional `header` (title + actions), `footer`. Today `.card`.

### Badge / StatusBadge
`Badge` (`variant` = success | warning | danger | info | neutral | brand, `size`, optional dot or icon). `StatusBadge` takes a status name and the translated label and picks the role from the table in foundations §1.2 (one map, shared). Always text — colour alone never conveys status.
Today: `StatusBadge.tsx` with a long colour table, `.badge` class.

### Tabs
Underline tabs on web (`role="tablist"`, arrow-key navigation), segmented control or top tabs on mobile. Tab content is lazy; the active tab is in the URL (`?tab=`) on web so refresh and links work.

### EmptyState
Icon (48, `text-muted`), title, one-sentence reason, optional primary action ("Add the first terminal"). Variants: `empty` (no data yet), `no-results` (filters hide everything, action = clear filters), `error` (load failed, action = retry), `forbidden` (missing permission, no action). Replaces plain "No records found".

### Skeleton, Spinner, ProgressBar
`Skeleton` mimics the final layout (table rows, cards, product tiles) with a 1.2 s shimmer (static under reduced motion); full-page `Spinner` only for route transitions; inline `Spinner` 16 in buttons. Loading text is a last resort.

### Avatar, Chip, Tooltip, Breadcrumb
Avatar: image or initials on `brand-soft`, sizes 24 / 32 / 40. Chip: removable filter value. Tooltip: web only, 500 ms delay, also on keyboard focus. Breadcrumb: Admin detail pages, max 4 levels, last item not a link.

## Overlays
### Modal (base) and Drawer
One `Modal` base: `open`, `title`, `onClose`, `size` (`sm` 400 / `md` 560 / `lg` 800), `footer` slot, scroll inside the body, focus trap, `Esc` closes, backdrop click closes only when the form is clean, returns focus to the trigger, locks page scroll. `Drawer` is the same from the right edge (details, filters on tablets).
`ConfirmDialog` and `ReasonDialog` (already shared) become thin wrappers over this base; every `fixed inset-0` block in the 70 pages is replaced by `Modal` (adoption U3).
Mobile: `BottomSheet` (drag handle, snap points half / full, dims the screen, hardware back closes) replaces `Modal` for choices and forms; a centred `Alert` only for confirmations.

### Toast / Alert / Banner
`Toast`: transient result of an action (success 4 s, error stays until closed, max 3 stacked, top-right web, above the tab bar on mobile, `role="status"` / `alert` for errors). `Alert`: inline, persistent, inside a card or form (info, warning, danger). `Banner`: page-wide (offline, trial expiring, scheduled maintenance). Replaces the 11 `alert()` calls and the inline red paragraphs for action results (field errors stay in `FormField`).

## Data
### DataTable
Spec of the existing component, extended: sticky header, column alignment (numbers right), sortable header with `aria-sort`, row selection, row actions in a trailing `MenuButton`, loading = `Skeleton` rows, empty = `EmptyState`, fixed row height 44, horizontal scroll with a shadow edge, responsive: below `md` rows turn into stacked cards on Storefront account pages, stay a scrolling table in Admin. Keeps the `columns` + `data` + `isLoading` props.
### Pagination
Existing component; shows range ("21–40 of 133"), page size 20 / 50 / 100, keyboard accessible. The server contract is `{ items, total, page, pageSize }`; lists that still return plain arrays page on the client.
### Stat / KPI card
Label, value (`2xl/700`, `tabular-nums`), delta chip (success / danger), optional sparkline (`brand-accent`).
### Charts
Recharts (web) with the semantic and `brand-accent` colours; max 6 series; every chart has a text summary or table alternative; axis text `xs text-secondary`.

## Mapping from today's code
| Standard | Today | First step |
|---|---|---|
| Button | `.btn*` classes, inline mobile touchables | classes read tokens; then component |
| FormField, Input | `.input` + inline error `<p>` | FormField component used by new pages |
| Modal | `fixed inset-0` block in 70 pages; `ConfirmDialog`, `ReasonDialog` | build `Modal`, rebase the two dialogs, migrate pages in batches by area |
| Toast | `alert()` ×11, inline paragraphs | `ToastProvider` in the app shell |
| StatusBadge | `StatusBadge.tsx` colour table | map statuses to roles once |
| DataTable, Pagination | exist | add skeleton / empty / sort props, keep API |
| EmptyState | "No records found" text | default for `DataTable` |
| Select / Combobox / Tabs / DatePicker | native elements, ad-hoc | new components, adopted per page |
| Storefront components | `ProductCard.vue`, `ChatWidget.vue` only | add Vue versions of the library |
| Mobile components | none | add under `components/` (the Tailwind `content` globs already point there) |
