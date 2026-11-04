# Server-side pricing of order lines and POS sale lines (R113)

> Written 2026-11-04 with branch `fix/r113-server-pricing`. Builds on [Product/Pricing.md](../Product/Pricing.md) (R111: what a product sells for). Related screens: [StorefrontScreens.md](StorefrontScreens.md), [MobileScreens.md](MobileScreens.md), [POS/StaffAppScreens.md](../POS/StaffAppScreens.md).

## 1. Purpose and scope
Until R113 **the client chose the price**: Sales took the `unitPrice` of every order and quotation line from the request (a customer could post `0.01`), and POS took the price of a sale line from the till. Now **the server prices every line**; a client's price is only a claim that must equal the system price, unless the person holds a special permission.

In scope: orders (Storefront, customer app, Admin), quotations (Admin), POS sale lines (staff app) and offline-synced POS sales, the new permission, and what each screen shows when the server's price differs from what the user saw.
Out of scope (risks in §12): a variant id on order / POS lines (the price is the product's cheapest variant), tax per product, POS discount control, price changes that happen only by date.

## 2. Actors
| Actor | Role |
|---|---|
| Customer (Storefront / customer app) | Orders at the system price; never sets a price |
| Staff with `sales_order:create` (Admin) | Creates orders / quotations; price pre-filled and locked |
| Staff with `sales_order:price_override` | May type another price (a manual price, noted on the order) |
| Cashier (staff app) | Sells at the price the server gives the line |
| ProductService | Answers `internal/v1/product-prices` (retail price per product) |
| POSService | Prices a line from its product cache (kept fresh by `ProductPriceChanged`) |

## 3. The rule
**For every order or quotation line** (`LinePricer`, one place):
1. Sales asks ProductService for the retail price of the products in one call. **If ProductService cannot answer, the order is refused** ("The prices cannot be checked right now. Please try again in a moment."), never priced by guess.
2. No price sent: the line is booked at the system price.
3. A price sent that equals the system price (to the cent): booked at the system price.
4. A price sent that differs: **refused** ("The price of <product> changed from <sent> to <now>. Review the prices and try again.") **unless the caller holds `sales_order:price_override`**, then the sent price is booked as a **manual price** and the order gets an internal note ("Manual price for <product>: system price <x>, booked at <y>.", author `price-override:<user id>`).
5. A product with no system price (none, or 0) cannot be ordered ("<product> has no price set, so it cannot be ordered."), unless the caller may override and sends a price.
6. The coupon, the delivery fee and the saga then run on the server-priced lines (nothing else changed).
7. A **quotation** follows the same rule when it is created; **converting** a quotation to an order keeps the quoted prices (a quotation is a commitment).

**For every POS sale line** (`AddTransactionItemCommandHandler`): the price sent by the terminal is **ignored**; the line takes the **POS product cache price** (the cache is fed by `ProductPriceChanged`, R111). A product the cache does not know, has switched off or prices at 0 is refused ("<product> has no price set, so it cannot be sold."), never sold as free. The reply (`TransactionDto`) carries the real unit price, tax and totals.

**Offline POS sales** (`sync-offline`): the customer already paid the price the terminal showed, so that price stands and the sale is booked; a line whose price differs from the cache (or has no known price) is listed in `PriceWarnings` of the result for a manager to review.

## 4. Requirements and acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| FR-1 | A customer cannot order at their own price | `POST orders` with `unitPrice: 0.01` answers 400 with the price sentence; nothing is saved, no stock is reserved |
| FR-2 | An order needs no client price | A line without `unitPrice` is booked at the system price; the order total is what the customer sees in the confirmation |
| FR-3 | Only the override permission may book another price | With `sales_order:price_override` the sent price is booked and an internal note names the user; without it the same request is refused; the role name has no effect |
| FR-4 | No price, no order | A product without a system price is refused for everyone who may not override |
| FR-5 | Fail closed | ProductService down: the order is refused with a "try again" sentence |
| FR-6 | POS prices its own lines | A line added with `unitPrice: 1` comes back at the cache price; an unknown / inactive / zero-priced product is refused |
| FR-7 | Offline sales are never lost | An offline sale at another price is booked at the price paid and flagged |
| FR-8 | Every screen tells the user what happened | §6 |
| FR-9 | The permission exists | `sales_order:price_override` is in the Roles screen (Identity migration 032); only the platform admin role holds it by default |

## 5. Business rules
- BR-1 The server decides the price; a client's price is never trusted.
- BR-2 A price differing from the system price needs `sales_order:price_override` (the permission, not a role name).
- BR-3 A customer is never charged a figure they did not see: when the server's price differs from the cart's, the order is refused and the screen reloads the cart prices.
- BR-4 A POS cashier is never charged silently either: a changed price stops the sale and the cashier confirms it.
- BR-5 Prices compare to the cent; the product's price is its cheapest priced variant (R111, R115).
- BR-6 A quotation's prices are binding at conversion.

## 6. The screens (what the user sees)
**Storefront (Vue)**
- **Cart and Checkout** check the remembered cart against the shop's current prices when they open. A changed price shows a yellow notice at the top: title **"Some prices changed"**, one line per product ("Blue shirt: $20.00 → $25.00"; or "...no price is set any more, so it was taken out of your cart"), the text "Your cart was updated. Check the total, then place the order." and a **Close** button. The totals already use the new prices; a product whose price vanished leaves the cart.
- **Place order refused**: if a price moved, the notice above appears (no generic error) and the cart keeps its lines; any other refusal shows the server's own sentence in the red alert (for example "Blue shirt has no price set, so it cannot be ordered."); no answer at all shows "Failed to place order". The button works again; nothing is cleared.
- **Product page / card**: a product with no price shows **"Price not available"** and a disabled Add to cart instead of $0.00.

**Customer app (Expo)**: the same notice (warning alert above the cart lines with a Close button), refreshed when the Cart tab opens and whenever its lines change; a refused order reloads the prices (notice) or shows the server's sentence in the toast; the product screen shows "Price not available" and disables Add to cart.

**Staff app POS (Expo)**
- **Complete Transaction** creates the transaction, adds the lines and reads the server's prices. If every price is as shown, it completes. **If any price differs, the sale is not completed**: the cart takes the server's prices (lines, tax and total update) and a warning shows **"Prices changed"**, one line per product, and "The prices were updated. Check the total, then press Complete again."; the cashier's second press only completes (the cashier's confirmation).
- A retry never adds the lines twice: when the lines are already on the server a retry only completes; when adding a line failed part-way, the next try starts a fresh transaction; changing the cart after the lines went up also starts a fresh one.
- A refusal's own sentence (for example "R68 test item has no price set, so it cannot be sold.") is shown instead of the generic "Failed to complete transaction."; a 403 still says the user may not ring up sales.

**Admin (React)** - Orders > New order, Sales > Quotations > New quotation
- The **Unit price** field is pre-filled with the system price. **Without `sales_order:price_override` it is read-only**, greyed, with the hint "Prices come from the system price list; they cannot be changed here."; the price is not sent. **With the permission** it is editable with the hint "A price typed here replaces the system price: it is recorded as a manual price on the order."
- A product with no system price shows under the field "This product has no price set: set it in Products > Variants first." (for someone who may override: "This product has no system price: type the price to use."); Save is refused until it is solved.
- A refusal from the server shows its own sentence under the form (it used to be "Failed to save").
- The field is gated by the permission list of the token, not by the role: a user with the role Admin but without the permission is locked.

## 7. Use cases
**UC-1 Customer orders (main).** The customer fills the cart and checks out; the order is booked at the system prices and the confirmation shows the real total.
*Alternative:* a price moved since the cart was filled: the cart notice (or the refusal) shows the new price; the customer decides again. *Exception:* ProductService is down: "try again"; a product lost its price: it leaves the cart.

**UC-2 Staff gives a manual price.** A user with the permission types a lower price on an order line; the order is booked at it and an internal note records who and from what.

**UC-3 Cashier sells.** The cashier adds products, presses Complete; the server prices the lines; if nothing changed the sale completes; if a price changed the cashier reads the notice and presses Complete again.

**UC-4 Offline sale syncs.** The sale is booked at the price paid; the warning list tells a manager which lines differ.

## 8. Data and integrations
No new table. New permission row `sales_order:price_override` (AuthDB migration 032). Sales -> Product: `POST internal/v1/product-prices` (`{productIds}` -> `{prices:[{productId, price}]}`, ≤ 200 per call, service token `salesorder`, config `Services:ProductServiceBaseUrl`, Product `ServiceAuth:TrustedCallers:salesorder`). POS reads its own cache (`PosProductCache`).

## 9. Permissions
`sales_order:price_override` (new): book an order / quotation line at another price. `sales_order:create` and `pos:transactions` as before. The shop grants the new permission to its own roles in Settings > Roles; only SuperAdmin holds it by default.

## 10. Reports
None added. Manual prices are visible in the order's internal notes ("Manual price ...").

## 11. Assumptions
One shop, one currency; the system price of a product is its cheapest variant's retail price; the POS cache is event-fed.

## 12. Open issues (DEFERRED-ISSUES)
R118 order and POS lines carry no variant id (dearer variants are under-priced for customers); R119 a quotation keeps its quoted price at conversion even if the list moved; R120 offline price warnings are returned by the API only (no screen); R121 a failed POS completion can leave an open, never completed transaction on the server (no money / stock); R122 POS discount is still a free amount and the tax rate is client-sent (R115); R114 date-only price changes raise no event, so the cache can be stale.

## 13. UAT scenarios
| # | Scenario | Expected |
|---|---|---|
| U1 | Customer posts an order at 0.01 | 400, price sentence, nothing written |
| U2 | Customer posts an order without a price | Created at the system price |
| U3 | Cart saved at $500, shop price $626.99, open the Cart page | Notice "$500.00 → $626.99", total $626.99 |
| U4 | Same, then Place order | Order refused or created at the new price; notice shown; cart kept when refused |
| U5 | Unpriced product ordered | Refused: "has no price set" |
| U6 | Admin user without the permission opens New order | Price read-only, hint shown, request sent without a price |
| U7 | Admin user with the permission types 20 | Booked at 20, internal note with the user |
| U8 | POS add line with unitPrice 1 | Line at the cache price |
| U9 | POS price changed between the cashier's view and the server | Not completed; "Prices changed"; second press completes |
| U10 | POS retry after a failed complete | Only `/complete` again, quantities not doubled |
| U11 | ProductService stopped, customer orders | "The prices cannot be checked right now" |
| U12 | Offline POS sale at another price | Booked, `PriceWarnings` lists the line |

## 14. Verification record (2026-11-04)
Unit tests: Sales 102 (16 new + 4 controller), POS 27 (9 new), Product 30 (2 new), Architecture 76, Admin 306 (8 new), Storefront 46 (11 new), customer app 72 (11 new), staff app 64 (6 new), `tsc` and `check:tokens` clean. Live on glcheck (deployed `productservice`, `salesorderservice`, `posservice`, `storefront`, `admin`; migration 032 applied): order at 0.01 -> 400 "changed from 0.01 to 626.99", order at 631.99 -> 400, unknown product -> 400 "has no price set", the glcheck admin (role Admin, no permission) at -1 -> 400, all with **no rows written** apart from the sign-ins; one order without a price -> 201 at 626.99 (the rows it wrote are in the report); POS line with `unitPrice: 1` -> 12,500 (total 13,750 with 10 % tax), unknown product -> 400. Browser walk: Storefront cart with an old $500 price shows the notice and updates the remembered price; Admin order and quotation forms read-only for the shop admin; 0 page errors.

Emulator (Pixel 6, rebuilt APKs): the customer app showed the cart notice "Some prices changed - CozyNest Haven K20 Queen Size: $626.99 -> $650.00" after the shop price was changed, with the cart updated to $1,300.00 for two and a Close button; the staff POS picker listed the product at the new price $650.00. **Not driven on the emulator: the staff POS "Prices changed" stop and the cashier's second press** - the harness refused the Complete Transaction press (a sale is not made by the assistant), so that flow is covered by the 6 new Jest tests only; the owner can try it: add a product in POS, change its price in Admin (Products > Variants > Set price), press Complete Transaction once (it stops with the yellow notice), then press it again to sell. The demo product price changed during the check was set back to 626.99.
