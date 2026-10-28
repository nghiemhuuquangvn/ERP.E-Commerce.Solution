# Foundations — tokens and icons

All values are the **standard**. "Today" notes show where the code differs. Hex values are given once here; apps must never write a hex in a component (see "Implementation shape" at the end).

## 1. Colour

### 1.1 Neutral (shared by every theme)
| Token | Hex | Use |
|---|---|---|
| `surface` | `#ffffff` | cards, inputs, modals, tab bars |
| `surface-muted` | `#f3f4f6` | table header, hover rows, skeleton base |
| `page` | `#f9fafb` | page background (Today: `bg-gray-50` in Admin) |
| `border` | `#e5e7eb` | card and table dividers (decorative, no 3:1 requirement) |
| `border-strong` | `#6b7280` | borders of **controls** (input, select, checkbox) — needed for 3:1, see accessibility |
| `text` | `#111827` | body text and headings |
| `text-secondary` | `#4b5563` | helper text, secondary labels |
| `text-muted` | `#6b7280` | timestamps, placeholders, disabled hints (min 14 px) |
| `text-on-brand` | `#ffffff` | text on the brand action colour |
| `overlay` | `rgba(17,24,39,.45)` | modal backdrop (Today: `bg-black/40`) |

### 1.2 Semantic (shared)
| Role | Solid (fill, white text) | Soft background | Soft text | Today |
|---|---|---|---|---|
| `success` | `#15803d` | `#dcfce7` | `#166534` | green-100 / green-700 in `StatusBadge` |
| `warning` | `#b45309` | `#fef3c7` | `#92400e` | yellow-100 / yellow-700 |
| `danger` | `#dc2626` (hover `#b91c1c`) | `#fee2e2` | `#991b1b` | red-600 in `.btn-danger` |
| `info` | = brand action | `#e0f2fe` | `#075985` | blue-100 / blue-700 |
| `neutral` | `#4b5563` | `#f3f4f6` | `#374151` | gray-100 / gray-600 |

Status → role mapping (replaces the long list in `StatusBadge.tsx`): *Active, Confirmed, Completed, Delivered, Captured, Sent, Present, Approved, Paid* → success; *Pending, PendingReview, Submitted, PartiallyReceived, Late, HalfDay, ReadyForPickup* → warning; *Cancelled, Failed, Rejected, Voided, DeliveryFailed, Discontinued* → danger; *Processing, Authorized, InTransit, PickedUp, OutForDelivery, Ordered, OnLeave* → info; *Draft, Queued, Holiday, Scheduled, Paused, Expired* → neutral. A status keeps its role in every app and every language.

### 1.3 Brand — one set of role tokens, two themes
| Token | Back-office (Admin, Staff) | Customer (Storefront, Customer app) | Meaning |
|---|---|---|---|
| `brand-action` | `#0369a1` | `#e11d48` | fill of primary buttons, selected tab / nav item, links on white, **focus ring** |
| `brand-action-hover` | `#075985` | `#be123c` | hover / pressed |
| `brand-soft` | `#e0f2fe` | `#ffe4e6` | selected row, soft badge, highlight |
| `brand-soft-text` | `#0c4a6e` | `#881337` | text on `brand-soft` |
| `brand-subtle` | `#f0f9ff` | `#fff1f2` | very light tint (hero, empty-state backdrop) |
| `brand-accent` | `#0ea5e9` | `#f43f5e` | decoration only: charts, illustrations, progress fill (never text, never alone as a UI boundary) |

**Why Back-office action is `#0369a1` and not today's `#0284c7`:** white text on `#0284c7` is 4.10:1 (fails AA for 14 px text); on `#0369a1` it is 5.93:1. The visual change is one shade darker. Customer action `#e11d48` is unchanged (4.70:1).

Today → standard: Admin `primary-500/600/700` and Storefront `brand-500/600` are replaced by the role tokens above; Storefront's current button colour `brand-500 (#e11d48)` becomes `brand-action` with the same hex.

## 2. Typography
- **Family:** system UI stack on web `ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Noto Sans", sans-serif`; mobile uses the platform font (SF Pro / Roboto). No web font is downloaded (Vietnamese diacritics render correctly in all of these; check with "Nguyễn Thị Hương, Đồng Nai, ạ ơ ữ").
- **Scale (px / line height):** `xs` 12/16 · `sm` 14/20 · `base` 16/24 · `lg` 18/28 · `xl` 20/28 · `2xl` 24/32 · `3xl` 30/36. Back-office body is `sm` (14), customer body is `base` (16).
- **Weights:** 400 body, 500 labels and buttons, 600 headings and table headers, 700 only for prices and totals.
- **Roles:** page title `xl/600` (Back-office) or `2xl/600` (Customer); section title `lg/600`; table header `xs/600 uppercase tracking-wide text-secondary`; helper `xs/400 text-secondary`; money `tabular-nums`.
- Never use text below 12 px; never colour text with `brand-accent`.

## 3. Space, size, shape
- **4-pt grid.** Allowed steps: 4, 8, 12, 16, 20, 24, 32, 40, 48, 64 (Tailwind `1,2,3,4,5,6,8,10,12,16`). Page padding 24 (Back-office) / 16 mobile, 16–24 Storefront. Gap between form fields 16; between sections 24–32.
- **Radius:** control (button, input) `6` web / `12` mobile; card `8` web (`12` Storefront) / `16` mobile; pill (badge, chip) full; modal `12`; bottom sheet top corners `20`.
- **Control height:** web 36 (Back-office) / 40 (Storefront); mobile 48 (min touch 44 × 44).
- **Elevation:** `shadow-sm` cards at rest; `shadow-md` dropdowns; `shadow-lg` modals and sheets. Borders carry structure, shadows only separate layers.
- **Z-index layers:** content 0 · sticky header 10 · sidebar 20 · dropdown / popover 30 · modal 40 · toast 50 · tooltip 60. Nothing else invents a number.

## 4. Motion
`fast` 120 ms (hover, press) · `base` 200 ms (dropdown, tab) · `slow` 300 ms (modal, sheet). Easing `ease-out` in, `ease-in` out. Respect `prefers-reduced-motion` (web) and the OS reduce-motion setting (mobile): replace movement by a fade.

## 5. Breakpoints and density
- **Web:** `sm` 640, `md` 768, `lg` 1024, `xl` 1280. Admin is designed for ≥ 1024 with a collapsible sidebar below `lg` and tables that scroll horizontally; Storefront is mobile-first (single column → 2 / 3 / 4 columns at `sm / lg / xl`).
- **Density:** Back-office *compact* (control 36, row 44, body 14). Customer *comfortable* (control 40–48, body 16). A Back-office page opened on a tablet keeps compact density.
- **Mobile:** respect safe areas (top notch, bottom home indicator), minimum touch target 44 pt, content width max 640 on tablets (centered).

## 6. Icons (Lucide)
- Packages: web React `lucide-react`, web Vue `lucide-vue-next`, mobile `lucide-react-native` (+ `react-native-svg`). Import icons by name (tree-shaken), never the whole set.
- **Sizes:** 16 (inline with text, table actions), 20 (buttons, nav items — Back-office), 24 (tab bar, Storefront header, empty-state 48). **Stroke 1.75**, `currentColor`; an icon takes the colour of its text.
- An icon-only control always has an accessible name (`aria-label` / `accessibilityLabel`) and a tooltip on web.
- Decorative icons are hidden from screen readers; meaningful status icons (error, success) are paired with text.
- No emoji in the UI chrome, nav, buttons, tabs or status. (Emoji written by users or in product text stay as text.)

### 6.1 Emoji → Lucide map (every icon in use today)
| Where | Today | Lucide | Where | Today | Lucide |
|---|---|---|---|---|---|
| Dashboard | ⊞ | `LayoutDashboard` | Sales (group) / Sales orders | 📦 | `ShoppingBag` |
| Quotations | 📝 | `FileText` | Credit limits | 💳 | `CreditCard` |
| Commission plans / records | 💹 📈 | `Percent` / `TrendingUp` | Purchasing (group) | 🧾 | `ClipboardList` |
| RFQs | 📨 | `Send` | Supplier invoices | 🧮 | `Receipt` |
| Returns to vendor | ↩️ | `Undo2` | Purchasing settings / Stock adjustments | ⚖️ | `Settings2` / `Scale` |
| Products (group) / Products / Brands | 🏷️ | `Tag` | Categories | 📁 | `FolderTree` |
| Price lists | 💲 | `BadgeDollarSign` | Attributes | 🧩 | `Puzzle` |
| Inventory (group) / Inventory | 🏭 | `Warehouse` | Locations | 📍 | `MapPin` |
| Warehouses / Tenants | 🏢 | `Building2` | Stocktakes | 📋 | `ClipboardCheck` |
| Transfers | 🔀 | `ArrowLeftRight` | Barcode | 🔖 | `ScanBarcode` |
| Valuation snapshots / Reports | 📊 | `BarChart3` | Payments | 💳 | `Wallet` |
| Shipments | 🚚 | `Truck` | Promotions (group) | 🎁 | `Gift` |
| Promotions | 🏷️ | `BadgePercent` | Flash sales | ⚡ | `Zap` |
| Loyalty | ⭐ | `Star` | POS (group) | 🛒 | `Store` |
| POS transactions | 🧾 | `Receipt` | POS shifts | 🕒 | `Clock` |
| Z report | 📑 | `FileBarChart` | POS terminals | 🖥️ | `MonitorSmartphone` |
| Chatbot | 🤖 | `Bot` | HRM / Users | 🧑‍💼 | `Users` / `UserCog` |
| Accounting | 💼 | `Landmark` | Master data | 🗂️ | `Database` |
| Settings | ⚙️ | `Settings` | Monitoring | 🖥 | `Activity` |
| Logs | 📜 | `ScrollText` | Notification bell | 🔔 | `Bell` |
| Mobile Customer tabs: Home / Search / Cart / Orders / Profile | 🏠 🔍 🛒 📋 👤 | `Home` / `Search` / `ShoppingCart` / `Package` / `User` | Mobile Staff tabs: Dashboard / POS / Inventory / Deliveries | ⊞ 🛒 📦 🚚 | `LayoutDashboard` / `Store` / `Boxes` / `Truck` |

Generic actions (used in buttons and menus): add `Plus`, edit `Pencil`, delete `Trash2`, save `Check`, close `X`, search `Search`, filter `SlidersHorizontal`, refresh `RefreshCw`, more `MoreHorizontal`, download `Download`, upload `Upload`, back `ChevronLeft`, expand `ChevronDown`, external `ExternalLink`, success `CheckCircle2`, error `AlertCircle`, warning `AlertTriangle`, info `Info`.

## 7. Implementation shape (for the adoption steps, informative)
- Tokens live in **one source file per platform family**, generated from the tables above: web `tokens.css` (CSS variables: `--color-brand-action` …, theme set by `data-theme="back-office|customer"` on `<html>`) + a Tailwind preset that maps them (`colors.brand.action = 'rgb(var(--…))'`); mobile a `theme.ts` exporting the same names and a NativeWind preset. Until a monorepo workspace exists the file is copied per app and a test fails when copies drift (U1 in the roadmap).
- Components read tokens only; a hex in a component is a review finding.
