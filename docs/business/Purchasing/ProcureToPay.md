# Purchasing — RFQ, supplier invoices with three-way match, returns to vendor (ROADMAP P1-8)

Business analysis written 2026-10-26. It describes what the system does after P1-8. The older Vietnamese analyses (`PurchaseOrder.md`, `PurchasingWorkflow.md`, `Supplier.md`) describe the order itself and are partly out of date (see section 13). The ledger side is in `docs/business/Accounting/BA-Accounting.md` (FR-164…167, BR-217…219, UC-42, UAT-165…168) and `AutoPosting.md`.

## 1. Purpose and scope
Before P1-8 buying had three holes: there was no way to ask suppliers for prices and compare them; the accounts-payable bill was created for the **whole order the moment it was approved**, before any goods arrived and before any invoice existed (so a short delivery or a different price left the difference on the goods-received-not-invoiced account for ever); and there was no way to send goods back. Now:
- an **RFQ** goes to several suppliers, their quotations are compared and one is awarded — the awarded quotation becomes a draft purchase order at the quoted prices;
- the supplier's **invoice is recorded and matched** (order ↔ receipts ↔ invoice) against per-tenant tolerances; only an approved invoice becomes a bill, with the VAT and due date the supplier invoiced;
- **goods can be returned** to the supplier: stock leaves, the receipt accrual is reversed, and the supplier's credit note lowers what is owed.

Out of scope (DEFERRED §4s): e-mailing the RFQ to suppliers, a supplier portal, contracts and price lists, partial award of an RFQ line by line, a foreign-currency invoice converted automatically, batches / serials on a return, restocking replacement goods.

## 2. Actors and permissions
| Actor | Does | Permission |
|---|---|---|
| Buyer (Purchaser role) | RFQs, quotations, records supplier invoices, vendor returns | `rfq:read/manage`, `supplier_invoice:read/manage`, `vendor_return:read/manage` |
| Purchasing manager (Manager role) | also approves a matched invoice | `supplier_invoice:approve` |
| Finance / Admin | approves an invoice on hold (variance), sets the tolerances | `supplier_invoice:approve_variance`, `purchasing_settings:manage` |
| Warehouse | receives goods (existing), issues returned goods (automatic) | `purchase_order:receive` |
| System | books bills and debit notes (Kafka `purchasing-topic`) | service token |

Identity migration 029 grants the nine permissions to SuperAdmin / Admin (all), Manager (all but variance and settings) and Purchaser (also without approve).

## 3. Process
**Quote to order.** Create an RFQ (title, needed-by, lines by product code, suppliers to ask) → *Send* → enter each supplier's quotation (number, valid until, currency, lead time, a unit price for **every** line) or mark the supplier *Declined* → *Comparison* shows every price per line with the lowest highlighted, expired quotations greyed, and each supplier's total → *Award* a supplier with a valid quotation → a **draft purchase order** is created (lines and prices from the quotation, a note naming the RFQ and quotation), the RFQ closes. The order then follows the existing approval chain (manager; finance above 5,000).
**Receive.** *Receive goods* on the order (accepted / rejected quantity per line, partial receipts allowed): Warehouse books stock and the receipt accrual (Dr Inventory / Cr GRNI at the order price).
**Invoice.** *Record invoice* for an order: the supplier's invoice number, date, optional due date (default invoice date + the supplier's payment terms), tax, lines (product, quantity, unit price; *Fill with what can still be billed* proposes received − returned − already invoiced at the order price). The system matches at once: **Matched** or **OnHold** with the reasons. A matched invoice is approved by `supplier_invoice:approve`; one on hold only by `supplier_invoice:approve_variance` with a written reason; or it is rejected, or matched again after another receipt, a return or a change of tolerances. Approval publishes the invoice: Accounting books the bill.
**Return.** *New return* for an order (limited to received − already returned) → *Ship* (stock leaves) → when the supplier credits it, *Record credit note* (number, amount): the debit note is booked and the credit comes off what is owed on the order's bills.

## 4. Matching rules (per invoice line; defaults 0 % quantity, 2 % price)
Billable quantity = accepted received − returned − already invoiced on earlier **Matched or Approved** invoices of the order.
- Quantity: invoiced ≤ billable × (1 + quantity tolerance). Billing less than billable is fine (the rest comes later).
- Price: |invoiced price − order price| ÷ order price ≤ price tolerance, up **or down** (a large discount is a variance too).
- A product not on the order, or an invoice in another currency than the order, holds the invoice.
- Verdict per line: Matched, QuantityVariance, PriceVariance, QuantityAndPriceVariance, NotOnOrder; the result and the reasons are stored on the invoice.
**Worked example** (order of 20 shirts at 90, 10 received): invoice of 10 at 90 → Matched; 10 at 94.50 (+5 %) → OnHold "price is +5 % from the order price"; 5 more when all 10 are already invoiced → OnHold "invoiced 5 but only 0 received and not yet invoiced". With the tolerance set to 0 %, 211 against 210 (+0.48 %) is held; at 2 % it passes.

## 5. Requirements
| ID | Requirement | Acceptance |
|---|---|---|
| FR-P1 | RFQ with lines, invited suppliers; send needs lines and suppliers; lines and suppliers change only in draft | refusal messages; live |
| FR-P2 | A quotation prices every line, comes from an invited supplier and is still valid; a second quotation from the same supplier replaces the first | 400 on missing price / expired / not invited |
| FR-P3 | Comparison per line and per supplier, lowest price marked, expired quotations flagged | `GET rfqs/{id}/comparison` |
| FR-P4 | Award closes the RFQ and creates a draft order at the quoted prices, atomically; an expired or missing quotation is refused; awarding twice is refused | live |
| FR-P5 | A supplier invoice is stored (unique per supplier and number) with its lines and the stored match result per line | live |
| FR-P6 | Match with per-tenant tolerances (section 4), re-matchable until approved or rejected | unit tests (table cases); live |
| FR-P7 | Approving a matched invoice needs `supplier_invoice:approve`; one on hold also `supplier_invoice:approve_variance` and a reason; approval publishes `SupplierInvoiceApproved` | 400 without reason; unit tests |
| FR-P8 | Settings: quantity tolerance, price tolerance, auto-approve matched (never a held invoice) | `GET/PUT purchasing-settings` |
| FR-P9 | A return is limited to received − returned (drafts count); ship publishes `VendorReturnShipped`; a credit note (number, amount) publishes `VendorReturnCredited` | 400 beyond the limit; live |
| FR-P10 | Warehouse issues the returned stock once per return and product, all or nothing (movement `ReturnedToVendor`) | live: stock 21 → 19 |
| FR-P11 | The order approval no longer creates a bill | live: 0 bills after approval |
| FR-P12 | Admin screens (section 10) and *Receive goods* on the order | tsc; live API |

## 6. Business rules
- BR-P1 An RFQ is awarded to **one** supplier for all its lines; the other quotations are kept as lost.
- BR-P2 A quotation expires at the end of its valid-until day; an expired quotation cannot be recorded or awarded and is ignored for the lowest price.
- BR-P3 An invoice number is unique per supplier. An order that is Draft, Cancelled or Rejected cannot be invoiced. The invoice must be for the order's supplier.
- BR-P4 Tolerances are percentages of the billable quantity and of the order price; changing them affects only later matches.
- BR-P5 The invoice's net is the sum of its lines; the tax is what the supplier invoiced (no rate is assumed).
- BR-P6 Only an approved invoice becomes a bill. A bill entered by hand in Accounting still exists for expenses without an order.
- BR-P7 A return cannot exceed what was accepted on the order less earlier returns; goods already sold cannot be returned (Warehouse refuses when stock is not available).
- BR-P8 A shipped return cannot be cancelled; a credit note needs a shipped return.

## 7. Use cases
**UC-P1 Buy at the best price.** Buyer creates an RFQ for two suppliers, sends it, enters both quotations, opens the comparison, awards the cheaper → draft PO → submit and approve. Alt: a supplier declines; a quotation expires before the award → refused. Exc: a quotation missing a line price → 400.
**UC-P2 Pay a supplier only for what arrived.** Warehouse receives 10 of 20; the buyer records the invoice for 10; it matches; the manager approves; one bill is booked. Alt: the supplier invoiced 5 % more → on hold → a finance user approves with a reason → the extra 45 goes to price variance. Exc: invoice for more than received → on hold → rejected or matched again after the next receipt.
**UC-P3 Send damaged goods back.** Buyer creates a return of 2 shirts (limit shown), ships it → stock down 2; later records the supplier's credit note → what is owed falls by 180.
**UC-P4 Tune the tolerances.** Finance sets 0 % / 0 % to hold every difference, or raises them for low-value goods.

## 8. Data and events
Tables (Purchasing 012): `RequestForQuotations`, `RfqLines`, `RfqInvitations`, `SupplierQuotations`, `QuotationLines`, `SupplierInvoices`, `SupplierInvoiceLines`, `SupplierInvoiceMatchLines`, `VendorReturns`, `VendorReturnLines`, `PurchasingSettings`. Accounting 026: `Bills.SupplierInvoiceId` (unique), `ReceivedValue`, `CreditedAmount`.
States: RFQ Draft → Sent → Awarded | Cancelled. Invoice Recorded → Matched | OnHold → Approved | Rejected. Return Draft → Shipped → Credited | Cancelled.
Events (purchasing-topic, outbox, tenant-stamped): `SupplierInvoiceApproved` (invoice, supplier, order, dates, currency, net, tax, received value), `VendorReturnShipped` (lines with price), `VendorReturnCredited` (credit note, amount). Consumers: Accounting (bill, debit note), Warehouse (stock issue; 5 attempts then a logged error). `PurchaseOrderApproved` is still published but Accounting no longer acts on it.

## 9. Accounting effect (detail in BA-Accounting FR-164…167)
Receipt: Dr Inventory / Cr GRNI (order price). Approved invoice (net 1,050, tax 105, received value 1,000): Dr GRNI 1,000, Dr **purchase price variance** 50, Dr VAT input 105 / Cr Accounts payable 1,155. Goods sent back (2 × 90): Dr GRNI 180 / Cr Inventory 180. Supplier credit note 180: Dr Accounts payable / Cr GRNI 180, and the order's approved bills are credited oldest due date first; AP aging and payment use the open balance.

## 10. Screens (admin, Purchasing menu)
- **Requests for quotation** (`/purchasing/rfqs`): status filter; table number, title, lines, quotes received / invited, needed by, status; *New RFQ* form (title, needed-by, line rows, supplier checkboxes). Detail (`/purchasing/rfqs/:id`): lines, supplier list with states and *Enter quotation* / *Change quotation* / *Declined*, *Send to suppliers*, *Cancel RFQ*, the comparison matrix (lowest price green, expired grey, totals, an *Award* button per supplier) and, after the award, *Open purchase order*.
- **Supplier invoices** (`/purchasing/supplier-invoices`, filterable by order): table with status badge and the hold reasons; *Record invoice* (order list, invoice fields, a reference table of ordered / received / returned / invoiced / billable, editable lines, running net + tax). Detail: totals, hold banner, *Match again*, *Approve for payment*, *Approve with variance* (reason; shown only to holders of the variance permission, others see a hint), *Reject*, and the **match table** per line with a verdict badge.
- **Returns to vendor** (`/purchasing/vendor-returns`): table, *New return* (order, per-line "can still be returned", quantity, reason), *Ship*, *Cancel*, *Record credit note*.
- **Match settings** (`/purchasing/settings`): the two tolerances and the auto-approve box.
- **Purchase order**: *Receive goods* modal (remaining, accepted, rejected), links to its invoices and returns.

## 11. Assumptions
Product code on an order line equals the warehouse SKU. Invoices are in the order's currency; an invoice in another currency is held, and an approved one in a currency other than the books' leaves a Pending bill. Prices on an RFQ are per unit before tax.

## 12. UAT
| ID | Scenario | Expected |
|---|---|---|
| UAT-P1 | Quote, compare, award (B cheaper: 3,900 vs 4,000) | draft PO of supplier B at 90 / 210, note names the RFQ |
| UAT-P2 | Quote before send / missing price / expired / award twice | 400 each |
| UAT-P3 | Approve the order | no bill |
| UAT-P4 | Receive 10 of 20 shirts and 5 of 10 pants | stock +10 / +5; billable shown |
| UAT-P5 | Invoice at the order price, tax 195 | Matched; due = date + 45 days; approve → bill 2,145 (net 1,950, tax 195), entry 3388 Dr 1,950 / 1331 Dr 195 / 331 Cr 2,145; same number again 400 |
| UAT-P6 | Invoice 5 % over the price | OnHold with the reason; approve without reason 400; with reason → bill 945, price variance 45 on 632 |
| UAT-P7 | Invoice for more than received | OnHold "only 0 received and not yet invoiced"; reject |
| UAT-P8 | Return 100 shirts / 2 shirts, ship | 400 / stock 21 → 19, movement type 6, entry 3388 Dr 180 / 1561 Cr 180 |
| UAT-P9 | Credit note 180 | debit note 331 Dr / 3388 Cr 180; the first bill credited 180; AP aging down |
| UAT-P10 | Tolerance 0 % / 0 % then invoice +0.48 % | OnHold; at 2 % *Match again* → Matched |
| UAT-P11 | A user without the variance permission approves a held invoice | refused (unit test; the live admin holds the permission) |

Verified live on tenant `glcheck` 2026-10-26: UAT-P1…P10; UAT-P11 by unit test.

## 12b. Replenishment (P1-9)
Purchase requests now carry a quantity, the goods already on order and the product's preferred supplier, and *Create orders* turns them into draft purchase orders (one per preferred supplier) and one RFQ (products without a preferred supplier): see `docs/business/Warehouse/Replenishment.md`. An RFQ made that way follows the flow of section 3 from *Send* on.

## 13. Stale documents
`PurchaseOrder.md` §6 (no RFQ), `PurchasingWorkflow.md` (three-way match as a stateless call, bill from the order) and `Supplier.md` §5 are superseded by this document; each carries a pointer.
