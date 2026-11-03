# Staff mobile app — screens

Business analysis of the field / retail staff app (`src/frontend/mobile/staff`, Expo / React Native) as shipped on 2026-11-03 (ROADMAP P2-9, U5). Rules for the sale, the stock and the delivery are in [POS/AdminBackOffice.md](AdminBackOffice.md), [Warehouse](../Warehouse) and [Shipping](../Shipping). **U5 changed the presentation only** (plus the build set-up and one data-reading fix, §8): requests, stores and rules are unchanged and asserted by screen tests.

## 1. Purpose and scope
Let a signed-in staff member see the day's figures, ring up a sale, look up stock and follow deliveries from a phone, with big touch targets (52 pt) and text in English or Vietnamese.
In scope: Sign in, Dashboard, POS, Inventory (with a stock-adjustment request sheet), Deliveries.
Not built: cash shift open / close screen (a sale is attached to the terminal's open shift when there is one, never required), offline queue, receipt print / share, offline queue, language switch inside the app (the language follows the stored setting), dashboard figures (all four are placeholders: 0).

## 2. Actors and permissions
| Actor | Uses | Server permission |
|---|---|---|
| Cashier | POS | `pos:transactions` (terminals list, products lookup, sales); `pos:shifts:view` is only needed to see the open cash shift (without it the sale is simply not attached to one) |
| Warehouse staff | Inventory | inventory view; stock adjustments are requests that need approval |
| Delivery staff | Deliveries | `shipping:view`, `shipping:update` |
The app checks nothing itself; the server decides. The shop is the tenant of the host in `EXPO_PUBLIC_API_URL`.

## 3. Screens and states
| Screen | Loading | Empty | Error | Notes |
|---|---|---|---|---|
| Sign in | busy button | - | danger alert | labelled **Email \*** / **Password \***, field messages, go key signs in |
| Dashboard | - | - | - | greeting, four figure cards (placeholders), quick-actions hint |
| POS | skeletons while the terminal list or a search loads | "No items added yet." + hint; "No POS terminal is set up." in the terminal sheet | red toast on failure; "You are not allowed to ring up sales." (403) | top card **POS terminal: <name>** with **Choose / Change** (sheet **Choose a POS terminal**, active terminals as radios; remembered on the phone, forgotten at sign-out) and a note "Cash shift open: the sale is attached to it." or "No open cash shift: the sale is not attached to one."; **Find a product** (name or SKU, 2+ characters, 350 ms debounce, result rows with name, SKU, price, "Out of stock" rows cannot be picked) and **Scan a barcode** (camera sheet); lines with a named stepper (0 removes); subtotal, **tax (10 %)**, total; payment radio group (Cash, Credit Card, E-Wallet); **Complete Transaction** (disabled with no line or no terminal, with a warning) |
| Inventory | four skeletons | "No products found." + **Clear the search** | alert + **Try again** | labelled **Search products**; each row: name, SKU, available quantity (red 0, amber below 5, green otherwise), **Adjust stock of <name>** opens the bottom sheet **Request a stock adjustment**: current quantity, **New quantity \***, **Reason** radio group (Damaged, Lost, Correction - preselected, Other), optional **Note**, **Send request** / Cancel |
| Deliveries | three skeletons | "No active shipments." | alert + **Try again** | tracking number, status chip (translated), order id; **Mark <tracking> in transit** for a shipment that is not delivered |

## 4. Rules (unchanged by U5)
1. **A sale** opens a transaction with `POST pos/transactions` (`terminalId` = the chosen terminal's Guid, `cashierId` = the signed-in user, `cashShiftId` = the terminal's open shift when `GET pos/shifts/active/{terminalId}` finds one), adds every line (real catalogue product id, name, quantity, unit price, tax rate 10) to **the id the server returned** and completes it with the chosen payment method; success shows a green toast "Transaction complete. Total: $x" and clears the sale (the terminal stays chosen); a failure keeps the lines; 403 says the user may not ring up sales.
2. **Stock adjustment is a request, never a direct change** (WareHouseService `stock-adjustments`): the phone sends the difference between the typed quantity and the quantity on hand (`quantityDelta`), the reason as its number (Damaged 0, Lost 1, Correction 2, Other 3) and the note; a manager approves or rejects it in the Admin and only then does the stock move. A quantity that is not a whole number, or is the current one ("Nothing to change"), is refused on the sheet and nothing is sent. Success toast: "Adjustment requested. The stock changes once a manager approves it." A role without `inventory:adjustment:create` gets "You are not allowed to request stock adjustments."
2a. **Finding a product.** Typed search uses ProductService (`GET products/search?searchTerm=`, name or SKU); a **scan** takes the barcode as the SKU: first `GET pos/products/lookup/{barcode}` (the POS service's own product cache), and when that has no such product the catalogue is asked for an exact SKU match; found = one unit is added with a toast, not found = toast "No product with the barcode …" and the camera keeps scanning; the same code within two seconds is counted once. The camera permission is asked on first use; when it was turned off in the phone settings the sheet says so and opens the settings.
2b. **Products without a price.** The catalogue does not carry a retail price yet (R111). When a product is added and its price is 0, the app asks the POS product cache for a price; if there is none the product is **not added** and the toast says "<name> has no price set, so it cannot be sold. Ask a manager to set its price." (a sale with a zero amount cannot be completed).
3. **Mark in transit** adds a tracking event (`InTransit`, location "Warehouse", description "Updated by staff"); a delivered shipment has no action.

## 5. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| SA-1 | One look from the design system | as MC-1 in [MobileScreens.md](../Sales/MobileScreens.md); the two apps share one component library (a parity test fails if the copies differ) |
| SA-2 | Labels, errors, states | as MC-2 / MC-3: every field has a label, every list has loading / empty / error |
| SA-3 | Big targets | every button, tab and radio is at least 44 pt, actions on a sale or a shipment 52 pt |
| SA-4 | Text in both languages | the previously hard-coded English (sign in, dashboard, deliveries, inventory, POS, alerts) is in `en.json` / `vi.json`; a test fails on a missing key |
| SA-5 | Feedback without blocking | success and failure of a sale, an adjustment and a tracking update are toasts, not modal alerts |
| SA-6 | Behaviour unchanged | adjust and tracking requests equal the previous ones (asserted); POS totals (10 % tax) unchanged |
| SA-7 | A sale can really be made | the sale uses Guids only (terminal and product ids come from the server); no free-text product entry; terminal chosen before completing; shift attached when open (asserted field by field) |
| SA-8 | Finding products | search needs 2 characters; out-of-stock rows cannot be picked; a scan adds one unit once per two seconds; unknown barcode and denied camera are explained |

## 6. Use cases
**UC-1 Ring up a sale.** POS → (first time) **Choose** the terminal → **Find a product** or **Scan a barcode** (repeat) → fix quantities with − / + → choose the payment method → **Complete Transaction**.
*Exception:* the server refuses → red toast, lines kept so the cashier can retry.
**UC-2 Look up stock.** Inventory → type in **Search products** → read the available quantity.
**UC-3 Follow a delivery.** Deliveries → read the status; **Mark … in transit** when the parcel leaves.

## 7. Data and integration
Routes used (all through the Gateway, checked by `ClientApiContractTests`): `v1/auth/login`, `v1/inventory`, `v1/stock-adjustments`, `v1/shipments` (+ `/{id}/tracking`), `v1/pos/terminals`, `v1/pos/shifts/active/{terminalId}`, `v1/pos/products/lookup/{barcode}`, `v1/products/search`, `v1/pos/transactions` (+ `/{id}/items`, `/{id}/complete`).

## 8. Defects found in U5 and what became of them (R107, closed 2026-11-03; R110 opened)
- **Inventory data contract** - fixed in U5 (the screen read `id / quantityOnHand / sku`; the service answers `productId / totalQuantity / productSku`).
- **Adjust called a route that no longer exists** (`POST v1/inventory/adjust`) - fixed: the sheet files a stock-adjustment request (§4 rule 2). Behaviour change on purpose: the phone no longer changes stock directly.
- **Deliveries list 404** - fixed in the Gateway: `ocelot.json` now has `/api/v1/shipments` (the catch-all `/api/v1/shipments/{everything}` needs a further segment), and `/api/v1/stock-adjustments` (+ `/{everything}`); both need a bearer token (anonymous = 401), `internal` routes still do not exist (404).
- **POS opened a transaction on a route that does not exist** (`pos/transactions/open`) and then used the transaction id from before it existed - fixed (route `pos/transactions`; the id just returned is used).
- **The contract guard now covers the staff app and the Gateway**: every client call must have a controller action *and* an Ocelot route (`ClientApiContractTests`, `EveryRouteTheClientsCallIsRoutedByTheGateway`).
- **R110 closed 2026-11-03:** the POS screen now picks a real terminal and real products (see §3, §4 rule 1, rule 2a). The terminal list is readable with `pos:transactions` (before, only `pos:terminals:manage`; creating / renaming / switching terminals stays administrator-only).
- The 10 % tax rate and US-dollar formatting are fixed in code.


## 9. Build and verification status (2026-11-03)
Same set-up fixes as the customer app (metro config + `global.css`, `nativewind` 4.1.23, `react-native-reanimated` ~3.10.1 added because the NativeWind Babel plugin needs it, `@react-native/js-polyfills` pinned at the top level so Metro finds it, `import.meta` plugin, navigation guard).
Verified: 38 Jest tests, `tsc`, `check:tokens`; `expo export --platform web` and a **read-only** walk at 375 px as the test shop's admin (23 of 23: sign in errors and success, four named tabs, inventory search and adjust sheet opened and cancelled, deliveries error state, POS line added locally with tax 1.00 / total 11.00, radio group, touch targets, contrast 5.60, no console error). No sale was completed, no stock changed, no shipment touched. **Not verified: a native build or a device.**

Device test (R68, 2026-11-03): the app is configured for an Android build (identifiers, icon, splash, EAS preview profile with cleartext HTTP to the test stack) and verified with `expo prebuild` (CAMERA permission only). The run on a phone - permission dialog, real barcode / QR scan, one real sale, TalkBack - is the owner's: [mobile-device-test.md](../../user-guides/mobile-device-test.md). This document is updated with the result.

## 10. UAT scenarios
1. Sign in empty → two messages; wrong password → alert; right one → Dashboard.
2. POS: **Choose** a terminal (the list shows only active ones); search "cozy" → rows with price; tap one → the line appears with a toast; **More** → quantity 2; with no terminal chosen **Complete Transaction** stays disabled and a warning says why; scan: allow the camera, scan a product's barcode (its SKU) → one unit added once; an unknown barcode → toast, camera stays open; with an open cash shift the note says the sale is attached to it.
3. Inventory: search by name; open **Adjust**: choose a reason, type letters → "Enter a whole number."; type the current quantity → "Nothing to change"; a different number and **Send request** → green toast, and the request appears as Pending in the Admin's stock adjustments, where a manager approves it (the stock changes only then); Cancel closes the sheet.
4. Deliveries: a shipment shows its status chip and **Mark … in transit**.
5. Switch the phone to Vietnamese (stored locale): every screen reads Vietnamese.
