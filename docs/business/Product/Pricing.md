# Retail pricing (R111)

> Written 2026-11-03 with branch `feat/r111-retail-price`. Replaces the "Cost vs Price" warning of [module-09-product.md](module-09-product.md) §8 (which describes the state before this change).
> Related: [BA of the apps that show the price](../Sales/StorefrontScreens.md), [POS staff app](../POS/StaffAppScreens.md), [event catalog](../../architecture/event-catalog.md).

## 1. Purpose and scope
Until R111 **no client ever saw a selling price**: `ProductDto.Price` was a hard-coded 0, the "Set price" API overwrote the variant's *cost*, and the price events sent to POS and Sales carried that cost. A shop could not sell anything with a price. This document defines **what a product sells for at retail, where it is kept, how it is shown and how the other services learn that it changed.**

In scope: the retail price of a product / variant, the "Default retail" price list, the price shown by every read endpoint, the `ProductPriceChanged` event, republishing, the Admin dialog.
Out of scope (own risks, see §12): server-side re-pricing of order and POS lines (R113), price changes that happen only by date (R114), currency conversion and number formatting (R116 / U6), tax per product.

## 2. Actors
| Actor | Role |
|---|---|
| Merchandiser (permission `products:edit`) | Sets a variant's retail price in the Admin (Product > Variants > **Set price**); may create other price lists (Products > Price Lists) |
| Customer (Storefront / customer app) | Sees the price; never changes it |
| Cashier (staff app POS) | Sees the price of the product and the one cached by POS |
| POS service, Sales service | Consume `ProductPriceChanged` (POS refreshes its price cache, Sales flags open orders that contain the product) |

## 3. The price model
**Source of truth = the price lists of ProductService** (`PriceList` aggregate with `PriceListItem` rows: variant, base price, optional discounted price, minimum quantity; channel Retail / Wholesale / Ecommerce; effective date; optional expiry date; active flag). The variant's `Cost` is **cost only** and is never a selling price.

### 3.1 The rule (one place: `RetailPriceRules`)
For one variant, take the rows of lists that are **active**, **already effective** (`EffectiveDate <= now`) and **not expired**; then
1. keep the **Retail** channel first, the **Ecommerce** channel only when there is no Retail row; a **Wholesale** list is never a retail price;
2. among those, the list with the **newest effective date** wins;
3. within it, the **lowest quantity tier** (the price for buying one unit);
4. the price is the **discounted price when there is one, otherwise the base price**.
No row = **no price**.

A **product's price** is the price of its **cheapest priced variant** (a "from" price, the same figure Elasticsearch calls `PriceMin`). A product with no priced variant has price **0** and clients show it as "no price".

The same rule feeds the product DTOs, the price list screen's "retail price" column and the Elasticsearch document, so every client agrees.

### 3.2 "Default retail" list
`POST pricing/variants` (the Admin **Set price** dialog) writes one row (quantity tier 1) to the shop's **"Default retail"** list: Retail channel, active, effective from the moment it is created, no expiry. The list is created the first time a price is set. Editing the price changes that row; the cost is **not touched**. Other lists (promotions, wholesale, a newer retail list) are created in *Price Lists* and take part in the rule above.

## 4. Requirements and acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| FR-1 | Every product read shows the retail price | `GET products`, `products/featured`, `products/{id}`, `products/sku/{sku}` and the SQL search answer `price` = the rule's result for that product; a product with no price answers 0 |
| FR-2 | Prices are read in bulk | A page of products costs two price queries, not one per product |
| FR-3 | Setting a price writes the price list, not the cost | After `POST pricing/variants`, `GET pricing/products/{id}` shows `retailPrice` and the unchanged `cost`; a discounted price not below the base price is refused (400) |
| FR-4 | A price change is announced | When the product's retail price changes, exactly one `ProductPriceChanged` (old, new = the product-level prices) is written to the outbox in the same transaction; no event when the price did not change |
| FR-5 | Creating a price list or moving it to / from the retail channels re-evaluates the affected products | Same event rule as FR-4 |
| FR-6 | POS catches up | `POST pricing/republish` raises one event (old = new) for every priced product; POS cache rows take the price; Sales does not flag orders for it |
| FR-7 | The Admin shows and sets the price | Variants tab: **Retail price** column ("Not set" when none), **Set price** dialog (price, optional discounted price below it), the product list shows the price |
| FR-8 | The search index agrees | The Elasticsearch document uses the same rule |

## 5. Business rules
- BR-1 A wholesale price is never shown as the retail price.
- BR-2 Retail beats Ecommerce even when the Ecommerce list is newer (a deliberate, simple rule; a per-channel price view is a later feature).
- BR-3 An expired, inactive or future-dated list does not count.
- BR-4 The price in `ProductPriceChanged` is the **product's** price (POS keeps one price per product); `VariantId` names the variant that carries it.
- BR-5 A re-sent event (old = new) is not a change: Sales ignores it (`SalesOrder.FlagPriceChanged`).
- BR-6 Price rows are never negative; the discounted price is below the base price; a price is below 1,000,000.
- BR-7 The cost of a variant is edited only through the variant commands.

## 6. Use cases
**UC-1 Set the retail price (main flow).** A merchandiser opens Product > Variants, presses **Set price** on a variant, types the price (and optionally a discounted price) and saves. The Default retail list gets / changes the row, the product list and storefront show the price, POS's cache is refreshed.
*Alternative:* the variant already has a row in the list: the row is updated. *Exception:* discounted price not below the price: the dialog says so and sends nothing; the variant does not exist: 404 shown in the dialog.

**UC-2 A newer list takes over.** A merchandiser creates a Retail list effective today with a lower price for a variant: the rule picks it, one event is raised for the product.

**UC-3 Catch up a POS cache.** After deploying R111 (or when a cache is suspected stale) an administrator calls `POST pricing/republish`; POS caches take today's prices.

**UC-4 Customer browses.** The Storefront and customer app list the product with the price; a product with no price shows 0.

## 7. Data and state
`PriceLists` (`Name`, `Channel`, `IsActive`, `EffectiveDate`, `ExpiryDate`, `CurrencyId?`, tenant) and `PriceListItems` (`PriceListId`, `VariantId`, `BasePrice`, `DiscountedPrice`, `MinQuantity`) already existed (Product migrations 001, 007, 010, 018); **no migration was needed**. The "Default retail" list is a normal row with that name.

## 8. Integrations
`ProductPriceChanged` (Kafka `product-topic` through the outbox, tenant-stamped): `ProductId`, `VariantId`, `OldPrice`, `NewPrice`. Consumers: **POS** (`PosKafkaConsumer` -> `PosProductCache.Price`), **Sales** (flags open orders whose lines contain the product when the price really changed). Promotion and Reporting do **not** consume it (older docs said so). Elasticsearch is refreshed through the usual search-index queue.

## 9. Permissions
`POST pricing/variants`, `POST pricing/lists`, `PUT pricing/lists/{id}/channel`, `POST pricing/republish`: `products:edit`. Reads (`GET pricing/products/{id}`, `GET pricing/lists/{id}`, the product endpoints) stay anonymous as before.

## 10. Reports
None added. The price is on the product list and the order / quotation forms (which prefill the unit price from it).

## 11. Assumptions
- A shop works in one currency (`CurrencyId` of a list is ignored here); the apps still format US dollars until U6.
- The price of a product with several variants is the cheapest one.
- The "Default retail" list name is reserved for this flow.

## 12. Open issues (DEFERRED-ISSUES)
R113 server re-pricing of orders / POS lines (a client can still send any `unitPrice`); R114 date-only price changes raise no event; R115 POS keeps one price per product; R116 currency and formatting; R117 the R68 test product has no stock, and the POS picker blocks out-of-stock products.

## 13. UAT scenarios
| # | Scenario | Expected |
|---|---|---|
| U1 | Set 12,500 on a variant without a price | Product list, storefront, customer app, POS search show 12,500; `retailPrice` 12,500 and `cost` unchanged on `pricing/products/{id}`; POS cache row = 12,500 |
| U2 | Set 12,500 again | No new event |
| U3 | Set price 100 and discounted 120 | Refused: discounted price must be below the price |
| U4 | Wholesale-only product | Shows 0 (no retail price) |
| U5 | Two retail lists, the newer one cheaper | The newer one's price shows |
| U6 | `POST pricing/republish`, POS cache of one product set to 0 beforehand | The cache returns to the price; no open order is flagged |
| U7 | User without `products:edit` | No **Set price** button; the API answers 403 |

## 14. Verification record (2026-11-03, tenant glcheck, throw-away product `R68-TEST-001`)
Unit tests: ProductService 28, Sales 2 new, Architecture 76, Admin 298. Live: `featured` showed the seeded prices (626.99, 46.99 ...) and 0 for the unpriced R68 item; after the Admin call (204) the item showed 12,500.00 in `featured`, `sku/...`, `products/{id}` and `pricing/products/{id}` (cost still 10,000); the POS cache moved 10,000 -> 12,500 **through the event**; with the cache row set to 0 by SQL, `republish` (31 events for 31 priced products) brought it back and `pos/products/lookup/R68-TEST-001` answered 12,500; no open order was flagged. Browser walks: Admin product list, Variants tab and the Set price dialog (opened, cancelled); Storefront home; customer app on the emulator (12,500.00 / 626.99 / 46.99); staff POS search showed the price (an out-of-stock product cannot be added by search).
