# Accounting — Business Analysis (general ledger, automatic posting, closing, reports)

| | |
|---|---|
| **Domain** | Accounting (AccountingService, port 5005, code ACC) |
| **Covers** | ROADMAP P1-1 … P1-1e (automatic posting), P1-2 (chart of accounts, void / reverse, closing), P1-2b (ledger, trial balance, cash flow), P1-2c (B01 / B02 statements, CSV) |
| **Status** | Implemented and verified on the local Docker stack, 2026-10-24. Items still open are in [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md). |
| **Audience** | Product owner, chief accountant, QA, developers |
| **Companion documents** | [AutoPosting.md](AutoPosting.md) (posting rules in detail), [ChartOfAccounts.md](ChartOfAccounts.md), [Reports.md](Reports.md), [event-catalog](../../architecture/event-catalog.md) |

> **How this document is kept up to date.** The owner's rule (2026-10-24): after every task the matching business document is updated like a real business analyst would — actors, process,
> requirements with acceptance criteria, business rules, use cases, data and states, integrations, permissions, reports, assumptions, open issues, UAT scenarios. Section 16 lists what changed per task.

---

## 1. Business context and objectives

### 1.1 Problem
Before this work the platform created invoices, bills, payments, stock movements, POS sales and pay slips, but **none of them reached the general ledger by itself**: an accountant had to key every
journal entry by hand, the seeded chart of accounts was misread by the reports, and there was no way to finish a month (no closing, no cash-flow statement, no ledger, no statutory statements).
A Vietnamese company must keep its books under the Vietnamese accounting standards and file a balance sheet (B01-DN) and an income statement (B02-DN).

### 1.2 Objectives
| # | Objective | How we know it is met |
|---|---|---|
| O1 | Every business document that moves money or stock writes its own balanced journal entry, exactly once. | Acceptance criteria of FR-01 … FR-12; replaying the same event creates no second entry. |
| O2 | A tenant can run on a Vietnamese chart of accounts, and keep its own. | FR-20 … FR-24. |
| O3 | Mistakes can be corrected without losing the audit trail. | FR-30 … FR-32 (void, reverse). |
| O4 | A month can be closed in a controlled way and the books reconciled. | FR-33 … FR-36. |
| O5 | The accountant gets the standard books and statements. | FR-40 … FR-47. |
| O6 | A problem in the posting set-up never blocks the business document (sale, bill, payment). | BR-12. |

### 1.3 Scope
**In scope:** posting from sales, payment, purchasing, warehouse, POS, payroll, refunds and returns; chart of accounts and templates; manual entries, void, reverse; fiscal periods and closing;
trial balance, ledger, profit and loss, balance sheet, cash flow, B01 / B02 by indicator code; CSV export; admin screens for all of it.
**Out of scope (see section 15):** VAT, e-invoice, foreign currency, bank reconciliation, real payroll calculation (insurance, personal income tax), restocking of returned goods, B03 / B09, Excel / PDF.

---

## 2. Stakeholders and actors

| Actor | Who | What they do with Accounting |
|---|---|---|
| Accountant | Staff with `accounting:*:manage` | Maintains accounts, posts manual entries, voids / reverses, closes and reopens periods, reviews the posting profile. |
| Chief accountant | Same permissions, accountable for the books | Reviews the Vietnamese chart template and statement layouts, signs off the period close. |
| Finance viewer / auditor | `accounting:*:view`, `accounting:reports:view` | Reads reports and the ledger; exports CSV. |
| Tenant administrator | Tenant owner | Decides the chart template for the tenant, grants permissions. |
| Sales / POS cashier | Other modules | Their documents (orders, POS sales, voids, shift close) create entries; they never see the ledger. |
| Warehouse keeper, purchasing officer, HR officer, payment operator | Other modules | Receive goods, approve purchase orders, generate / pay pay slips, refund payments — each action creates entries. |
| System (event consumers) | AccountingService | Receives integration events and posts them (see section 9). |

---

## 3. Glossary

| Term | Meaning |
|---|---|
| Journal entry (bút toán) | A set of lines (account, debit or credit amount) whose debits equal its credits. Has a number `JE-<year>-<000001>`, a date, a fiscal period and a status. |
| Posting key | A business meaning of a line ("receivables", "sales revenue", "cost of goods sold" …). A business flow posts keys; the tenant's **posting profile** says which account each key is. |
| Posting profile | The per-tenant mapping posting key → account. |
| Chart template | A ready-made chart plus its posting map: `Simple14` (the platform's 14 accounts) or `Vas99` (Vietnamese chart, Circular 99/2025/TT-BTC). |
| Parent / leaf account | A parent groups sub-accounts and takes no postings; only leaves are postable. |
| Contra account | An account whose normal side is opposite to its group (accumulated depreciation, provisions, sales deductions). |
| Fiscal period | A calendar month in which entries are booked; open or closed. |
| Closing entry (bút toán kết chuyển) | The entry written when a period is closed: revenue and expenses → profit determination (VAS 911) → retained earnings (VAS 421). |
| Void | Takes a hand-written entry of an open period out of the books (status Voided). |
| Reverse | Posts a mirror entry; the original stays as Reversed. |
| GRNI | Goods received, not invoiced: the liability account credited by a stock receipt and debited by the supplier bill. |
| Cash-flow category | Operating, investing or financing — how a cash movement is classified by the account on the other side of the entry. |
| Indicator code (mã số) | The row number on the statutory forms B01-DN, B02-DN. |
| Draft layout | A statement layout the accountant has not yet reviewed against the official form. |

---

## 4. End-to-end business processes

### 4.1 Order to cash
1. A sales order is created → **invoice** is created for the order total → entry *Dr Receivables / Cr Sales revenue*.
2. Payment is captured → entry *Dr Cash (cash) or Bank (all other methods) / Cr Receivables*; the invoice's paid amount goes up; the invoice is Paid when covered.
3. Order is shipped → Warehouse issues the stock and publishes its cost → entry *Dr Cost of goods sold / Cr Inventory*.
4. Cancel before payment → invoice cancelled, revenue entry reversed. Cancel after payment → invoice cancelled, revenue reversed, the customer's money stays as a credit on receivables until the refund entry arrives.
5. Return completed → *Dr Sales returns / Cr Receivables* for the refund amount; the payment refund (when the gateway confirms) → *Dr Receivables / Cr Cash or Bank*; receivables of the order net to zero.

### 4.2 Procure to pay
1. Purchase order approved → **bill** created. Bill approved → *Dr GRNI / Cr Payables*. Bill paid → *Dr Payables / Cr Bank*.
2. Goods received (priced from the PO line, partial receipts supported) → *Dr Inventory / Cr GRNI*. GRNI nets to zero when receipts and bills match.

### 4.3 Retail (POS)
1. Sale completed → *Dr Cash / Bank per tender / Cr Sales revenue (subtotal − discount) and Cr VAT (tax)*. The stock leaves through the Warehouse movement (cost of goods sold, step 4.1.3).
2. Sale voided → mirror entry. Shift closed with counted ≠ expected cash → *Dr Cash over/short / Cr Cash* (shortage) or the reverse.

### 4.4 Payroll
Pay slip generated → *Dr Salary expense (gross + overtime) / Cr Salaries payable (net) and Cr Payroll withholdings payable (deductions)*, dated the end of the pay period (at most today).
Pay slip paid → *Dr Salaries payable / Cr Bank*. Unpaid slip cancelled → entry reversed, the month can be generated again.

### 4.5 Month end
Review posting problems (logs) → post/correct manual entries (void or reverse) → run the trial balance → close the period (checks, closing entry, lock) → produce statements → reopen only if a correction is needed.

---

## 5. Functional requirements

Priority: M = must, S = should. Source: ROADMAP item. Each requirement has acceptance criteria in section 12 (UAT).

### 5.1 Automatic posting
| ID | Requirement | Pri | Source |
|---|---|---|---|
| FR-01 | An invoice created for a sales order posts Dr Receivables / Cr Sales revenue at the gross amount. | M | P1-1 |
| FR-02 | A captured payment posts Dr Cash (method Cash) or Bank / Cr Receivables and is applied to the order's invoice; the invoice is Paid when covered. | M | P1-1 |
| FR-03 | Cancelling an order cancels its invoice and reverses the revenue entry, whether or not it was paid. | M | P1-1, P1-1e |
| FR-04 | Bill approval posts Dr GRNI / Cr Payables; bill payment posts Dr Payables / Cr Bank. | M | P1-1, P1-1b |
| FR-05 | A stock receipt posts Dr Inventory / Cr GRNI at quantity × unit cost; a PO receipt is valued at the PO line price; several partial receipts of one product on one PO are all booked. | M | P1-1b |
| FR-06 | A stock issue posts Dr Cost of goods sold / Cr Inventory at the cost Warehouse took the stock out. | M | P1-1b |
| FR-07 | A stock gain or loss (adjustment, stocktake, damage, POS void) posts against the *Inventory adjustment* key, at the current cost. Transfers and movements without a known cost post nothing. | M | P1-1b |
| FR-08 | A completed POS sale posts the money per tender and splits revenue (net of discount) from VAT. A voided sale reverses it. Sales already paid online (click-and-collect) are not posted twice. | M | P1-1c |
| FR-09 | A POS shift closed with a cash difference posts the difference to *Cash over/short*. | S | P1-1c |
| FR-10 | A generated pay slip, its payment and its cancellation post as in 4.4. A pay slip can be paid or cancelled only while pending. | M | P1-1d |
| FR-11 | A refund confirmed by the payment gateway posts Dr Receivables / Cr Cash or Bank and lowers the invoice's paid amount; a declined refund posts nothing. | M | P1-1e |
| FR-12 | A completed return posts Dr Sales returns / Cr Receivables for the refund amount (not above the order total). | M | P1-1e |
| FR-13 | Every automatic entry is posted **once** per source document, even if the event is delivered again. | M | P1-1 |
| FR-14 | The accountant can read and edit the posting profile (which account each key uses). | M | P1-1 |

### 5.2 Chart of accounts
| ID | Requirement | Pri | Source |
|---|---|---|---|
| FR-20 | Accounts form a tree: an account may have a parent of the same type; levels follow the parent. | M | P1-2 |
| FR-21 | A parent account takes no postings (manual, profile, automatic). | M | P1-2 |
| FR-22 | An account can be renamed, moved, deactivated and activated; deactivation is refused while the account is in the posting profile, has active sub-accounts or has a balance. | M | P1-2 |
| FR-23 | A tenant with no journal entries can apply a chart template (`Simple14` or `Vas99`); the template's accounts are added and the posting profile is set to it. | M | P1-2 |
| FR-24 | Accounts carry *cash account* and *cash-flow category* attributes, editable. | M | P1-2b |

### 5.3 Corrections and closing
| ID | Requirement | Pri | Source |
|---|---|---|---|
| FR-30 | Any posted entry can be reversed with a reason, at a chosen date; the original becomes Reversed. | M | P1-2 |
| FR-31 | A hand-written entry of an open period can be voided with a reason; it is then ignored by every report. | M | P1-2 |
| FR-32 | Entries that belong to a document and closing entries cannot be voided; closing entries cannot be reversed by hand. | M | P1-2 |
| FR-33 | Closing a period checks that earlier periods are closed, that there are no draft entries and that debits equal credits. | M | P1-2 |
| FR-34 | Closing writes one closing entry (revenue and expenses → profit determination → retained earnings) dated the period end and locks the period. | M | P1-2 |
| FR-35 | The latest closed period can be reopened with a reason; its closing entry is voided. | M | P1-2 |
| FR-36 | After a period is closed no entry can be posted into it; a document dated in a closed period is booked today. | M | P1-1 |

### 5.4 Reports
| ID | Requirement | Pri | Source |
|---|---|---|---|
| FR-40 | Trial balance with opening, period and closing columns, roll-up to parent accounts, totals and a balanced check. | M | P1-2b |
| FR-41 | Account ledger with opening balance, counter-accounts, running balance, closing balance; parent includes sub-accounts; paged. | M | P1-2b |
| FR-42 | Profit and loss for a range, unaffected by closing entries. | M | P1-2 |
| FR-43 | Balance sheet that balances before and after closing. | M | P1-2 |
| FR-44 | Cash-flow statement, direct method, by counter-account category, with opening / closing cash and a reconciliation check; cash ↔ bank transfers excluded. | M | P1-2b |
| FR-45 | B01-DN and B02-DN by indicator code, with an optional prior-period column, a list of accounts the layout does not take, and a draft label. | M | P1-2c |
| FR-46 | CSV export of the trial balance, ledger (all rows), cash flow, B01 and B02, safe for Excel and Vietnamese text. | S | P1-2c |
| FR-47 | The admin app has screens for the accounts tree, journal entries (reverse / void), fiscal periods (close / reopen) and every report. | M | P1-2 … P1-2c |

---

## 6. Business rules

### 6.1 Posting
| ID | Rule |
|---|---|
| BR-01 | Every entry has at least two lines; each line has exactly one positive side; total debits = total credits (amounts rounded to cents). |
| BR-02 | **Idempotency.** An automatic entry is identified by *source type + source id* (e.g. `Invoice:<id>`, `StockMovement:<id>`, `PaymentRefund:<refundId>`); posting the same source again returns the existing entry. A unique index per tenant enforces it. |
| BR-03 | **Entry date** = the document date. The fiscal period is the one containing the date; if none exists a calendar-month period is opened. |
| BR-04 | A document dated in a **closed** period is booked **today**, in the current open period, with a warning. If today's period is closed too, the posting fails (see BR-12). |
| BR-05 | Entry numbers are `JE-<year>-<6 digits>` per tenant from an atomic counter; a rolled-back posting leaves a gap. |
| BR-06 | A tenant with no accounts gets the chart of the configured default template (`Simple14`) when its first document is posted; a tenant with its own chart is never given extra accounts. A posting key with no account makes only the flows that use it fail. |
| BR-07 | A posting key must point at an **active leaf** account; receivables and payables cannot be the same account. |
| BR-08 | Revenue is booked at invoice creation, net of discount for POS; VAT is booked only where the source carries it (POS today). |
| BR-09 | The cost of goods is posted from the Warehouse movement only (also for POS sales) — never from the sales or POS document. |
| BR-10 | Cash vs bank: payment method *Cash* and POS tender "Cash" go to the Cash key; everything else to Bank. |
| BR-11 | Pay slip entries are dated the end of the pay period, at most today. |
| BR-12 | **A posting problem never blocks the business document.** The invoice, bill, payment, stock movement, sale or pay slip is saved; the missing entry is logged as an error with the reason. Nothing is back-posted automatically. |
| BR-13 | Events without the data needed to post (published before the amounts existed; refunds not confirmed by the gateway) are skipped, never guessed. |
| BR-14 | The switch `Accounting:AutoPosting:Enabled = false` stops documents from posting by themselves. |

### 6.2 Status of an entry
| Status | Counted by reports | How it is reached |
|---|---|---|
| Draft (0) | No | (not produced by the application today) |
| Posted (1) | Yes | Created and posted |
| Voided (2) | No | Void (hand-written entry, open period) or a reopened closing entry |
| Reversed (3) | Yes — its reversal entry offsets it | Reverse |

### 6.3 Corrections
| ID | Rule |
|---|---|
| BR-20 | Void needs a reason, a Posted entry, an open period, and an entry that no document owns (no source key). |
| BR-21 | Reverse needs a reason and a Posted entry; the mirror entry is dated the chosen date (default today; a date in a closed period falls back to today). Reversing twice is not possible. |
| BR-22 | Closing entries are neither voided nor reversed by hand; the period is reopened instead. |

### 6.4 Chart of accounts
| ID | Rule |
|---|---|
| BR-30 | Account numbers are unique per tenant. A sub-account has the same account type as its parent; no cycles. |
| BR-31 | The number, type and normal balance of an account never change. |
| BR-32 | A template can be applied only while the tenant has **no** journal entries. |
| BR-33 | Reports sign a balance on the natural side of the account type, so contra accounts reduce their group. |

### 6.5 Closing
| ID | Rule |
|---|---|
| BR-40 | A period closes only if every earlier period is closed, it has no draft entries and its debits equal its credits. |
| BR-41 | The closing entry clears every leaf revenue and expense account on its own side, books the net result to the *income summary* key and moves it to *retained earnings* (a profit as a credit, a loss as a debit). When both keys are one account (the 14-account chart) the transfer lines are omitted. A period with no revenue or expense activity gets no entry. |
| BR-42 | Reopening is allowed only for the latest closed period and voids its closing entry (the key is renamed so a new closing can be posted). |
| BR-43 | The profit and loss ignores closing entries; the balance sheet includes them and adds the result not yet closed to equity. |

### 6.6 Cash flow
| ID | Rule |
|---|---|
| BR-50 | Cash accounts are those flagged *cash*. Every other line of an entry that touches cash is attributed to its account's category: operating (default), investing, financing (equity accounts default to financing). |
| BR-51 | A counter-line contributes *credit − debit* (inflow positive, outflow negative). Entries whose lines are all cash accounts are not cash flows. Closing and voided entries never count. |
| BR-52 | Opening cash + net change must equal closing cash; a difference is reported, never hidden. |

### 6.7 Statements B01 / B02
| ID | Rule |
|---|---|
| BR-60 | A row is the sum of account balances by number prefix, or a signed sum of other rows. B01 splits 131 / 331 by the side of each account's balance. |
| BR-61 | Accounts with a balance that no row takes are listed as *unmapped*; the layout exists for the `Vas99` chart only. |
| BR-62 | B01: total assets (270) = total sources (440) in every column, else the statement says so. |
| BR-63 | The layouts are **drafts** until the accountant has reviewed them; the API, the screen and the CSV say so. |

---

## 7. Use cases

### UC-01 View and maintain the posting profile
*Actor:* accountant (`accounting:accounts:manage`). *Pre:* the tenant has been posted to at least once, or applied a template.
*Main flow:* open the posting profile → the list shows each key and its account → change the account of a key → save. *Alternative:* the key points at a parent account / inactive account / receivables = payables → refused with the reason.
*Post:* later automatic entries use the new account; existing entries are unchanged.

### UC-02 Apply a chart template
*Actor:* tenant administrator / accountant. *Pre:* the tenant has no journal entries.
*Main flow:* Chart of Accounts → "Apply chart template" → choose *Vietnamese chart* → read the review warning → apply. *Result:* the template's accounts are created with their parents, the posting profile points at the template's accounts.
*Exception:* the tenant already has entries → refused ("add accounts and re-map the posting profile instead").

### UC-03 Add or change an account
*Main flow:* "Add Account" → number, name, type, normal balance, optional parent, cash flag and cash-flow category → save. *Alternatives:* duplicate number → refused; parent of another type → refused.
*Edit:* rename or move under another parent (levels of sub-accounts follow); the number and type are read-only. *Deactivate:* see BR-31 and FR-22 for the refusals.

### UC-04 Post a manual entry
*Actor:* accountant (`accounting:journals:manage`). *Main flow:* New Journal Entry → pick the open fiscal period, date, description → at least two lines on postable accounts, balanced → post.
*Exceptions:* a line on a parent account, inactive or unknown account → refused; date outside the period → refused; period closed → refused.

### UC-05 Void or reverse an entry
*Void (hand-written, open period):* Journal entries → "Void" → reason → status Voided, reports ignore it.
*Reverse (any posted entry):* "Reverse" → reason and optional date → mirror entry posted; original Reversed. *Refused:* closing entries; entries of a document cannot be voided; a closed period cannot be voided (reverse instead).

### UC-06 Close a fiscal period
*Actor:* accountant (`accounting:periods:manage`). *Pre:* the previous periods are closed.
*Main flow:* Fiscal Periods → Close → the system checks (BR-40), writes the closing entry (BR-41), locks the period. *Exceptions:* an earlier open period, draft entries or an unbalanced trial balance → refused with the reason.
*Post:* no entry can be posted into the period; the profit and loss still shows its result; the balance sheet shows the result in retained earnings.

### UC-07 Reopen a fiscal period
*Main flow:* Reopen → reason → the closing entry is voided and the period is open. *Refused:* a later period is closed; the period is not closed. After corrections the period is closed again (a new closing entry).

### UC-08 Produce the books and statements
*Actor:* accountant / auditor. *Main flow:* Reports → choose the tab (trial balance, ledger, profit and loss, balance sheet, cash flow, B01, B02) → dates (and a prior period) → read → Export CSV.
*Alternatives:* a tenant on another chart opens B01 / B02 → every account is listed as unmapped with a note; no cash account flagged → the cash-flow tab says to flag cash and bank accounts.

### UC-09 Handle a posting problem
*Trigger:* a document was saved but its entry is missing (log: "No journal entry was posted for … : reason"). *Flow:* the accountant reads the reason (key without an account, inactive or parent account, no open period today), fixes the posting profile or opens the period, and books the missing entry by hand (UC-04). *Note:* there is no automatic back-posting (section 15).

---

## 8. Data requirements

### 8.1 Main entities
| Entity | Key attributes | Notes |
|---|---|---|
| Account | number (≤ 20, unique per tenant), name, type (Asset, Liability, Equity, Revenue, Expense), normal balance (Debit, Credit), parent, level, active, control, **IsCash**, **CashFlowCategory** | VAS class = first digit of the number (derived). |
| PostingAccountMapping | tenant, posting key, account | Unique per (tenant, key). |
| JournalEntry | number, date, fiscal period, status, description, total debit / credit, reference type and id, source key, posted / voided audit fields | Source key unique per tenant. |
| JournalLine | entry, account, debit, credit, description | One positive side. |
| FiscalPeriod | name, start, end, status (Open, Closed) | Months are auto-created. |
| Invoice | number, customer, order, amount, paid amount, status (Unpaid, Paid, Overdue, Cancelled) | `AmountPaid` goes down on refunds. |
| Bill | number, supplier, purchase order, amount, status | Header only (no lines). |

### 8.2 Posting keys and default accounts
`Cash, Bank, AccountsReceivable, AccountsPayable, SalesRevenue, Inventory, VatOutput, GoodsReceivedNotInvoiced, CostOfGoodsSold, InventoryAdjustment, CashOverShort, SalaryExpense, SalariesPayable,
PayrollWithholdingsPayable, SalesReturns, IncomeSummary, RetainedEarnings`. The defaults per template are listed in AutoPosting.md (14-account chart) and ChartOfAccounts.md (Vietnamese chart).

### 8.3 State models
- **Fiscal period:** Open → (close) → Closed → (reopen, latest only) → Open.
- **Journal entry:** Posted → Reversed (by reverse) / Voided (by void or reopen of a closing entry).
- **Invoice:** Unpaid → Paid (covered) → Unpaid again (refund lowers the paid amount); any → Cancelled (order cancelled).
- **Pay slip:** Pending → Paid | Cancelled (only from Pending).

---

## 9. Integrations

| Source | Event | Transport | What Accounting does |
|---|---|---|---|
| Sales | SalesOrderCreated | RabbitMQ `sales_order_events` | Creates the invoice and posts revenue |
| Sales | SalesOrderCancelled | RabbitMQ | Cancels the invoice, reverses revenue |
| Sales | SalesOrderReturned | RabbitMQ | Posts the return (credit note) |
| Payment | PaymentProcessed | Kafka `payment-topic` | Posts the receipt, applies it to the invoice |
| Payment | PaymentRefunded (with refund id) | Kafka `payment-topic` | Posts the refund, lowers the paid amount |
| Purchasing | PurchaseOrderApproved | Kafka `purchasing-topic` | Creates the bill |
| Warehouse | StockMovementRecorded (UnitCost, ValueDelta) | RabbitMQ `warehouse_exchange` | Posts receipt / issue / adjustment |
| POS | TransactionCreated, TransactionVoided, CashShiftClosed | Kafka `pos-topic` | Posts sale, void, cash difference |
| HRM | PaySlipGenerated, PaySlipPaid, PaySlipVoided | Kafka `hrm-topic` | Posts payroll |

Reliability: Kafka offsets are committed after handling (3 attempts, then the message is logged and skipped); the RabbitMQ consumers retry up to 5 times then drop with an error log. Events of one document share a Kafka key,
so they arrive in order. Events without a tenant are dropped.

---

## 10. Reports specification

| Report | Parameters | Columns / content | Check |
|---|---|---|---|
| Trial balance (`trial-balance/full`) | fiscal period or dates, hide zero, roll-up | Account; opening Dr / Cr; period Dr / Cr; closing Dr / Cr; totals | Debits = credits in all three pairs |
| Account ledger (`account-ledger`) | account, dates, include sub-accounts, page | Opening; date, entry, description, counter-accounts, Dr, Cr, running balance; closing | Running balance = trial balance |
| Profit and loss | dates | Revenue and expense accounts, net income | Excludes closing entries |
| Balance sheet | as-of date | Assets, liabilities, equity (+ unclosed result) | Assets = liabilities + equity |
| Cash flow (`cash-flow`) | dates | Operating / investing / financing lines by counter-account, net change, opening and closing cash | Opening + net = closing |
| B01-DN (`statements/balance-sheet`) | as-of date, optional prior date | Indicator code, name, amount, prior amount; unmapped; balanced flag | 270 = 440 |
| B02-DN (`statements/income-statement`) | dates, optional prior range | Same | 60 = 50 − 51 − 52 |
All reports accept `format=csv`.

---

## 11. Security, audit and non-functional requirements

| Area | Requirement |
|---|---|
| **Permissions** | `accounting:accounts:view|manage`, `accounting:journals:view|manage`, `accounting:periods:view|manage`, `accounting:reports:view`, `accounting:invoices:view|manage`, `accounting:bills:view|manage`, `accounting:tax:view|manage`, `accounting:bank:view|manage`. Cross-module actions: `hrm:payroll:manage` (generate / pay / cancel pay slips), `payments:refund`, `sales_order:approve` (complete a return). |
| **Tenant isolation** | Every row belongs to one tenant; consumers drop events without a tenant; the account queries are not cached (a tenant-less cache key once leaked another tenant's chart). |
| **Audit** | Entries keep created / posted / voided timestamps and reasons; reversals and voids keep both entries visible; closing entries are flagged. |
| **Consistency** | The document, its entry and its outbox rows commit together; idempotent consumers; no double posting. |
| **Performance** | The ledger and cash flow read journal lines by account and date through an index; the ledger pages in memory (fine for normal volumes — DEFERRED R2). |
| **Localisation** | Admin screens in English and Vietnamese; statutory rows in Vietnamese; CSV is UTF-8 with a byte-order mark. |
| **Currency** | VND only; documents in another currency are not converted. |

---

## 12. Acceptance criteria and UAT scenarios

Verified on the local Docker stack on 2026-10-24 unless marked *unit test only*.

| # | Given / When / Then | Requirement | Result |
|---|---|---|---|
| UAT-01 | Given a new tenant, when its first invoice of 1,000 is created, then the 14 default accounts, the posting profile and an open month exist and the entry is Dr 1100 / Cr 4000 1,000. | FR-01, BR-06 | Passed |
| UAT-02 | Given that invoice, when the same order event is delivered twice, then only one invoice and one entry exist. | FR-13 | Passed |
| UAT-03 | When a payment of 1,000 by card is captured, then Dr bank / Cr receivables 1,000 and the invoice is Paid. | FR-02 | Passed |
| UAT-04 | When an unpaid order is cancelled, then the invoice is Cancelled and a reversal entry exists. | FR-03 | Passed |
| UAT-05 | When a PO line 20 × 50 is received as 12 then 8, then both receipts are stocked at cost 50 and entries 600 and 400 (Dr inventory / Cr GRNI) exist. | FR-05 | Passed |
| UAT-06 | When 3 units are issued at cost 50, then Dr cost of goods sold / Cr inventory 150; a loss of 2 → 100; a gain of 4 → 200. | FR-06, FR-07 | Passed |
| UAT-07 | When the PO's bill (1,000) is approved, then Dr GRNI / Cr payables 1,000. | FR-04 | Passed |
| UAT-08 | When a POS sale of 2 × 100 with discount 20 and tax 20 is paid cash 100 + card 100, then Dr cash 100, Dr bank 100 / Cr revenue 180, Cr VAT 20; voiding it reverses the entry. | FR-08 | Passed |
| UAT-09 | When a shift with float 100 and one cash sale of 100 is closed with 195 counted, then Dr cash over/short / Cr cash 5. | FR-09 | Passed |
| UAT-10 | When a pay slip of gross 5,000 + overtime 100 − deductions 1,500 is generated, then Dr salary expense 5,100 / Cr salaries payable 3,600 and Cr withholdings 1,500, dated the end of the pay period. Paying it → Dr 3,600 / Cr bank. Cancelling an unpaid slip reverses it and lets the month be generated again. | FR-10 | Passed |
| UAT-11 | When 400 of a 1,000 payment is refunded and the gateway confirms, then Dr receivables / Cr bank 400 and the invoice's paid amount is 600; an over-refund is refused. | FR-11 | Passed |
| UAT-12 | When a return of 1,000 is completed and the remaining 600 is refunded, then receivables of the order net to 0. | FR-12 | Passed |
| UAT-13 | When the gateway declines a refund, then no refund event and no entry exist. | FR-11 | Unit test only |
| UAT-14 | Given a tenant with entries, when a template is applied, then it is refused; given a clean tenant, then 109 accounts and 17 posting keys are created. | FR-23 | Passed |
| UAT-15 | When a manual entry uses parent account 111, then it is refused; on its sub-account 1111 it is accepted. | FR-21 | Passed |
| UAT-16 | When a hand-written entry is voided, then it disappears from the reports; reversing another at 20 Oct posts the mirror entry on that date. | FR-30, FR-31 | Passed |
| UAT-17 | When October (revenue 1,900, expenses 600) is closed, then one closing entry moves 1,300 to retained earnings, the profit and loss still shows 1,300 and the balance sheet balances. | FR-34, FR-43 | Passed |
| UAT-18 | When the period is reopened, then the closing entry is Voided; closing again posts a new one. | FR-35 | Passed |
| UAT-19 | When a loan 2,000, equipment 1,500, rent 300, a customer payment 400 and a bank → cash transfer 200 are posted, then the cash-flow shows financing +2,000, investing −1,500, operating as posted, ignores the transfer and reconciles. | FR-44 | Passed |
| UAT-20 | When the trial balance is run with roll-up, then parent 111 equals its sub-accounts and the totals balance in all three column pairs. | FR-40 | Passed |
| UAT-21 | When B02 for October is run, then it equals the flat profit and loss; B01 at 31 Oct has 270 = 440; a prior date fills the prior column; the SYSTEM tenant shows its accounts as unmapped. | FR-45 | Passed |
| UAT-22 | When a report is exported with `format=csv`, then the file starts with the UTF-8 byte-order mark and the ledger export holds every row. | FR-46 | Passed |
| UAT-23 | When a document is dated in a closed period, then it is booked today. | FR-36, BR-04 | Unit test only |
| UAT-24 | When a posting key has no usable account, then the document is saved and an error is logged. | BR-12 | Passed (closed period / inactive account scenarios) |

---

## 13. Roles and permissions matrix

| Action | accounts | journals | periods | reports | Other module permission |
|---|---|---|---|---|---|
| View accounts, tree, profile, templates | view | | | | |
| Create / edit / (de)activate account, apply template, edit profile | manage | | | | |
| View journal entries | | view | | | |
| Post manual entry, void, reverse | | manage | | | |
| View periods | | | view | | |
| Close / reopen period | | | manage | | |
| Any report and CSV export | | | | view | |
| Pay / cancel a pay slip | | | | | `hrm:payroll:manage` |
| Refund a payment | | | | | `payments:refund` |
| Complete a return | | | | | `sales_order:approve` |

---

## 14. Assumptions and constraints

1. One currency (VND) and one legal entity per tenant.
2. The Vietnamese chart and the statement layouts are **suggested drafts** based on Circular 200's numbering (largely kept by Circular 99/2025/TT-BTC); the accountant reviews them before real use.
3. The SYSTEM tenant and the demo data stay on the 14-account chart.
4. Months are the fiscal periods; closing is monthly.
5. Warehouse owns costing; Accounting never computes a cost.
6. A tenant that already has journal entries cannot change its chart template.

---

## 15. Out of scope and open issues

Full list with owners and impact: [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md). The business-relevant ones:
VAT split and e-invoice (P1-3); foreign currency and bank reconciliation (P1-4); restocking of returned goods and reversing their cost, and triggering the refund from a return (P1-5); real payroll with insurance, personal income tax and employer
contributions (P1-7); B03 by indicator code, indirect cash flow, B09 notes, off-balance accounts, year-end closing, maturity split, Excel / PDF (P1-2d); no automatic back-posting of documents that were saved without an entry; accountant review
of the Vietnamese chart and statement layouts.

---

## 16. Traceability and change log

| Task | Date | Requirements | Technical documents | Main tests |
|---|---|---|---|---|
| P1-1 automatic posting (phase 1) | 2026-10-23 | FR-01 … 04, 13, 14, 36; BR-01 … 08, 12, 14 | AutoPosting.md | `AutoPostingFlowTests`, `JournalPostingServiceTests` |
| P1-1b stock and cost of goods | 2026-10-24 | FR-05 … 07; BR-09 | AutoPosting.md | `StockMovementPostingTests`, Warehouse `StockMovementCostTests` |
| P1-1c POS | 2026-10-24 | FR-08, 09; BR-10 | AutoPosting.md | `PosPostingTests`, POS `PosLedgerContractTests` |
| P1-1d payroll | 2026-10-24 | FR-10; BR-11 | AutoPosting.md | `PayrollPostingTests`, HRM `PaySlipLifecycleTests` |
| P1-1e refunds and returns | 2026-10-24 | FR-11, 12; BR-13 | AutoPosting.md | `RefundPostingTests`, Payment `RefundEventTests` |
| P1-2 chart, void / reverse, closing | 2026-10-24 | FR-20 … 23, 30 … 35; BR-20 … 22, 30 … 33, 40 … 43 | ChartOfAccounts.md | `AccountManagementTests`, `ChartTemplateTests`, `VoidAndReverseTests`, `PeriodClosingTests` |
| P1-2b ledger, trial balance, cash flow | 2026-10-24 | FR-24, 40, 41, 44; BR-50 … 52 | Reports.md | `LedgerAndTrialBalanceTests`, `CashFlowTests` |
| P1-2c B01 / B02, CSV | 2026-10-24 | FR-45, 46; BR-60 … 63 | Reports.md | `StatementTests`, `StatementHandlerAndCsvTests` |
| BA baseline (this document) | 2026-10-24 | All | — | — |
