# Staff mobile app — screens

Business analysis of the field / retail staff app (`src/frontend/mobile/staff`, Expo / React Native) as shipped on 2026-11-03 (ROADMAP P2-9, U5). Rules for the sale, the stock and the delivery are in [POS/AdminBackOffice.md](AdminBackOffice.md), [Warehouse](../Warehouse) and [Shipping](../Shipping). **U5 changed the presentation only** (plus the build set-up and one data-reading fix, §8): requests, stores and rules are unchanged and asserted by screen tests.

## 1. Purpose and scope
Let a signed-in staff member see the day's figures, ring up a sale, look up stock and follow deliveries from a phone, with big touch targets (52 pt) and text in English or Vietnamese.
In scope: Sign in, Dashboard, POS, Inventory (with an adjust sheet), Deliveries.
Not built: barcode scan (`expo-camera` is installed, unused), cash shift screen, offline queue, language switch inside the app (the language follows the stored setting), dashboard figures (all four are placeholders: 0).

## 2. Actors and permissions
| Actor | Uses | Server permission |
|---|---|---|
| Cashier | POS | POS transaction rights (checked by the server) |
| Warehouse staff | Inventory | inventory view; stock adjustments are requests that need approval |
| Delivery staff | Deliveries | `shipping:view`, `shipping:update` |
The app checks nothing itself; the server decides. The shop is the tenant of the host in `EXPO_PUBLIC_API_URL`.

## 3. Screens and states
| Screen | Loading | Empty | Error | Notes |
|---|---|---|---|---|
| Sign in | busy button | - | danger alert | labelled **Email \*** / **Password \***, field messages, go key signs in |
| Dashboard | - | - | - | greeting, four figure cards (placeholders), quick-actions hint |
| POS | - | "No items added yet." + hint | red toast on failure | add bar: **Product ID**, **Name**, **Unit price**, **+ Add**; lines; subtotal, **tax (10 %)**, total; payment method radio group (Cash, Credit Card, E-Wallet); **Complete Transaction** (disabled with no lines, shows progress) |
| Inventory | four skeletons | "No products found." + **Clear the search** | alert + **Try again** | labelled **Search products**; each row: name, SKU, available quantity (red 0, amber below 5, green otherwise), **Adjust stock of <name>** opens a bottom sheet (current quantity, **New quantity \***, Save / Cancel) |
| Deliveries | three skeletons | "No active shipments." | alert + **Try again** | tracking number, status chip (translated), order id; **Mark <tracking> in transit** for a shipment that is not delivered |

## 4. Rules (unchanged by U5)
1. **A sale** opens a transaction (`TERM-001` as the terminal, the signed-in user as cashier), adds every line (product id, name, quantity, unit price, tax rate 10) and completes it with the chosen payment method; success shows a green toast "Transaction complete. Total: $x" and clears the sale; a failure keeps the lines.
2. **Stock adjust** sends the difference between the typed quantity and the quantity on hand with the reason "Staff adjustment"; an entry that is not a whole number is refused on the sheet and nothing is sent.
3. **Mark in transit** adds a tracking event (`InTransit`, location "Warehouse", description "Updated by staff"); a delivered shipment has no action.

## 5. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| SA-1 | One look from the design system | as MC-1 in [MobileScreens.md](../Sales/MobileScreens.md); the two apps share one component library (a parity test fails if the copies differ) |
| SA-2 | Labels, errors, states | as MC-2 / MC-3: every field has a label, every list has loading / empty / error |
| SA-3 | Big targets | every button, tab and radio is at least 44 pt, actions on a sale or a shipment 52 pt |
| SA-4 | Text in both languages | the previously hard-coded English (sign in, dashboard, deliveries, inventory, POS, alerts) is in `en.json` / `vi.json`; a test fails on a missing key |
| SA-5 | Feedback without blocking | success and failure of a sale, an adjustment and a tracking update are toasts, not modal alerts |
| SA-6 | Behaviour unchanged | sale, adjust and tracking requests equal the previous ones (asserted); POS totals unchanged |

## 6. Use cases
**UC-1 Ring up a sale.** POS → type product id, name and unit price → **+ Add** (repeat) → choose the payment method → **Complete Transaction**.
*Exception:* the server refuses → red toast, lines kept so the cashier can retry.
**UC-2 Look up stock.** Inventory → type in **Search products** → read the available quantity.
**UC-3 Follow a delivery.** Deliveries → read the status; **Mark … in transit** when the parcel leaves.

## 7. Data and integration
Routes used: `v1/auth/login`, `v1/inventory` (+ `v1/inventory/adjust`), `v1/shipments` (+ `/{id}/tracking`), `v1/pos/transactions/open`, `…/{id}/items`, `…/{id}/complete`.

## 8. Defects found while verifying (pre-existing, recorded as R107)
- **Inventory data contract:** the screen read `id`, `quantityOnHand`, `sku`; the service answers `productId`, `totalQuantity`, `productSku`. **Fixed in U5** (the list and the sheet now show real numbers; before, the current quantity was blank and the difference sent would have been `NaN`).
- **`POST v1/inventory/adjust` does not exist** any more: the warehouse replaced it by stock-adjustment *requests* that need approval (`api/v1/stock-adjustments`). Saving on the sheet therefore fails with the red toast until the screen is moved to the request flow. Left as is (a business decision, not presentation).
- **Deliveries list:** `GET /api/v1/shipments` is answered 404 by the Gateway (its route is `/api/v1/shipments/{everything}`, which needs something after `shipments`); the Admin goes through `/shp/api/v1`. The screen shows the error state with **Try again**; the list stays empty until the route or the path is fixed.
- The terminal id `TERM-001`, the 10 % tax rate and US-dollar formatting are fixed in code.

## 9. Build and verification status (2026-11-03)
Same set-up fixes as the customer app (metro config + `global.css`, `nativewind` 4.1.23, `react-native-reanimated` ~3.10.1 added because the NativeWind Babel plugin needs it, `@react-native/js-polyfills` pinned at the top level so Metro finds it, `import.meta` plugin, navigation guard).
Verified: 38 Jest tests, `tsc`, `check:tokens`; `expo export --platform web` and a **read-only** walk at 375 px as the test shop's admin (23 of 23: sign in errors and success, four named tabs, inventory search and adjust sheet opened and cancelled, deliveries error state, POS line added locally with tax 1.00 / total 11.00, radio group, touch targets, contrast 5.60, no console error). No sale was completed, no stock changed, no shipment touched. **Not verified: a native build or a device.**

## 10. UAT scenarios
1. Sign in empty → two messages; wrong password → alert; right one → Dashboard.
2. POS: add a line 10.00 → subtotal 10.00, tax 1.00, total 11.00; **Complete Transaction** stays disabled until a line exists.
3. Inventory: search by name; open **Adjust**, type letters → "Enter a whole number."; Cancel closes the sheet.
4. Deliveries: with the route fixed, a shipment shows its status chip and **Mark … in transit**.
5. Switch the phone to Vietnamese (stored locale): every screen reads Vietnamese.
