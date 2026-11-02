# Purchasing — Admin back office (Purchase orders, RFQs, Supplier invoices, Returns to vendor, Match settings)

Business analysis of the Purchasing screens of the Admin as shipped on 2026-11-02 (ROADMAP P2-9, U3 batch 7): **Purchase orders** (list, detail, receive goods), **Requests for quotation** (list, detail, quotation), **Supplier invoices** (list, record, detail with the three-way match), **Returns to vendor**, **Match settings**. The business rules behind them are in [ProcureToPay.md](ProcureToPay.md), [PurchaseOrder.md](PurchaseOrder.md), [PurchasingWorkflow.md](PurchasingWorkflow.md) and [Supplier.md](Supplier.md); this document describes **what the screens do and how a person is meant to use them**. Vietnamese user guide: [docs/user-guides/purchasing.md](../../user-guides/purchasing.md). Not in this batch: the supplier list / form, the purchase order form and the purchase request proposal page (still on the old classes, see R92).

## 1. Purpose and scope
Let a buyer ask suppliers for prices and pick one, let approvers release purchase orders, let the warehouse record what arrived, let accounts-payable check what the supplier invoiced against what was ordered and received, and send goods back. In scope: the screens above and the PurchaseOrderService API behind them. **Out of scope today (no screen):** creating a purchase order from an awarded RFQ by hand (award does it), editing an RFQ after sending, a partial award, undoing a goods receipt, supplier payment (Accounting / Payment), credit-note matching beyond recording the amount.

## 2. Actors and permissions
| Actor | Does | Permission |
|---|---|---|
| Buyer | Creates, sends, cancels RFQs; records quotations; marks a supplier as declined; awards | `rfq:manage` (`rfq:read` to see) |
| Approver | Approves or rejects a submitted order (reason on reject) | `purchase_order:approve` |
| Finance approver | Gives the finance approval of an order above the limit | `purchase_order:finance_approve` |
| Purchaser | Cancels an order that is not final | `purchase_order:cancel` |
| Receiver (warehouse) | Records goods received (accepted / rejected quantities) | `purchase_order:receive` |
| AP clerk | Records a supplier invoice, matches it again, rejects it | `supplier_invoice:manage` (`:read` to see) |
| AP approver | Approves a matched invoice | `supplier_invoice:approve` |
| AP supervisor | Approves an invoice that is **on hold** (a variance) | `supplier_invoice:approve_variance` |
| Returns clerk | Creates, ships, cancels returns; records the supplier's credit note | `vendor_return:manage` (`:read`) |
| Purchasing admin | Sets the match tolerances and auto-approval | `purchasing_settings:manage` |
A button appears only with its permission.

## 3. Process overview
```
RFQ (Draft) ─send─> Sent ─quotations─> compare ─award─> draft Purchase Order ─submit─> approve ─(finance)─> ORDERED
                     └cancel                                            └reject (reason) / cancel
Purchase Order ─receive (accepted / rejected)─> PARTIALLY_RECEIVED / RECEIVED   (Warehouse adds the accepted units to stock)
Supplier invoice recorded ─auto match vs order + receipts + returns + earlier invoices─> Matched | OnHold
   Matched ─approve─> Approved   (Accounting books the bill)      OnHold ─approve with variance (reason + permission)─> Approved
   any open ─reject (reason)─> Rejected
Received order ─return to vendor─> Draft ─ship─> Shipped (Warehouse issues stock) ─credit note─> Credited   |  Draft ─cancel
```

## 4. State models and rules
### 4.1 Purchase order (`DRAFT → SUBMITTED → [PENDING_FINANCE_APPROVAL] → APPROVED → ORDERED → PARTIALLY_RECEIVED → RECEIVED`; `REJECTED`, `CANCELLED`)
List screen actions by status: **Approve** (SUBMITTED), **Finance approve** (PENDING_FINANCE_APPROVAL), **Reject** (SUBMITTED or PENDING_FINANCE_APPROVAL; a written reason is mandatory), **Cancel** (anything except CANCELLED / RECEIVED / REJECTED; already confirmed before this batch). **Approve and Finance approve now ask for confirmation** and name the order. Failures show the server's reason as a toast (they were silent). **Receive goods** (detail page) is offered for approved / ordered / partially received orders: the dialog lists each line with the remaining quantity and one accepted and one rejected input; each line's accepted + rejected may not exceed the remaining quantity; **Record receipt** opens a confirmation ("Add n accepted unit(s) to stock (m rejected)? A receipt cannot be undone.") and only then posts.

### 4.2 Request for quotation (`Draft → Sent → Awarded | Cancelled`; invitation `Invited → Quoted | Declined`)
- **New RFQ:** title (required), needed-by (not in the past), at least one line with a product code and quantity ≥ 1, the suppliers to ask. A **Draft** with no supplier can be saved but **cannot be sent** (the server refuses: "Invite at least one supplier before sending the RFQ" — shown as a toast).
- **Send** (confirm: the invitations go out and the lines can no longer be edited), **Cancel RFQ** (confirm), **Mark as declined** (confirm, per supplier).
- **Quotation** per invited supplier while the RFQ is Sent: number, valid-until (default today + 14, not in the past), currency, lead time and a price for **every** line (≥ 0). Editing replaces the supplier's quotation.
- **Compare:** one column per quotation; the lowest unit price per line is marked; an expired quotation is greyed and labelled "expired" and cannot be awarded; the supplier with the lowest total gets a star.
- **Award** (confirm naming the supplier: "A draft purchase order is created at <supplier>'s quoted prices and the RFQ is closed") creates the draft order and opens it.

### 4.3 Supplier invoice (`Recorded → Matched | OnHold → Approved | Rejected`)
- **Record:** choose an approved / ordered / received order; the screen shows ordered / received / returned / invoiced / billable per line; **Prefill billable** fills the lines at the order price; number is required, invoice date defaults to today, due date empty = invoice date + the supplier's payment terms, tax ≥ 0. The invoice is matched at once ("Record and match").
- **Match result per line:** Matched, QuantityVariance (over what was received and not yet invoiced beyond the tolerance), PriceVariance (off the order price beyond the tolerance), QuantityAndPriceVariance, NotOnOrder. Any variance puts the invoice **OnHold** with the reasons listed.
- **Approve** (Matched) and **Approve with variance** (OnHold; needs a written reason and `supplier_invoice:approve_variance`) **book the supplier bill in Accounting and are final**; the confirmation says so. **Reject** (a written reason is required) says the supplier must send a new invoice and nothing is booked. **Match again** repeats the match (after a receipt or a return changed the facts).
- Only an approved invoice publishes `SupplierInvoiceApproved`; the price difference goes to `PurchasePriceVariance`.

### 4.4 Return to vendor (`Draft → Shipped → Credited`, or `Cancelled`)
**New return:** a received order, a quantity per line (≤ returnable, at least one > 0) and a reason. **Ship** (confirm: the goods leave the warehouse stock now) publishes `VendorReturnShipped`; **Cancel** (Draft only, confirm: no stock changes); **Record credit note** (Shipped) takes the supplier's credit-note number and amount (> 0) and publishes `VendorReturnCredited` (Accounting books the debit note and credits the order's bills).

### 4.5 Match settings
Quantity tolerance % (default 0) and price tolerance % (default 2), each 0–100, and **Approve matched invoices automatically** (never an invoice on hold; it books bills without a person — the screen says so).

## 5. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| PU-1 | A step that releases an order, books a bill or moves stock is never one click | Approve / Finance approve (order), Receive goods, Approve / Approve with variance / Reject (invoice), Ship / Cancel (return), Send / Cancel / Award / Decline (RFQ) open a dialog that states the consequence; **no request is sent until it is confirmed**; Esc / Cancel change nothing |
| PU-2 | A reason is captured where the rules require one | Reject order, Reject invoice and Approve with variance cannot be sent with an empty reason; the reason travels with the request |
| PU-3 | Quantities are bounded before sending | Receive: accepted + rejected ≤ remaining per line (warning, button disabled); Return: quantity ≤ returnable; RFQ line quantity ≥ 1; at least one quantity > 0 where the action needs it |
| PU-4 | Every dialog validates with linked errors | Required fields starred with "Bắt buộc"; `aria-invalid` + `aria-describedby`; no form is submitted with a known error |
| PU-5 | Refusals are explained | The server's reason (or the first validation message, or the translated fallback **"Something went wrong. Please try again."** — never the raw key `common.error`, R79) appears in the dialog or as a toast |
| PU-6 | Lists are usable | Empty states with the primary action; filter with no result → "Không có kết quả" + Xóa lọc; row buttons carry the row's number in their accessible name |
| PU-7 | The comparison does not rely on colour alone | Lowest price is a badge with the figure, expired quotations are labelled "expired", verdicts are badges with text |
| PU-8 | Accessible | Dialogs trap focus, scroll is locked, every input has a label, axe finds no violation |

## 6. Use cases
**UC-1 Buy by quotation:** New RFQ (lines, suppliers) → Send (confirm) → Enter quotation per supplier → compare → Award (confirm) → the draft order opens → submit and approve it. *Alternative:* a supplier declines (Mark as declined); the RFQ is cancelled. *Exception:* no supplier invited → Send is refused with the reason; an expired quotation cannot be awarded.
**UC-2 Release an order:** Purchase orders → Approve (confirm) or Reject (reason); an order needing finance shows Finance approve.
**UC-3 Receive goods:** order detail → Receive goods → type accepted and rejected per line → Record receipt → confirm → stock increases by the accepted units. *Exception:* a quantity above the remaining is refused in the dialog.
**UC-4 Check a supplier invoice:** Record invoice (order, number, prefill billable) → Matched or OnHold → Approve / Approve with variance (reason) / Reject (reason). *Alternative:* Match again after a late receipt.
**UC-5 Return goods:** New return → quantities and reason → Ship (confirm) → Record credit note when the supplier issues it.
**UC-6 Tune the match:** Match settings → tolerances and auto-approve → Save ("Đã lưu.").

## 7. Data model (screen view)
Rfq(Number, Title, NeededBy, Status, Lines, Invitations(Supplier, State), Quotations(Supplier, QuoteNumber, ValidUntil, Currency, LeadTime, Lines(RfqLine, UnitPrice)), AwardedPurchaseOrderId) · PurchaseOrder(PoNumber, Supplier, Status, OrderDate, Total, Currency, Items(Product, Quantity, Received, Remaining, UnitPrice)) · SupplierInvoice(Number, Supplier, PurchaseOrder, Dates, Net, Tax, Total, Status, HoldReasons, MatchLines(Ordered, Received, Returned, PreviouslyInvoiced, Invoiced, OrderPrice, InvoicedPrice, PriceVariance %, Verdict)) · VendorReturn(Number, PurchaseOrder, Supplier, Status, Value, Lines, CreditNote, CreditAmount) · PurchasingSettings(QuantityTolerance %, PriceTolerance %, AutoApproveMatched). Every row carries the tenant.

## 8. Integrations
PurchaseOrderService publishes `PurchaseOrderCreated`, `PurchaseOrderApproved`, `ReceiptScheduled`, `SupplierInvoiceApproved` (→ Accounting books the bill), `VendorReturnShipped` (→ Warehouse issues the stock, movement `ReturnedToVendor`), `VendorReturnCredited` (→ Accounting debit note); goods receipt reaches Warehouse (accepted units into stock). Replenishment proposals from Warehouse alerts are a separate screen (Purchase requests).

## 9. Reports
Lists with status filters; the comparison table of an RFQ; the match table of an invoice. No export.

## 10. Assumptions
Prices are typed per line; the supplier list offered to an RFQ is the tenant's **active** suppliers; money is shown with the order's currency, formatted in `en-US` whatever the interface language (U6); dates in the list use `dd MMM yyyy`.

## 11. Open issues (also in DEFERRED-ISSUES §6)
- **R79 — closed here:** `common.error` now exists in English and Vietnamese and a test covers the fallback.
- **R93:** a goods receipt cannot be undone and there is no "receipt history" screen; a wrong receipt is corrected by an adjustment in Warehouse. The confirmation says it cannot be undone.
- **R94:** purchase order money and dates ignore the interface language (`en-US` currency, `dd MMM yyyy`); the RFQ comparison shows the quote number even when it is empty (a leading "·"). Part of the U6 formatting task.
- The walk could not exercise Award → purchase order → Receive / Approve on real orders: the tenant `glcheck` has no suppliers and no submitted order (see §12); these steps are covered by tests and the confirmation dialogs were opened and dismissed where data existed.

## 12. UAT scenarios
1. RFQs → **New RFQ** → Save empty → "Bắt buộc" on Title and a message about the lines; no request sent.
2. Fill a title, a product code and quantity, tick a supplier → Save → the RFQ opens as **Draft**.
3. **Send to suppliers** → text says the lines can no longer be edited → Esc → still Draft → confirm → **Sent**; with no supplier ticked the reason "Invite at least one supplier…" appears.
4. **Enter quotation** → Save with an empty price → refused; type prices → saved → the comparison shows the lowest price badge.
5. **Award** → text names the supplier and says a draft purchase order is created → Esc → nothing awarded; confirm → the order opens.
6. Purchase orders → a SUBMITTED order → **Approve** → text → Cancel → still SUBMITTED. **Reject** → empty reason refused → with a reason the order leaves the list of submitted.
7. Order detail → **Receive goods** → accepted 12 on a line with 10 remaining → warning, button disabled; 8 accepted + 2 rejected → **Record receipt** → "Add 8 accepted unit(s) to stock (2 rejected)? … cannot be undone" → Cancel → nothing posted.
8. Supplier invoices → **Record invoice** → Save empty → linked errors; choose an order → **Prefill billable** → totals update; record → Matched or OnHold.
9. On a Matched invoice **Approve for payment** → "The bill is booked in Accounting…" → Cancel; **Reject** is disabled until a reason is typed; on an OnHold invoice **Approve with variance** needs a reason and the variance permission.
10. Returns to vendor → **Ship** → "The goods leave the warehouse stock now…" → Cancel → still Draft; **Cancel** (return) asks too; **Record credit note** needs a number and an amount > 0.
11. Match settings → price tolerance 150 → "Enter a number from 0 to 100." and nothing is saved; 3 → "Đã lưu.".
