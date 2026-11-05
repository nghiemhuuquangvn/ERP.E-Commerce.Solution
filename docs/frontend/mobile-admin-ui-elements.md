# ERP Admin mobile app - UI element analysis

Input: the approved screen mockup (HTML phone frames, 2026-11-04). Output: for every HTML element the mockup uses, the React Native element the app uses, the accessibility role it needs, and the components that must exist. Plan: `feat/mobile-admin-app`.

## 1. Why the mapping is needed
The mockup is HTML only because it is a picture. The app is Expo / React Native: there is no DOM, no `div`, no CSS file. Every tag becomes a native component, styled with NativeWind role-token classes (`bg-surface`, `text-ink`, `border-line`, `bg-brandAction` ... generated from `tokens.json`). A raw colour is a build failure (`noRawColours.test.ts`).

## 2. Tag -> component table
| Mockup HTML | App element | Accessibility / notes |
|---|---|---|
| `div` (layout box) | `View` | Layout by flex classes only; no margins between siblings, use `gap-*` |
| `span`, `p`, `b`, `small`, `h3` | `Text` | Only `Text` may hold text. Heading: `accessibilityRole="header"`. Numbers: `tabular-nums` class |
| `header` (screen top) | Expo Router `Stack.Screen options` / `Tabs` header | Title + tenant chip in `headerRight`; back arrow from the stack |
| `nav` (bottom tabs) | `Tabs` (expo-router) | Each tab: `tabBarAccessibilityLabel`; icon Lucide size 24; active `colors.brandAction` |
| `section`, `main` | `Screen` (`components/ui`) | `scroll` for long pages, `edges` for safe area |
| `button`, `.act` | `Button` (`variant`: primary / outline / danger / ghost; `size`) | `accessibilityRole="button"`; min height 44; loading state disables it |
| `.chip` (filter) | `FilterChips` (new) = `ScrollView horizontal` of `Pressable` | `accessibilityRole="tab"` + `accessibilityState.selected` |
| `.seg` (segmented) | `SegmentedControl` (new) | One `View accessibilityRole="tablist"`; each segment `role="tab"` + `selected` |
| `.badge` | `Badge` (`tone`) via `StatusBadge` (new) | Text label always shown (colour is never the only signal) |
| `.card` | `Card` (`onPress` makes it tappable, min 48) | List rows are `Card` with `accessibilityLabel` = "PO-00123, Acme Ltd, 4,200 dollars, Submitted" |
| `.alert` | `Alert` (`tone`) | Already `role="alert"` + live region |
| `.sheet` + `.scrim` | `BottomSheet` | Scrim `Pressable` closes; title is a header; focus trapped by `Modal` |
| `.in`, `.field` (input) | `TextField` (`label`, `hint`, `error`, `required`) | Label bound via `accessibilityLabel`; reason field `multiline` |
| `.sk` | `Skeleton` | Hidden from screen readers; one `Spinner label` announces loading |
| `.empty` | `EmptyState` (`icon`, `title`, `hint`, `action`) | Title is a header |
| `dl / dt / dd` | `DetailRows` (new): `View` of label `Text` + value `Text` rows | Each row one `accessible` node reading "Supplier: Acme Ltd" |
| `.lines .r` (table rows) | `LineItemRow` (new) | Name left, amount right, `tabular-nums`; total row bold |
| `svg` icon | Lucide (`lucide-react-native`) | Decorative icons `accessibilityElementsHidden`; icon-only buttons need a label |
| `svg` sparkline | `RevenueSparkline` (new) with `react-native-svg` `Svg/Path/Circle` | Whole chart one node with a text summary label ("Revenue rose from 30 to 92 over 12 days") |
| `.kpi` | `KpiCard` (new) = `Card` + label `Text` + value `Text` | Value read together with label |
| `.tenant` chip | `TenantChip` (new) = `Badge tone="info"` | Value from `tenantConfig.code` |
| list (`.row` of cards) | `FlatList` | `onEndReached` paging, `RefreshControl` pull-to-refresh, `ListEmptyComponent`, `keyExtractor` = id |
| toast | `ToastHost` / `useToast` | success after an action, danger on refusal (server sentence) |
| `.bar` (action bar) | `ApprovalBar` (new) | `View` fixed under the scroll area, safe-area bottom padding; renders only the allowed `Button`s |

## 3. New components (all in `admin/components/`, not in `components/ui`)
`components/ui` is byte-identical across the three apps (parity test), so admin-only parts live beside it:
`ScreenHeader` (title + `TenantChip`), `TenantChip`, `KpiCard`, `RevenueSparkline`, `SegmentedControl`, `FilterChips`, `SearchField`, `StatusBadge`, `DetailRows`, `LineItemRow`, `ApprovalBar`, `ReasonSheet` (BottomSheet + required `TextField` + confirm), `ConfirmSheet`, `OfflineBanner`, `NoAccess` (EmptyState with lock icon), `ListFooter` (spinner while paging), `usePagedList`. Each gets a Jest + accessibility test. Rule: no component imports another module's code; modules compose these.

## 4. Per-screen element tree
**Sign in** `Screen center` > logo `View` > `Text header` "ERP Admin" > `TenantChip` > `TextField` email (`keyboardType=email-address`, `autoComplete=email`, `textContentType=username`) > `TextField` password (`secureTextEntry`, show/hide `Pressable`) > `Alert danger` (only on error) > `Button primary` Sign in (loading).
**Two-step code** `Screen` > back header > `Text` explanation > six-digit `TextField` (`keyboardType=number-pad`, `textContentType=oneTimeCode`, `maxLength=6`) > `Button` Verify > `Button ghost` Resend (disabled with countdown text).
**Home** `Screen scroll` + `RefreshControl` > `SegmentedControl` (7 / 30 / 90) > two `KpiCard` > `Card` `RevenueSparkline` > `Card` "Needs your action" of rows (`Pressable` -> filtered list) > states: `Skeleton`s while loading, `NoAccess` without `reports:sales:view`.
**Purchasing / Sales list** `View` > `SearchField` > `FilterChips` > `FlatList` of `Card` rows (number as mono `Text`, `StatusBadge`, party, amount, date) > `OfflineBanner` on top when offline.
**Detail** `Screen scroll` > header number + `StatusBadge` > `Card DetailRows` > `Card` of `LineItemRow`s + total > notes `Card` > (Sales) anomaly `Alert warning` > `ApprovalBar` below the scroll. Tapping Approve opens `ConfirmSheet`; Reject / Cancel open `ReasonSheet`; refusal -> `Alert danger` with the server sentence; success -> toast + refreshed item.
**HR** `SegmentedControl` (Employees | Leave | Attendance) > per segment: `SearchField`/`FilterChips` > `FlatList`. Leave row `Card` + inline Approve / Reject `Button`s (open the same sheets). Employee and attendance rows are read only.
**Header** `MenuButton` (Lucide `Menu`, label "Open menu"), title, `NotificationBell` (Lucide `Bell`, badge `View` bg-danger with `Text` 1-99 / 99+, label "Notifications, N unread"), `TenantChip`.
**Side menu** `Modal` > left `Animated.View` panel (84 % of the width, max 340) + right scrim `Pressable` (closes) > `ScrollView`: user name / e-mail / `TenantChip` + Close `X`, then a `menu` of rows (icon, title; selected = bg-brand-soft + `accessibilityState.selected`; the Notifications row shows the unread number), language `SegmentedControl`, `DetailRows` (tenant, server, version), `Button outline` Sign out (opens `ConfirmSheet`).
**Notifications inbox** `PagedListView` of `Card` rows: unread dot (bg-brand-action), subject (bold when unread), body (2 lines, all when opened), date; label "Unread: ..." / "Read: ..."; `EmptyState` when empty; `NoAccess` on a 403.

## 5. Status -> `Badge` tone (colour never alone, label always shown)
| Domain | neutral | info | warning | success | danger |
|---|---|---|---|---|---|
| Purchase order | Draft | Submitted, Ordered | Pending finance approval, Partially received | Approved, Received | Rejected, Cancelled |
| Sales order | Draft | Submitted, Picked, Shipped, Partially shipped | Return requested | Approved, Delivered | Rejected, Cancelled, Returned |
| Leave | Cancelled | - | Pending | Approved | Rejected |
| Employee | Terminated | - | On leave | Active | - |

## 6. Accessibility and platform rules
- Touch target >= 44 px; text scales with system font (no fixed heights on text containers); `allowFontScaling` stays on.
- Every `Pressable` has `accessibilityRole` and a label; selected chips/tabs expose `accessibilityState.selected`; disabled buttons expose `disabled`.
- Errors and refusals use `Alert` / toast live regions; the focus moves to the first invalid field.
- Forms: keyboard type, `returnKeyType`, `KeyboardAvoidingView` in sheets; password `secureTextEntry`.
- Dark theme: components use role tokens only, so the dark set applies without changes.
- No swipe-only actions; money formatted by `lib/money.ts` (locale aware); dates by `date-fns` locale.
- Every string through `t()`; en / vi parity test.

## 7. What the mockup does not show (to settle while coding)
Filtered-list deep links from "Needs your action", the leave detail screen, the employee detail screen, attendance row layout, landscape/tablet (portrait only for v1), pull-to-refresh visual.

## Bottom bar, filters and the new screens (version 1.2)
**Bottom bar** `ModuleTabBar`: `View` (border-top, bg-surface, bottom safe area) of items (Lucide icon 24 + label 12, min height 56, role `tab`, `accessibilityState.selected`); on Home one item per module, inside a module Home + the module's sections, last item "More" (`MoreHorizontal`) opening the side menu (Home) or a `BottomSheet` of the remaining sections (module).
**Filters above a list** `ListFilters`: row of `SearchField` + "Filters" `Pressable` (`SlidersHorizontal`, count badge bg-brand-action; label "Open filters, N active"); under it summary chips (`bg-brand-soft`, label + `X`, 44 pt, "Clear filter ...") for the status and the period.
**Filters sheet** `BottomSheet`: Status `FilterChips` (All + statuses) > Period `SegmentedControl` (All time | Month | Quarter | Year | Custom) > stepper row (`ChevronLeft` previous - label "2026-11" / "Q4 2026" / "2026" - `ChevronRight` next) or, for Custom, two `DateField`s and a danger `Alert` when the start is after the end > Reset (outline) + Done (primary).
**Date box** `DateField`: bordered row (`CalendarDays`, small label, date or "Choose a date"); opens a `BottomSheet` month grid: month header with previous / next, weekday row (Mo - Su), 7-column grid of 44 pt cells (the chosen day as bg-brand-action, days of other months muted, each cell labelled with its full date), Today and Clear date buttons.
**List rows (new screens)** `Card`: line 1 number or name (semibold) + `StatusBadge`; line 2 party and amount; line 3 dates (muted). Suppliers: name, code and contact, active badge, payment terms. Invoices: number, supplier and PO, amount, invoice and due date, hold reason in the warning colour. RFQs: number, title, "N lines - N invited - N quoted". Pay slips: name, number and year-month, gross and net.

## Details, action fields and credit limits (version 1.3)
**Detail screen:** `Stack` header with the back arrow and the screen title > `ScrollView` (pull to refresh) of `DetailHeader` (number or name + `StatusBadge`), optional `Alert`s (hold reason warning, approved success, rejected danger), `DetailSection` cards (title as heading, `DetailRows` or `LineItemRow`s) > `ApprovalBar` pinned below.
**Action sheet with fields:** `BottomSheet` > `ScrollView` of `TextField`s (label with a red star when required, number fields with the decimal keypad, the error under the field) > Back + confirm buttons. Used by Record credit (note number, amount), Convert to order (five address lines), Pay (method), Change limit (amount).
**Credit limit row:** `Card`: name + level `Badge` (Within limit success / Low warning / Over the limit danger), "Limit: ... Open balance: ...", "Available: ..." in semibold.
