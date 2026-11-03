# Customer mobile app — screens

Business analysis of the customer app (`src/frontend/mobile/customer`, Expo / React Native) as shipped on 2026-11-03 (ROADMAP P2-9, U5). The order, payment and delivery rules behind the screens are in [Sales](.), [Payment/OnlinePayments.md](../Payment/OnlinePayments.md) and [Shipping/ShippingFee.md](../Shipping/ShippingFee.md); the web twin is [StorefrontScreens.md](StorefrontScreens.md). **U5 changed the presentation only** (plus the build set-up in §9): no endpoint, payload, permission or business rule changed - the screen tests assert the sign-in, register and order requests field by field.

## 1. Purpose and scope
Let a customer sign in, browse and search the shop's products, review a cart, place an order with the payment method the shop offers, follow the order and its payment, and choose a language, from a phone.
In scope: Sign in, Register, Home, Search, Cart, Orders, Order detail with payment, Profile (language, sign out).
Out of scope / not built: **a product page and "add to cart"** (see §8, R108), reset password, coupon entry, shipment tracking, address book, push notifications, dark mode (U7), offline mode.

## 2. Actors
| Actor | Can | Cannot |
|---|---|---|
| Visitor | see the Sign in and Register screens | anything else (the app sends them to Sign in) |
| Customer (signed in) | browse, search, see the cart, place an order, see own orders and payments, change language, sign out | see another customer's order |
| Shop (tenant) | decides products, payment methods and delivery fee from its back office | - |
The shop is the tenant of the host in `EXPO_PUBLIC_API_URL` ([mobile-apps.md](../../frontend/mobile-apps.md)); the customer never chooses a tenant.

## 3. Screens and states
| Screen | Loading | Empty | Error | Notes |
|---|---|---|---|---|
| Sign in | button shows progress and is busy | - | danger alert "Invalid email or password." | labelled **Email \*** and **Password \***; an empty field shows "This field is required." under it and nothing is sent; the keyboard's *go* key signs in |
| Register | as above | - | danger alert | first name, last name (optional), email, password (hint: at least 8 characters) |
| Home | three skeleton tiles | "No products found." | alert + **Try again** | hero on the soft brand colour; featured products in a row; each tile is one button named "<product>, <price>" |
| Search | four skeleton tiles | "No products found." + hint + **Clear the search** | alert + **Try again** | labelled **Search products**; 2-column grid; "Out of stock" in the tile and its name |
| Cart | - (local) | "Your cart is empty" + **Browse Products** | order refused: red toast | each line: stepper named after the product ("More Blue shirt"), **Remove <product>**; summary: subtotal, total; payment methods as a radio group; the order button names the amount ("Place Order · $30.00") |
| Orders | three skeletons | "No orders yet." + hint + **Browse Products** | alert + **Try again** | each order is one card named "<no>, <status>, <total>, <date>"; translated status chip |
| Order detail | spinner | "Order not found." + **View my orders** | red alert when the payment failed or the gateway cannot be reached | lines, total (in the payment's currency), payment status, **Pay now** while an online payment is pending; polled every 3 s and when the app returns to the foreground |
| Profile | - | "Not signed in" + **Sign In** | - | name, e-mail, My orders, language radio group (English / Tiếng Việt), **Sign Out** (asks first) |

## 4. Rules (unchanged by U5)
1. **Order request** - customer id, name and e-mail from the signed-in user, the cart lines (product id, name, unit price, quantity) and the chosen payment method; nothing else. A refused order leaves the cart untouched.
2. **Payment methods** are what the shop offers (cash on delivery plus the gateways it switched on and configured); the first is preselected; none offered = the order button stays disabled.
3. **Online payment** - after the order is created the app opens the gateway's page; the order screen shows the result and offers **Pay now** if the gateway could not be reached.
4. Sign-out asks for confirmation; a 401 from the server signs the customer out.

## 5. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| MC-1 | One look from the design system | No raw palette class, numeric alias or hex colour in `app/`, `components/`, `lib/` (`test/noRawColours.test.ts`); screens are built from `components/ui` |
| MC-2 | Every field has a label | No placeholder-only input; a required field carries a star in its name; an error is attached to the field and announced (role alert) |
| MC-3 | Every region has its states | Skeleton / empty / error with a retry button on Home, Search and Orders |
| MC-4 | Touch targets | Every button, tab, radio and card is at least 44 pt (48 for primary buttons); checked in the walk at 375 px |
| MC-5 | Screen reader | Tabs, steppers, remove buttons, tiles and order cards have names; the cart tab says "Cart (n)"; toasts and alerts are live regions; decorative icons are hidden |
| MC-6 | Keyboard and safe areas | Forms keep working with the keyboard open (`keyboardShouldPersistTaps`, avoiding view); return keys go next / go / search; `SafeAreaProvider` at the root, screens keep clear of the notch and gesture bar |
| MC-7 | Text in both languages | Every string exists in English and Vietnamese; a test fails on a missing key |
| MC-8 | Contrast | Text 4.5:1 or better on its background (worst measured 4.70) |
| MC-9 | Behaviour unchanged | Sign-in / register / order requests equal the previous ones (asserted); money still shows US dollars (U6 changes that) |

## 6. Use cases
**UC-1 Place an order (cash on delivery).** Cart → choose *Cash on delivery* → **Place Order · $x** → Order detail ("You will pay the courier when the goods arrive").
*Exception - refused:* red toast "Failed to place order. Please try again."; the cart is kept.
**UC-2 Pay online.** …→ choose MoMo / ZaloPay / VNPay → place order → the gateway page opens → back in the app the order screen shows the result.
*Exception - gateway unreachable:* **Pay now** on the order screen.
**UC-3 Create an account / sign in.** Register or Sign in → Home.
*Exception:* alert with the reason; fields keep their text.
**UC-4 Change language.** Profile → Tiếng Việt: every screen switches at once and the choice is remembered.

## 7. Data and integration
Gateway routes used: `v1/auth/login`, `v1/auth/register`, `v1/products/featured`, `v1/products`, `v1/products/search`, `v1/orders`, `v1/orders/my`, `v1/orders/{id}`, `v1/payment-gateway/methods`, `v1/payment-gateway/orders/{id}/checkout`, `v1/payment-gateway/orders/{id}/status` (guarded by `ClientApiContractTests`). Cart, session and language are kept on the phone.

## 8. Assumptions and open issues
- **R108:** tapping a product opens `/product/<id>`, and that screen does not exist; `addItem` of the cart is never called. Today the cart can only hold what a previous version stored. The product screen and add-to-cart are the first customer-app feature to build.
- Money shows US dollars (the web shop's checkout shows dong): U6.
- Native build and device test: **not done** (R68) - see §9.
- No push notifications (the package is installed, nothing subscribes), no assets (icon, splash images), no store identifiers in `app.json`.

## 9. Build and verification status (2026-11-03)
Found and fixed while verifying: the app had **no `metro.config.js` and no `global.css`**, so NativeWind never turned a class into a style (the app ran unstyled); the lock file pinned `nativewind` 4.2.6, which needs `react-native-worklets`, so `expo export` failed (R68); the sign-in guard navigated before the root layout was ready. Now: `nativewind` 4.1.23 with `react-native-css-interop` 0.1.22, `metro.config.js` (+ `global.css`), a Babel plugin for `import.meta`, a guard that waits for the navigator.
Verified: 54 Jest tests, `tsc`, `check:tokens`; `expo export --platform web` and a headless-Chrome walk at 375 px (29 of 29: sign in errors, register through the UI, tabs, search empty state, cart steppers and radio group, language switch, touch targets, contrast, no console error). **Not verified: an Android / iOS build or a real device** (no Android SDK on the build machine).

## 10. UAT scenarios
1. Open the app signed out: Sign in appears; press **Sign In** empty → two messages, nothing sent.
2. Wrong password → red alert; right one → Home.
3. Home / Search: a tile reads "<name>, <price>"; search a word that matches nothing → message and **Clear the search**.
4. Cart with a line: **More** / **Fewer** change the total, **Remove** empties it; the order button names the amount.
5. Place a cash-on-delivery order → Order detail; Orders lists it with a status chip.
6. Profile → Tiếng Việt: all texts change; Sign Out asks first.
7. With the phone's font size at 130 %: no text is cut off and every button is still reachable.
