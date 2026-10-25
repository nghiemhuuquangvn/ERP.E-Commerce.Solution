# POS — Business Analysis (retail sale, void, cash shift and the link to the ledger)

| | |
|---|---|
| **Domain** | Point of Sale (POSService, port 5011, code POS) |
| **Covers** | ROADMAP P1-1c (the money on POS events, shift figures, ordering of events). The wider module design (terminals, offline mode, loyalty) is in [module-18-ecommerce-pos.md](module-18-ecommerce-pos.md). |
| **Status** | Implemented and verified on the local Docker stack, 2026-10-24 (unit tests POS 10). Written afterwards, 2026-10-25, to the standard of [BA-Accounting.md](../Accounting/BA-Accounting.md). |
| **Audience** | Product owner, store manager, cashier trainer, chief accountant, QA, developers |
| **Companion documents** | [BA-Accounting.md](../Accounting/BA-Accounting.md) (FR-08, FR-09, UAT-08, 09), [BA-Warehouse.md](../Warehouse/BA-Warehouse.md) (the stock side of a sale), [AutoPosting.md](../Accounting/AutoPosting.md) |

---

## 1. Business context and objectives

### 1.1 Problem
A POS sale produced receipts and stock movements but **no money reached the books**: the events carried no amounts, the cash drawer was never reconciled, and the figures of a shift were wrong:
* a shift's `TotalSales` and `TransactionCount` ignored voids;
* the expected cash and the cash variance of a shift existed in the model but were never computed or sent;
* events used random Kafka keys, so a **void could overtake its sale** and be processed first.

### 1.2 Objectives
| # | Objective | How we know it is met |
|---|---|---|
| O1 | Every completed sale tells the ledger the subtotal, discount, tax and **each tender** (cash, card, …). | POS-FR-01, 02; UAT-POS-01 |
| O2 | A void undoes exactly what the sale did. | POS-FR-03; UAT-POS-02 |
| O3 | A shift closes with the cash it should hold, the cash counted and the difference. | POS-FR-04, 05; UAT-POS-03 |
| O4 | A void can never be processed before its sale. | POS-FR-06; UAT-POS-04 |

### 1.3 Scope
**In scope:** completing and voiding a sale, split payments, cash shifts (open, close, counting), the events sent to Accounting and Warehouse.
**Out of scope:** the cost of goods (Warehouse posts it from the stock movement); VAT filing (ROADMAP P1-3); offline sync after a shift is closed; POS pricing rules for tax on discounted amounts (known, see 15).

---

## 2. Stakeholders and actors

| Actor | Who | What they do |
|---|---|---|
| Cashier | `pos:transactions`, `pos:shifts:manage` | Opens a shift, rings up sales, completes or voids them, closes the shift with a count. |
| Store manager | `pos:terminals:manage`, `pos:reports:view`, `pos:shifts:view` | Manages terminals, reads shift summaries and the Z-report. |
| Chief accountant | Accounting roles | Reviews the entries a shift produces and the cash over / short account. |
| Warehouse (system) | WareHouseService | Takes stock out for a sale, puts it back for a void. |
| Accounting (system) | AccountingService | Consumes `pos-topic` and posts. |

---

## 3. Glossary

| Term | Meaning |
|---|---|
| Transaction | One sale (a cart): Open → Completed or Voided (or Suspended). |
| Tender | One payment part of a sale: a method and an amount (Cash, Card, …). A sale can have several (split payment). |
| Cash shift | The period one cashier works one terminal's drawer, between an opening float and a closing count. |
| Opening float | Cash in the drawer when the shift opens. |
| Expected cash | Opening float + cash tendered in the shift's completed sales. |
| Cash variance | Counted cash − expected cash (negative = shortage). |
| Z-report | The end-of-shift totals report. |

---

## 4. End-to-end business processes

### 4.1 Sell
1. The cashier opens a **shift** (terminal, opening float, optionally a denomination count that must equal the float).
2. A transaction is opened on the shift; items are scanned (price, tax rate); a discount can be applied (not above the subtotal).
3. The sale is **completed** with one or more tenders that add up to the total **exactly**; the method is recorded as "Mixed" when there are several.
4. POS publishes `TransactionCreated` with the subtotal, discount, tax, each tender, the shift id and (for click-and-collect) the external payment id; Warehouse takes the stock out; Accounting books *Dr Cash (cash tenders) and Dr Bank (all others) / Cr Revenue (subtotal − discount) and Cr VAT (tax)*.

### 4.2 Void
A **completed** sale can be voided with a reason: the status becomes Voided, the shift figures go down, stock is put back and `TransactionVoided` goes out. Accounting books the mirror entry. Voiding an **open** cart publishes nothing (no stock left, no money taken).

### 4.3 Close the shift
The cashier counts the drawer (optionally by denomination, which must equal the counted total) and closes: POS computes the expected cash from the shift's completed sales, the variance, and publishes `CashShiftClosed` with opening, counted, expected and variance. A shortage or an overage is booked to *cash over / short*.

---

## 5. Functional requirements

| ID | Requirement | Pri | Source |
|---|---|---|---|
| POS-FR-01 | `TransactionCreated` carries subtotal, discount, tax and the list of tenders (method, amount), the shift id and the external payment id. | M | P1-1c |
| POS-FR-02 | Tenders of a completed sale add up to the total exactly; a sale cannot complete empty, without a tender, or twice. | M | existing |
| POS-FR-03 | `TransactionVoided` carries the same amounts and tenders as the sale it undoes; only a completed sale publishes it. | M | P1-1c |
| POS-FR-04 | A shift's total sales and transaction count **drop when a sale is voided**. | M | P1-1c |
| POS-FR-05 | Closing a shift computes expected cash (opening + cash tenders of the shift's completed sales) and the variance, and publishes them. | M | P1-1c |
| POS-FR-06 | POS events are keyed by the transaction id (sale and void) or the shift id, so events of one document arrive in order. | M | P1-1c |
| POS-FR-07 | Opening / closing denomination counts must equal the opening float / the counted total. | S | existing |

### 5.1 Acceptance criteria
| ID | Acceptance criteria | UAT |
|---|---|---|
| POS-FR-01 | Given 2 × 100 with discount 20 and tax 20 paid cash 100 + card 100, then the event has subtotal 200, discount 20, tax 20, tenders Cash 100 and Card 100; Accounting books Dr cash 100, Dr bank 100 / Cr revenue 180, Cr VAT 20. | UAT-POS-01 |
| POS-FR-02 | Given tenders of 90 + 90 on a total of 200, then completion is refused ("Tender total … does not match"); an empty cart is refused. | unit test |
| POS-FR-03 | Given that sale voided, then `TransactionVoided` has the same amounts and the entry is reversed; given an open cart voided, then no event. | UAT-POS-02 |
| POS-FR-04 | Given a shift with two sales of 100 and one void, then total sales = 100 and count = 1. | UAT-POS-05 |
| POS-FR-05 | Given float 100, one cash sale of 100 and a counted 195, then expected 200, variance −5, and Accounting books Dr cash over/short / Cr cash 5. | UAT-POS-03 |
| POS-FR-06 | Given a sale and its void produced one after the other, then both carry the transaction id as the Kafka key and the consumer sees the sale first. | UAT-POS-04 |

---

## 6. Business rules

| ID | Rule |
|---|---|
| POS-BR-01 | **Total = subtotal − discount + tax.** A discount cannot exceed the subtotal. |
| POS-BR-02 | **Tenders must equal the total exactly**; no change is booked in the ledger (the cash received is the cash tender amount). |
| POS-BR-03 | **Cash vs bank:** the tender "Cash" is cash; every other method is bank (card, wallet, …). |
| POS-BR-04 | Only a **completed** sale can be voided with effect; an already voided sale cannot be voided again. |
| POS-BR-05 | A sale belongs to the shift it was rung up on; the shift's expected cash counts only its completed, not voided, sales. |
| POS-BR-06 | A shift closes once; the variance = counted − expected, and an exact count has variance 0 (nothing is booked). |
| POS-BR-07 | Revenue is posted net of discount; VAT is posted from the tax amount as sent (see 15 for the known pricing rule). |
| POS-BR-08 | The cost of goods is **never** posted from the POS event: Warehouse's stock movement does it (BA-Accounting BR-09). |
| POS-BR-09 | A sale already paid online (external payment id set, click-and-collect) is not posted twice by Accounting. |
| POS-BR-10 | Every event carries the tenant; consumers drop events without one. |

---

## 7. Use cases

### UC-POS-01 Open a shift
*Actor:* cashier (`pos:shifts:manage`). *Main flow:* choose terminal, enter the opening float (and optionally the denomination count) → shift Open. *Exception:* count total ≠ float → refused.

### UC-POS-02 Ring up and complete a sale
*Actor:* cashier (`pos:transactions`). *Main flow:* open transaction → add items → (discount) → complete with tenders → receipt → event published. *Alternatives:* split payment (several tenders); suspend and resume a cart. *Exceptions:* empty cart, tender total ≠ total, discount above subtotal → refused.

### UC-POS-03 Void a sale
*Actor:* cashier or manager (`pos:transactions`). *Main flow:* void with a reason → status Voided → stock put back → shift totals reduced → `TransactionVoided` → ledger reversal. *Alternative:* open cart → just closed, no event. *Exception:* already voided → refused.

### UC-POS-04 Close the shift
*Actor:* cashier. *Main flow:* count the drawer → close with counted cash (and optionally denominations) → expected cash and variance computed → `CashShiftClosed` → over / short booked if ≠ 0. *Exception:* shift not open → refused; denomination total ≠ counted → refused.

### UC-POS-05 Read the shift summary
*Actor:* manager (`pos:shifts:view`, `pos:reports:view`). *Main flow:* shift summary or Z-report → totals, count, opening, expected, counted, variance.

---

## 8. Data requirements

| Entity | Key attributes |
|---|---|
| Transaction | terminal, cashier, shift, subtotal, discount, tax, total, payment method, status (Open, Completed, Voided, Suspended), void reason, external payment id |
| TransactionItem | product, quantity, unit price, tax rate |
| TransactionTender | method, amount |
| CashShift | terminal, cashier, opening balance, closing balance, total sales, transaction count, status (Open, Closed), opened / closed at, notes, denomination lines |
| Terminal | name, active flag |

**State models:** Transaction Open → Completed → Voided; Open → Suspended → Open; Open → Voided. CashShift Open → Closed.

---

## 9. Integrations

| Direction | Event | Transport | Content / effect |
|---|---|---|---|
| POS → Accounting | `TransactionCreated`, `TransactionVoided`, `CashShiftClosed` | Kafka `pos-topic`, key = transaction / shift id | Amounts, tenders, shift id; opening, counted, expected, variance |
| POS → Warehouse | sale / void stock record | internal service call | `Sold` movement per line; gain at cost for a void |
| Payment → POS | PaymentProcessed | Kafka | confirms an external payment on a sale |

Kafka offsets are committed after handling (3 attempts then log and skip).

---

## 10. Reports

Z-report and shift summary (totals, count, cash reconciliation); the receipt; Accounting's ledger shows the resulting entries.

---

## 11. Security, audit and non-functional requirements

| Area | Requirement |
|---|---|
| Permissions | `pos:transactions`, `pos:shifts:manage`, `pos:shifts:view`, `pos:terminals:manage`, `pos:reports:view`. |
| Tenant isolation | Terminals, shifts and transactions are tenant-owned; events carry the tenant. |
| Audit | Void keeps the reason and time; the shift keeps the counted denominations. |
| Consistency | The sale, the shift totals and the outbox row commit together. |
| Ordering | Per-document Kafka keys keep a sale before its void. |

---

## 12. UAT scenarios

Verified on the local Docker stack on 2026-10-24 (SYSTEM tenant) unless marked unit test.

| # | Given / When / Then | Requirement | Result |
|---|---|---|---|
| UAT-POS-01 | A sale of 2 × 100, discount 20, tax 20, paid cash 100 + card 100 → *Dr 1000 100 + 100 / Cr 4000 180 / Cr 2200 20*. | POS-FR-01 | Passed |
| UAT-POS-02 | A voided cash sale → the entry is Reversed and a reversal entry exists. | POS-FR-03 | Passed |
| UAT-POS-03 | A shift with float 100, a cash sale 100, counted 195 → expected 200, variance −5 → *Dr 5100 / Cr 1000 5*. | POS-FR-05 | Passed |
| UAT-POS-04 | A sale then its void use the same key; the void is never processed first. | POS-FR-06 | Fixed by keying on the transaction / shift id; the ordering was not reproduced live |
| UAT-POS-05 | Shift totals after a void: 200 / 1 sale. | POS-FR-04 | Passed |
| UAT-POS-06 | Tender total ≠ total, empty cart, discount above subtotal are refused. | POS-FR-02 | Unit test |

---

## 13. Roles and permissions matrix

| Action | Permission |
|---|---|
| Ring up, complete, void, sync offline | `pos:transactions` |
| Open / close a shift | `pos:shifts:manage` |
| View a shift, its summary | `pos:shifts:view` |
| Manage terminals | `pos:terminals:manage` |
| Z-report | `pos:reports:view` |

---

## 14. Assumptions and constraints

1. One currency (VND); tenders are in the tenant's currency.
2. Cash is identified by the method name "Cash" (case-insensitive); other methods go to the bank account.
3. The drawer count at closing is trusted; a variance is booked without approval (a review step is a later improvement).
4. One open shift per terminal and cashier at a time.

---

## 15. Out of scope and open issues

* **Known, not fixed:** POS tax is computed without taking the discount into account (a POS pricing rule), so VAT can be slightly high on discounted sales; voids and offline syncs that arrive **after the shift is closed** are not in that shift's figures.
* Back-posting of sales made before P1-1c; VAT split by rate (P1-3); sales returns at the till (ROADMAP P1-5); approval of cash variance. See [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md).

---

## 16. Traceability and change log

| Task | Date | Requirements | Technical documents | Main tests |
|---|---|---|---|---|
| P1-1c POS sale to the ledger | 2026-10-24 | POS-FR-01 … 06; POS-BR-01 … 10 | AutoPosting.md, event-catalog | `PosLedgerContractTests` (POS), `PosPostingTests` (ACC) |
| BA baseline (this document) | 2026-10-25 | All | — | — |
