# Mobile patterns (Customer app and Staff app)

Expo / React Native, NativeWind with the tokens of [foundations.md](foundations.md). **Customer** app = Customer theme (rose), **Staff** app = Back-office theme (blue). Components come from [components.md](components.md) (`BottomSheet`, `Toast`, `EmptyState`, `Skeleton` …).

## 1. Rules for both apps
- **Safe areas:** use the safe-area insets for header and bottom bar; content never sits under the notch or home indicator; the tab bar height is 56 + bottom inset.
- **Touch:** every target ≥ 44 × 44 pt, 8 pt between neighbours; primary action reachable by the thumb (bottom of the screen, full width, 48 high).
- **Navigation:** bottom tabs for top-level areas (max 5), stack inside a tab; back is always available (header chevron + swipe / hardware back); a modal flow (checkout, scan) is a full-screen stack with a close `X`.
- **Feedback:** every action answers within 100 ms (pressed state), longer work shows a loading state on the control; result by `Toast`; destructive confirmation by centred `Alert`; light haptic on success of a scan, add to cart and payment, error haptic on a refused action.
- **Lists:** `FlatList`, pull-to-refresh, `Skeleton` rows while loading, `EmptyState` with action, end-of-list footer spinner for paging, rows ≥ 56 high.
- **Keyboard:** screens with inputs use `KeyboardAvoidingView` + scroll; the primary button stays visible above the keyboard; the field type sets the keyboard (`numeric`, `email-address`, `phone-pad`); "next" moves focus, "done" submits.
- **Offline:** a `Banner` ("You are offline — showing saved data") at the top; reads from cache, writes either queue (Staff) or are disabled with an explanation (Customer checkout); the banner clears with a `Toast` when the connection returns.
- **Icons / colour:** Lucide, 24 in tab bar, 20 in rows, colour from tokens; the active tab uses `brand-action`, inactive `text-muted` (today hex literals `#e11d48` / `#0284c7` in `_layout.tsx` are replaced by tokens).
- **Text:** minimum 14 pt, follows the OS font scaling (layout must survive 130 %); one language at a time from the app setting (en / vi).
- **Accessibility:** `accessibilityLabel` and `accessibilityRole` on every touchable and icon, grouped content read as one item, focus order follows reading order, error text announced.

## 2. Customer app
Tabs: **Home · Search · Cart · Orders · Profile** (cart tab shows a count badge). Stack screens: product detail, checkout flow, order detail, address form, payment result.

| Screen | Layout |
|---|---|
| Home | search bar (opens Search), banner carousel, category chips, product rows, promotions; pull-to-refresh |
| Search | focused input, recent searches, filter / sort `BottomSheet`, 2-column grid, `Skeleton` tiles |
| Product detail | image pager with dots, price (700) and discount, variant chips, description sections, sticky bottom bar: quantity stepper + "Add to cart" (48 high) |
| Cart | rows with image, name, quantity stepper, swipe-to-remove with undo `Toast`, coupon row (opens `BottomSheet`), summary, sticky "Checkout" |
| Checkout | steps: address (list + add) → payment method (methods enabled by the shop) → review; top progress; total always visible; paying with a gateway opens the gateway page and returns to the payment result |
| Payment result | large status icon, message, order number, "View order" and "Continue shopping" |
| Orders | status filter chips, order cards (number, date, status badge, total, first item thumbnails) |
| Order detail | timeline, parcels with tracking numbers, items, payment, address; actions: cancel (confirm), request return (reason `BottomSheet`) |
| Profile | avatar + name, menu rows (addresses, language, security, notifications, sign out) |
| Auth | single column, large inputs, social buttons, errors under fields, "forgot password" link |

## 3. Staff app
Tabs: **Dashboard · POS · Inventory · Deliveries**. Used standing up, one-handed, sometimes in poor light and on poor networks: bigger targets (52), higher contrast, fewer words.

| Screen | Layout |
|---|---|
| Dashboard | KPI `Stat` cards from Reporting (today's sales, open orders, low stock, deliveries) with last-updated time; shortcuts to scan and start a sale |
| POS | product search + **barcode scan** button (full-screen camera with a framing guide, torch, haptic + sound on success, manual entry fallback), cart list with steppers, discount row, big total, "Charge" button → payment method sheet (cash with change calculator, card, wallet), receipt screen (share / print) |
| Shift | open shift (opening cash count by denomination), close shift (counted cash, difference highlighted in `danger` when ≠ 0) |
| Inventory | search + scan, stock card (on hand, reserved, available, locations), adjust quantity with a mandatory reason, low-stock list |
| Deliveries | list by status with distance / sequence, delivery card: address (tap to open maps), phone (tap to call), COD amount in `warning` colour, actions: picked up, delivered (with confirmation of cash collected), failed (reason list + note) |
| Offline queue | a header chip "3 waiting to sync" opens the queue list (retry / discard); sales made offline keep a client id and are marked "pending" until synced |

## 4. Differences from web (decisions)
- Selection controls use native pickers or bottom sheets, not dropdowns.
- Confirmations of destructive actions use the OS-style alert; reasons use a `BottomSheet` with a text field.
- No hover states; pressed state darkens to `brand-action-hover` and scales to 0.98 for 120 ms.
- Tables become cards; at most three facts per card plus the status badge.
