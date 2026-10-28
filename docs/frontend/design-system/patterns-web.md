# Web patterns (Admin and Storefront)

Page recipes built from [components.md](components.md) with the tokens of [foundations.md](foundations.md). Admin uses the **Back-office** theme, Storefront the **Customer** theme.

## 1. Page-state matrix (every data region)
| State | Presentation | Never |
|---|---|---|
| Loading first time | `Skeleton` shaped like the content (table rows / cards) | blank page, bare "Loading…" |
| Loading again (refetch) | keep old data, thin `ProgressBar` under the header | clear the table |
| Empty (no data yet) | `EmptyState empty` + the creating action when permitted | an empty table frame |
| No results (filters) | `EmptyState no-results` + "Clear filters" | "No records found" |
| Error | `EmptyState error` with the reason and **Retry**; field errors in `FormField` | `alert()`, silent failure |
| No permission | `EmptyState forbidden` (menu already hides the entry) | a blank screen |
| Saving | button `loading`, form disabled, no double submit | letting the user click twice |
| Saved | `Toast success`, modal closes, list refreshes | leaving the modal open without feedback |

## 2. Admin

### 2.1 Shell
- **Sidebar** 256 px (collapsed 64 with tooltips), `surface` on dark-neutral background `#111827`, groups collapsible, item = icon 20 + label, active item `brand-action` fill with white text, group labels `xs uppercase text-muted`. Order of groups follows the business flow: Dashboard · Sales · Purchasing · Products · Inventory · Payments · Shipping · Promotions · POS · HR · Accounting · Master data · Reports · Settings · Monitoring · Users. Below `lg` the sidebar becomes a `Drawer` opened by a menu button.
- **Header** 56 px: menu button (tablet), breadcrumb, global search, notification bell (`Bell` + count badge), language switch, tenant name, user menu (profile, sign out).
- **Content** max width 1440, padding 24, page title row (title left, primary action right) then optional description, then the body.

### 2.2 List page
Title row → **filter bar** (SearchInput, Selects, DateRangePicker, "Clear filters" ghost button; filters live in the URL query) → `DataTable` → `Pagination`. Row click opens the detail; row actions in a `MenuButton`. Bulk actions appear in a bar above the table after selection. Export is a secondary button at the right of the filter bar. Reference: Promotions, POS Transactions, Shipments pages.

### 2.3 Create / edit
- **Short forms (≤ 8 fields)** in a `Modal md`: `FormField`s in a 2-column grid on ≥ `md`, footer with Cancel (outline) left of the primary Save. Validation on blur and on submit; first invalid field is focused.
- **Long forms** are a page with sections (`Card` each), a sticky footer action bar, an "unsaved changes" prompt on leave.
- Destructive actions use `ConfirmDialog` (danger, names the object); irreversible or financial actions use `ReasonDialog`.

### 2.4 Detail page
Header (title, `StatusBadge`, key facts, action bar with the primary next step), `Tabs` (Overview, Lines, History, Documents …), side `Card` for related objects. Status timeline for documents (order, shipment, return). Actions available depend on status and permission and are hidden, not disabled, when the user lacks the permission; disabled with an explanation tooltip when the state forbids them.

### 2.5 Dashboard
Row of `Stat` cards (4 max per row), then 2 charts, then a "needs attention" list (low stock, orders to approve, failed shipments). Date range at the top; every figure is clickable to its list.

### 2.6 Settings
`/settings` is a card grid grouped by area (General, Company, Notifications, Shipping, Payments …) with search; each card = icon, title, one-line description, permission-gated. The same pages remain reachable from the sidebar.

### 2.7 Master-data CRUD
Keep the pattern in `docs/frontend/masterdata-admin-guide.md` / `hrm-admin-guide.md` (list + modal form + deactivate); the standard changes only the pieces (components, states, copy).

## 3. Storefront

### 3.1 Shell
Header 64: logo, search (expands to full width on mobile), account, cart with count badge; category bar below on ≥ `lg`, menu `Drawer` below. Footer: help, policies, language, payment methods. Sticky "Add to cart" bar on product pages on mobile. `ChatWidget` bottom-right, 56 px, does not cover the sticky bar.

### 3.2 Journeys
| Page | Layout rules |
|---|---|
| Home | hero (`brand-subtle`), category tiles, product rows (horizontal scroll on mobile), promotions strip |
| Product list | filters left (≥ `lg`) / `BottomSheet`-style drawer (mobile), grid 2 / 3 / 4 columns, sort select, `Pagination` or "load more", `Skeleton` tiles |
| Product detail | gallery left, info right (stacked on mobile): price (700), stock note, variant selectors (chips), quantity, primary `Button lg` "Add to cart", tabs: description, specs, reviews |
| Cart | line list with quantity steppers, coupon input (`FormField` + Apply, result `Alert`), summary card (subtotal, discount, delivery fee, total), checkout button |
| Checkout | 3 steps with a progress indicator: Delivery (address book, ward, province select) → Payment (methods the shop enables: VNPay, MoMo, ZaloPay, COD) → Review; summary card stays visible; errors per step |
| Payment result | one status icon, short explanation, order number, next actions (view order, continue shopping); polling state with spinner while pending |
| Order tracking | status timeline, parcels (several parcels listed with tracking number each), return request button when allowed |
| Account | left menu (Orders, Addresses, Profile, Security, Loyalty points) → stacked list on mobile; order history rows → cards on mobile |
| Auth | single centred card, social buttons above e-mail form, errors under the field, reset password and confirm e-mail pages share the layout |

### 3.3 Trust and money
Prices `font-bold tabular-nums`, strike-through original price in `text-muted`, discount in `danger`; free delivery notice in `success`; totals always show the delivery fee line; the pay button states the amount ("Pay 1.234.567 ₫").

## 4. Responsive rules
- Design Storefront at 360 first, then 768, 1024, 1280. Admin at 1280 first, check 1024 and 768 (sidebar drawer, scrolling tables).
- No horizontal page scroll at any width; only tables and code blocks scroll inside their container.
- Touch targets 44 px on touch devices; hover-only information is also reachable by focus / tap.
- Images keep aspect ratio (4:3 product tiles, 1:1 thumbnails) with a neutral placeholder.

## 5. Accessibility hooks used above
Landmarks (`header`, `nav`, `main`, `footer`), one `h1` per page, skip-to-content link, visible focus ring, labelled icon buttons, `aria-live` for toasts and list updates, form errors linked to fields — details in [accessibility.md](accessibility.md).
