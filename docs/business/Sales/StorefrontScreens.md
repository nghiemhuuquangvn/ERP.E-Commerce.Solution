# Storefront — customer-facing screens

Business analysis of the web shop (`src/frontend/storefront`, Vue 3) as shipped on 2026-11-02 (ROADMAP P2-9, U4). The order, payment and delivery rules behind the screens are in [Sales](.), [Payment/OnlinePayments.md](../Payment/OnlinePayments.md) and [Shipping/ShippingFee.md](../Shipping/ShippingFee.md); this document describes what the customer sees and what is guaranteed about it. **U4 changed the presentation only**: no endpoint, payload, route, permission or business rule changed.

## 1. Purpose and scope
Let a visitor find a product, put it in a cart, sign in or register, check out and see what happened to the order and the payment, in English or Vietnamese, on a phone or a desktop.
In scope: shell (header, mobile menu, footer), Home, Product list, Product detail, Cart, Checkout (3 steps), Payment result, Order confirmation, Account (Orders, Profile), Sign in, Register.
Out of scope (ROADMAP P2-2, not built): reset password, confirm e-mail, coupon entry, shipment tracking, address book, wishlist, loyalty points; a category bar and filters (the storefront has no categories endpoint); variants and reviews tabs.

## 2. Actors
| Actor | Can | Cannot |
|---|---|---|
| Visitor (anonymous) | browse, search, sort, add to cart, view the cart, sign in, register | check out, see orders or profile (redirected to Sign in, then back) |
| Customer (signed in) | everything above, check out, see orders and profile, sign out | see another customer's order |
| Shop (tenant) | decides products, payment methods offered and delivery fee from its back office | — |
The shop is the tenant of the host (`<shop>.shop...`); the customer never chooses a tenant.

## 3. The screens and their states
Every data region has the five states of [patterns-web.md](../../frontend/design-system/patterns-web.md) §1:
| Screen | Loading | Empty | Error | Notes |
|---|---|---|---|---|
| Home | skeleton tiles | "No products found." | alert with **Try again** | hero on the soft brand colour, one primary button |
| Product list | skeleton tiles (12) | "No products found." + hint and **Clear the search** | alert with **Try again** | labelled **Search** and **Sort by**, grid 2 / 3 / 4 columns, page counter "Page n of N" with Prev / Next |
| Product detail | skeleton | "This product could not be found." | alert with **Try again** | price in bold with tabular digits, stock note ("In stock" / "Out of stock"), quantity stepper, **Add to Cart** (disabled when out of stock); below 768 px the controls stay in a bar at the bottom of the screen; "Added to cart" appears as a toast |
| Cart | — (local) | "Your cart is empty." + **Browse products** | — | each line: stepper ("Quantity of X", one fewer / one more) and **Remove** named after the product; summary: subtotal, coupon discount, a note that **delivery is worked out at checkout**, total |
| Checkout | spinner while the shop's payment methods load | warning when the shop offers no method | alert when the order is refused | see §4 |
| Payment result | spinner "Checking your payment…" (polls up to 60 s) | — | — | one icon and sentence per status: paid, failed, cancelled, pending ("Pay now / Pay again"); an unverified return is explained |
| Order confirmation | — | — | — | order number, total with the delivery fee (or "Free"), "You will pay the courier" for cash on delivery, payment line |
| Account › Orders | skeleton | "You haven't placed any orders yet." + **Browse products** | alert with **Try again** | order number, date, translated status badge, total |
| Account › Profile | — | — | — | name and e-mail, **Sign Out** |
| Sign in / Register | button progress | — | danger alert (wrong credentials / registration failed) | labelled required fields, centred card, all text in both languages |

## 4. Checkout rules (unchanged by U4, now visible)
1. **Shipping** — required: street, phone, city, country (ward, state, postal code optional). The phone must have at least 8 digits. Pressing **Continue →** with something missing shows the message **under each field** (linked to it) and stays on the step.
2. **Payment** — a radio group of what *this shop* offers (cash on delivery plus the gateways it has switched on): VNPay, MoMo, ZaloPay. Nothing is selectable if the shop offers none.
3. **Review** — address, method, lines, **goods**, **delivery fee** (loading / "could not be worked out - added when the order is placed" / "Free" / amount; "Free delivery from X" when a threshold exists), **total**. The button names the amount once the fee is known: "Place Order · 150.000 ₫" / "Place order and pay · …". The fee shown is a preview; the server recomputes it when the order is placed.
4. Placing the order sends the same data as before (customer, address with phone, lines, payment method). Cash on delivery goes to the confirmation page; an online method goes to the gateway, and if the gateway cannot be reached to the Payment result page with **Pay now**.
5. Moving between steps keeps what was typed; the focus moves to the page title so a screen-reader user hears where they are.

## 5. Money display (known inconsistency, isolated for U6)
Amounts are shown as US dollars in the cart, product pages and order list, and as Vietnamese dong at checkout once cash on delivery or an online method is chosen, on the confirmation and on the payment result (in the payment's own currency). This is the behaviour before U4; all formatting now goes through one composable (`useMoney`), so U6 can replace it with one rule per the content standard.

## 6. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| SF-1 | One look from the design system | No raw colour class or numeric alias in the source (`noRawColours.test.ts`); controls, buttons, alerts, toasts, drawer and steps are the shared components in `src/components/ui` |
| SF-2 | Every field has a label | No placeholder-only input; required fields carry a star and `aria-required`; an error is linked with `aria-describedby` and announced |
| SF-3 | Every region has its states | Skeleton / empty / error with retry on Home, list, detail, orders; the retry button reloads that region only |
| SF-4 | Works on a phone | No horizontal scroll at 375 px on any page; a menu button opens a drawer (focus moves in, Esc closes and returns the focus); the add-to-cart bar is fixed at the bottom of the product page; the chat button sits above it |
| SF-5 | Keyboard and screen reader | First Tab lands on **Skip to content**; one `main`; headings in order; steppers and remove buttons are named; the checkout stepper marks the current step; the cart link says "Cart, n item(s)"; toasts and statuses are live regions |
| SF-6 | Text in both languages | Sign in, Register, Profile and every new string exist in en and vi; a test fails on a missing key |
| SF-7 | Money rules | Prices bold with tabular digits; the old price struck through with the percentage off when a discount exists (component ready, used when the API sends one); "Free" in words; the pay button states the amount |
| SF-8 | Behaviour unchanged | The order create request equals the previous one field by field (asserted); fee preview, payment methods, redirect to the gateway and polling are untouched |

## 7. Use cases
**UC-1 Buy with cash on delivery.** Home → Products → pick a product → Add to Cart (toast, header count) → Cart → Proceed to Checkout → (sign in if asked) → address → Payment: Cash on delivery → Review → Place Order → confirmation.
**UC-2 Buy online.** …→ choose VNPay / MoMo / ZaloPay → Place order and pay → gateway → Payment result (spinner, then "Payment received").
*Exception — gateway unreachable:* the order exists; the Payment result page offers **Pay now**.
*Exception — payment fails:* "The payment was not completed … the order has been cancelled".
**UC-3 Create an account.** Register → name, e-mail, password (8+ characters) → signed in on Home.
**UC-4 Find an order.** Account → My orders → status badge and total.
**UC-5 On a phone.** Menu button → drawer with Products, Cart, account links, sign out; language selector stays in the header.

## 8. Permissions and data
Public: catalog, search. Signed-in customer (token from Identity, tenant from the host): orders, payments, shipping fee preview. No new endpoint. The cart and the language are kept in the browser.

## 9. UAT scenarios
1. Open Home on a phone-size window: no sideways scroll, menu button works with Enter, Esc closes it.
2. Search a word with no result → message and **Clear the search** returns the list.
3. Add a product twice → toast, header says "Cart, 2 item(s)"; change quantity in the Cart; Remove it by its named button.
4. Checkout: press Continue empty → four messages under the fields; fill → Payment lists only the shop's methods → Review shows goods, delivery, total and the amount on the button.
5. Register a new customer → signed in; sign out and sign in with a wrong password → alert; the right one → Home.
6. Switch to Vietnamese: sign-in, register, profile and the new texts are Vietnamese.
7. A payment result page for a pending, paid, failed and cancelled payment shows one sentence and one icon each.

## 10. Assumptions and open issues
- The demo products of the test shop have price 0, so totals read 0 in the walk-through; the layout was checked with these values.
- No product images in the test data: the placeholder icon is shown.
- Categories and filters need a storefront categories endpoint (not in the client today).
- Dollar / dong display is inconsistent by design of the earlier code (U6).
