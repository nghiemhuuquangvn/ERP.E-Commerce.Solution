# Product import (CSV, streaming) — business analysis

Version 1.0, 2026-11-06 (ROADMAP M6). Technical pattern: `docs/architecture/streaming-import-pattern.md`. Pricing rules: `Pricing.md`.

## 1. Purpose and actors

A catalogue manager loads many products at once from a CSV file instead of creating them one by one, sees the progress while it runs and gets a row-by-row result.

| Actor | Role |
|---|---|
| Catalogue manager | Holds `products:manage`; prepares the CSV, runs a check, runs the import, reads the result. |
| ProductService | Validates and creates the products, default variants and retail prices. |
| Search index | Fed through `SearchIndexQueue` (existing); imported products appear in search after the queue runs. |
| POS / Reporting | Receive `ProductPriceChanged` / `ProductCreated` through the existing outbox, like any product created by hand. |

## 2. Process (main flow)

1. Open **Products > Import products** (button shown only with `products:manage`), download the template, fill it in.
2. Optionally tick **Check only**: the file is validated and looked up, nothing is saved; every valid row is reported as "would be created".
3. Start. The progress bar (share of the file read) and the counters follow the server; skipped and invalid rows appear in the list as they are found.
4. The summary says how many rows were created, skipped and failed. **Download problem rows (CSV)** keeps the list for correction; the corrected file can be imported again (rows that were created are then skipped as existing).
5. The product list refreshes. Products are created as **Draft** unless the row says Active.

## 3. Requirements and acceptance criteria

| ID | Requirement | Acceptance criterion |
|---|---|---|
| PI1 | Import products from a CSV of up to 20 000 rows / 25 MB without loading it all in memory. | A 3 000-row file is imported; a 20 001st row stops the import with "more than 20000 rows" after the rows already read were processed. |
| PI2 | A required SKU and name; the SKU is upper-cased and may hold letters, digits and `-` (1-50 characters); the name is 3-255 characters. | Rows `bad sku!`, an empty SKU, a 2-letter name are reported as `error` with their line and reason; nothing is created for them. |
| PI3 | A SKU that already exists in the shop, or repeats an earlier row of the file (any letter case), is skipped, never overwritten. | Reasons "A product with this SKU already exists." / "The SKU repeats an earlier row of this file."; a second import of the same file creates nothing. |
| PI4 | Category and brand are given by **code**; an unknown category code, or an unknown or inactive brand code, fails that row. | "Unknown category code 'X'." / "Unknown or inactive brand code 'X'."; the codes are matched without case. |
| PI5 | `price` (retail), `discountedPrice`, `cost`, `barcode`, `weightKg` create a **default variant** (same SKU as the product) and, with a price, a row of the shop's **Default retail** price list. | The product list shows the price and `variantCount = 1`; a `ProductPriceChanged` event is raised once per priced product. |
| PI6 | `status` Active publishes the product (it gets a variant for that); the default is Draft. | An Active row is Active in the list. |
| PI7 | Checks: price and discounted price 0 up to (not including) 1 000 000, the discounted price below the price and only with a price; cost, weight not negative; a decimal comma ("19,90") is read. | Each violation is an `error` row with the field named. |
| PI8 | **Check only** writes nothing. | Counters show would-create / skipped / invalid; the product list is unchanged. |
| PI9 | Progress is visible while it runs; the import can be cancelled. | The bar moves per batch; Cancel stops after the batch being saved; rows saved before stay saved (the dialog says so). |
| PI10 | A wrong or missing header is refused at once. | HTTP 400 "The header lacks the required column(s): sku." and nothing is created. |
| PI11 | Only one import per user at a time; only the user who started an import can watch it. | A second POST of the same user while one runs is 400 "Another import of yours is still running". |

## 4. Business rules

* **Columns** (header names are matched without case, spaces or underscores): `sku`*, `name`*, `description`, `productType` (Physical / Digital / Service / Bundle; default Physical; numbers are refused), `categoryCode`, `brandCode`, `price`, `discountedPrice`, `cost`, `barcode`, `weightKg`, `status` (Draft / Active). Delimiter: comma, semicolon or tab (Excel in Vietnamese locale saves with `;`). Encoding: UTF-8 (a BOM is fine).
* The default variant is created when any of price, cost, barcode, weightKg is given or the status is Active; a row with only identity fields creates a product with no variant (it cannot be published until a variant is added, as before).
* Prices are written to the **Default retail** price list (Retail channel; created on first use), quantity tier 1, never to `Cost` (R111).
* Valid rows are committed in batches of 200; a database failure rolls the batch back and stops the import (the batch's rows are reported "Not saved"). Rows of earlier batches stay.
* No update of existing products (owner decision): to change a product use the product screens.
* The old `POST products/bulk-import` (CSV inside JSON) still exists and is deprecated (R150).

## 5. Use cases

**UC1 Import a clean file (main).** As section 2.
**UC2 Import with problems (alternative).** Rows are skipped / failed with reasons; the manager downloads the problem rows, fixes them and imports again.
**UC3 Header refused (exception).** The dialog shows the server sentence; nothing was created.
**UC4 Cancel (exception).** The manager cancels; the dialog says rows already saved stay saved; the list refreshes.
**UC5 Server stops (exception).** A batch cannot be saved: the dialog shows "The import stopped: ..." and the rows reported before it.
**UC6 Network lost.** The browser stream ends: the dialog reports the generic failure; the server stops after the current batch (the client is gone).

## 6. Data and integration

* Written: `Products`, `ProductVariants`, `PriceLists` / `PriceListItems` (default retail), `OutboxMessages` (`ProductCreated`, `ProductPriceChanged`), `SearchIndexQueue`. Per batch: save 1 = products + variants, save 2 = price rows (they reference the variants by a foreign key that EF does not order: the first live run failed with `FK_PriceListItem_Variant` until the saves were split), save 3 = the price-changed events; all inside one transaction.
* Endpoints (Gateway paths `/prd/api/v1/products/...`): `POST import`, `GET import/{importId}/events`, `GET import/template`; permission `products:manage`.
* No new events, no new tables, no migration.

## 7. Reports

The NDJSON result lines and the summary are the report; the problem rows can be downloaded as CSV (`line,key,status,message`). Nothing is stored on the server.

## 8. Assumptions and open issues

Recorded in `docs/DEFERRED-ISSUES.md` R146-R153 (progress registry in one process, import speed about 10 ms per row, no unique index on `Products.Sku`, old JSON endpoint, Excel "CSV (comma)" saves ANSI, 20 000-row cap, HTTP/1.1 half duplex).

## 9. UAT

1. Download the template, import it as is: 2 products, the first Active with a price. 2. Import the same file again: all skipped. 3. A file with a short name, a negative price, an unknown category code: three `error` rows with line numbers; the others are created. 4. Tick Check only with 100 rows: nothing appears in the list. 5. A file with a wrong header: refused. 6. A 5 000-row file: the bar moves, Cancel stops it, the created rows stay. 7. A user without `products:manage` does not see the button; the endpoint answers 403.

## 10. Verification record (2026-11-06, Docker stack, glcheck, throw-away `ZZ-` rows)

Unit tests: Shared 149, Product 55 (25 new), Architecture 76, Admin Vitest 327 (21 new). Live through nginx + Gateway: 3 000, 2 000 and 1 500-row imports created every row with a price (`variantCount` 1, `price` set); SSE events arrived during the upload; the second run skipped, the bad-row file reported lines 3-5 with reasons, a wrong header was HTTP 400, no token was 401, a missing `importId` was 400. Speed measured: about 10-13 ms per row (200-row batches). **Not verified:** the Admin dialog in a real browser (covered by Vitest + axe only), a 20 000-row file, a user without the permission, ES search of the imported products. Throw-away rows left on glcheck: about 6 500 products `ZZ-IMP-*` / `ZZ-CASE-*` (see the closing report).
