# Accounting — Business Analysis (general ledger, automatic posting, closing, reports)

| | |
|---|---|
| **Domain** | Accounting (AccountingService, port 5005, code ACC) |
| **Covers** | ROADMAP P1-1 … P1-1e (automatic posting), P1-2 (chart of accounts, void / reverse, closing), P1-2b (ledger, trial balance, cash flow), P1-2c (B01 / B02 statements, CSV), P1-2d (B03 cash-flow statement by indicator code, year-end closing policy, Excel export), P1-2e (amount unit VND / thousand / million and company header on the statements), P1-2f (PDF export of B01 / B02 / B03), P1-2g (B03 by the indirect method), P1-2h (short- / long-term split of trade receivables and payables on B01 by due date), **P1-2i (layouts for the 14-account chart, off-balance accounts, loan ledger, B09 notes)** |
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
| O7 | The accountant gets the third statutory statement, the cash-flow statement B03-DN, in the official row layout, and can trust that it agrees with the cash on the books. | FR-48 … FR-51; row 70 = closing cash. |
| O8 | A company that closes its books once a year (not every month) can do so without writing twelve closing entries, and carry its balances into the new year without manual work. | FR-53 … FR-57. |
| O9 | Reports can be handed to an auditor or a manager as a spreadsheet whose amounts are real numbers. | FR-52. |
| O10 | The statements look like the filed forms: in thousand or million VND where the company files that way, headed by the company's name, tax code and address, with the signatories below. | FR-58 … FR-62. |
| O11 | A statement can be printed or archived as a PDF that looks like the filed form, with the company header, the unit and the signatories, and agrees to the cent with the screen and the spreadsheets. | FR-63 … FR-66. |
| O12 | A company that files its cash-flow statement by the indirect method (starting from profit before tax) can produce it from the same ledger, and can see that it agrees with the direct method. | FR-67 … FR-70. |
| O13 | The balance sheet separates trade receivables and payables due within 12 months from those due later, using the real due dates of the open invoices and bills instead of one fixed account. | FR-71 … FR-74. |
| O14 | A tenant that has not applied the Vietnamese chart still gets B01 / B02 / B03; off-balance items are tracked; borrowings are split by their real schedule; and the notes to the financial statements can be prepared from the books and the accountant's texts. | FR-75 … FR-85. |

### 1.3 Scope
**In scope:** posting from sales, payment, purchasing, warehouse, POS, payroll, refunds and returns; chart of accounts and templates; manual entries, void, reverse; fiscal periods and closing;
trial balance, ledger, profit and loss, balance sheet, cash flow, B01 / B02 / **B03** by indicator code; CSV and **Excel** export; **a closing policy (monthly or at year end)**; admin screens for all of it.
**Out of scope (see section 15):** VAT, e-invoice, foreign currency, bank reconciliation, real payroll calculation (insurance, personal income tax), restocking of returned goods, an automatic posting link for loans (drawing, repayment, interest accrual),
layouts for a tenant's own chart (other than Vas99 and Simple14), PDF of reports other than B01 / B02 / B03 and the notes. Rounding and the header apply to B01 / B02 / B03 only; the trial balance and the ledger stay exact in VND.

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
| Fiscal period | A date range (normally a calendar month) in which entries are booked; open or closed. |
| Closing entry (bút toán kết chuyển) | The entry written when a period (Monthly policy) or the last period of the year (YearEnd policy) is closed: revenue and expenses → profit determination (VAS 911) → retained earnings (VAS 421). |
| Void | Takes a hand-written entry of an open period out of the books (status Voided). |
| Reverse | Posts a mirror entry; the original stays as Reversed. |
| GRNI | Goods received, not invoiced: the liability account credited by a stock receipt and debited by the supplier bill. |
| Cash-flow category | Operating, investing or financing — how a cash movement is classified by the account on the other side of the entry. |
| Indicator code (mã số) | The row number on the statutory forms B01-DN, B02-DN. |
| Draft layout | A statement layout the accountant has not yet reviewed against the official form. |
| B03-DN | The cash-flow statement of the Vietnamese forms: three activities (operating, investing, financing) with numbered rows 01 … 70. This platform produces it by the **direct method** (cash received and paid). |
| Direct / indirect method | Direct: list the cash actually received and paid. Indirect: start from profit and adjust for non-cash items. Only the direct method exists today. |
| Counter-account | For a cash line of an entry, the account on the other side (e.g. cash received for sales: the counter-account is revenue or receivables). It tells what the money was for. |
| Fiscal year | The twelve months ending in the *fiscal-year end month* of the closing policy (December by default). A period is the **last of the year** when it ends on the last day of that month. |
| Closing policy | A tenant setting: **Monthly** (every period writes a closing entry — the original behaviour) or **YearEnd** (a period only locks; the last period of the year writes one closing entry). |
| Report profile | A tenant setting holding the company block of the statements (name, tax code, address), the signatories (người lập biểu, kế toán trưởng, giám đốc) and the default amount unit. |
| Amount unit (đơn vị tính) | VND, thousand VND or million VND. A statement shows every amount divided by the unit and rounded; the exact books are unchanged. |
| PDF export | A print-ready A4 file of B01, B02 or B03 (`format=pdf`), built from the same data as the screen, CSV and Excel. Other reports (trial balance, ledger, flat cash flow) have no PDF. |
| Off-balance account (tài khoản ngoài bảng) | Class 0 accounts 001 – 008 (leased assets, goods held for others, goods on consignment, written-off debts, foreign currency, budget estimates). They have **one side only** and are tracked in the off-balance register, not in journal entries. |
| Off-balance register | The book of movements of the class 0 accounts: each entry is **In** (raises the balance) or **Out** (lowers it). |
| Loan ledger | The register of borrowings with their repayment schedule (due date, principal, interest) and the date each installment was repaid. |
| Installment | One repayment of a loan: principal due on a date; repaid or outstanding. |
| Notes (B09-DN, thuyết minh) | The notes to the financial statements: general information, accounting policies and detail tables behind the rows of B01 and B02. |
| Rounding difference | The gap of one or a few units that appears when the rounded rows of two totals that are equal in exact VND (e.g. assets and sources) do not add to the same number. Normal on filed statements; reported in a note. |
| Carry-forward (kết chuyển số dư) | Balance-sheet accounts keep their balance into the next year; revenue and expense accounts start the new year at zero because the closing entry cleared them. Balances here are cumulative, so no separate "opening entry" is written. |

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
Under the **YearEnd** policy (4.6) "close the period" only locks the month; the closing entry is written once, by the last period of the year.

### 4.6 Year end and the choice of closing policy
The tenant chooses **how often the profit is determined** (Fiscal Periods → *Closing policy*):

| | Monthly (default) | YearEnd |
|---|---|---|
| Closing January … November | Each writes its closing entry; the month's revenue and expenses go to retained earnings | Only checks and locks the month — **no entry**; revenue and expenses stay on their accounts |
| Closing the last period of the year (December when the year ends in December) | Writes December's entry like any other month | Writes **one** entry for everything not yet moved to retained earnings (the whole year) |
| Profit and loss / B02 for a month | Unchanged by closing | Unchanged by closing |
| Balance sheet / B01 before the year is closed | Result of the open months in row 421b | Result of the whole year to date in row 421b |
| Start of the next year | Revenue and expense accounts are at zero | Same — the year-end entry cleared them; balance-sheet accounts carry over |

Flow of a year under YearEnd: 1. the accountant sets the policy before the first period of the year is closed → 2. each month is closed in order (locks) → 3. the last month is closed → the closing entry is written, dated the last
day of the year → 4. statements for the year are produced (B01 shows the profit in retained earnings, B02 shows the year's result) → 5. the new year starts with revenue / expenses at zero and balance-sheet balances carried over.
The policy can be changed only **between** years (BR-75): a year closed half one way and half the other would be closed twice or not at all.

### 4.7 Producing the cash-flow statement (B03-DN)
Accountant opens Reports → B03 → picks the range (and optionally a prior range) → the system reads every posted entry of the range that touches a cash account, attributes each non-cash line to a numbered row by its account and
direction (BR-64 … 66, table in 6.8), adds opening cash (row 60) → row 70 must equal the cash on the books; if not, the statement says so and lists the movements it could not place → the accountant reviews the draft rows → exports to Excel / CSV.

### 4.8 Statements for a tenant on the 14-account chart
A tenant that never applied the Vietnamese chart keeps the platform's 14 accounts (1000 cash … 5200 salaries). The system looks at the accounts: **111 present → Vietnamese layouts; else 1000 and 4000 present → 14-account layouts; otherwise Vietnamese layouts** (the old behaviour: what cannot be placed is listed as unmapped). Everything else — rounding, header, prior column, PDF, Excel, CSV, direct and indirect cash flow — works the same. The 14-account layouts are a simplification (no VAT, income-tax, interest or depreciation accounts exist); a tenant who needs the full form applies the Vietnamese chart.

### 4.9 Off-balance items
An item that belongs to the company's notes but not to its books — an asset rented from a third party, goods received to keep or to sell for someone else, a bad debt already written off, foreign currency held — is recorded in the **off-balance register**: choose the account (001 – 008), the direction (In / Out), the amount and a reference. The register shows per account the opening balance, what came in, what went out and the closing balance; a negative closing balance is flagged. A wrong entry is voided with a reason (not while its fiscal period is closed). Off-balance accounts never appear in a journal entry and never change a trial balance, a closing or a statement; their balances are listed in the notes (section V.12).

### 4.10 Borrowings and their schedule
The accountant registers the loan (lender, principal, rate, start date) with its **repayment schedule**. The money itself is still booked by journal entries (Dr bank / Cr 341 on drawing, the reverse on repayment). When an installment is paid, the accountant marks it repaid with its date. B01 then splits borrowings by the schedule: installments due **within 12 months** of the balance-sheet date stay in "Vay ngắn hạn" (row 320), those due later move to "Vay dài hạn" (row 338). Because the repayment date is stored, the split of **any past date is exact**.

### 4.11 Preparing the notes (B09-DN)
At year end (or any period end) the accountant: 1. opens Reports → B09, 2. writes the texts once (general information, standards, the accounting policies, non-cash transactions, events after the period, other) in "Edit the texts" — they are kept for the next period, 3. picks the period and unit and reads the generated tables (cash, receivables, inventory, fixed assets with depreciation and net book value, payables, taxes, accruals, borrowings from the loan ledger, equity movement, off-balance items, revenue and expenses), 4. checks them against B01 / B02, 5. exports PDF / Excel / CSV for the filing pack with the company header and signatories of the report profile.

### 4.12 Borrowings posted from the loan ledger (P1-2j)
The accountant registers the loan with its schedule as before. If the loan is **new**, the ledger now writes the books: saving it posts the **drawdown** (Dr bank / Cr loan payable, dated the start date); each time an installment is marked repaid it posts the **repayment** (Dr loan payable for the principal, Dr interest expense for the scheduled interest / Cr bank, dated the day it was paid). Cancelling a loan that has no repayment reverses the drawdown. A loan that was **already booked by hand** is registered with "post drawdown" unticked: it stays out of the postings and its repayments stay manual (so nothing is booked twice); it can be posted later with *Post drawdown* as long as nothing was repaid. The *Loan ledger vs books* check on the Loans screen compares what the schedules say is owed with the credit balance of the loan-payable account and shows the difference.

### 4.13 Payment history (P1-2j)
Every payment and refund applied to an invoice or a bill is now kept as a **payment application** with its day (a refund is negative). The balance sheet of an **earlier date** uses them: an invoice paid after that date was still open on it, a bill approved after it was not yet a payable, a cancellation counts from its own day. Documents settled before the register existed carry **estimated** dates (taken from their last update): a split that relies on them says it is approximate.

### 4.14 A tenant's own chart on B01 / B02 (P1-2j)
A tenant whose chart is neither the Vietnamese nor the 14-account one sees its accounts under "unmapped". The accountant opens Reports → **Account mapping**, picks an unmapped account's number (or a prefix such as `62`), the statement row it belongs to and which balances count (net, debit only, credit only), and saves. From then on B01 / B02 count those accounts in that row and the unmapped list shrinks.

### 4.15 Notes in English, non-cash transactions and the prior period (P1-2j)
The accountant chooses the **language** of the notes (Tiếng Việt / English) on the B09 tab. The fixed wording (titles, columns, labels, the PDF labels and number format) and the accountant's own texts are kept **per language**; an English text is written once in English and is not shown in the Vietnamese notes. Section VII shows a table of **non-cash transactions** found in the journal (fixed assets bought on a liability; a liability moved into owners' equity) above the accountant's text. Choosing a **comparison period** adds a column with the prior closing balance (movement tables) or the prior amount (revenue and expenses).

### 4.16 A receiving account per loan (P1-2k)
When the accountant registers a loan (or posts the drawdown of an older one) he can choose the **bank or cash account** that receives the money. The drawdown debits that account and every repayment credits it. Without a choice the Bank key of the posting profile is used, as before.

### 4.17 Accruing loan interest (P1-2k)
At the end of a month (or any date) the accountant presses **Accrue interest** on the Loans screen with the date. For every posted active loan and every unpaid installment the system accrues the interest earned so far in the installment's period (Dr interest expense / Cr interest payable). When the installment is repaid the accountant types the **interest actually paid** (or leaves it empty for the scheduled amount): the repayment clears the accrued interest and books only the difference to expense — a credit when less was paid than accrued. The *Loan ledger vs books* card checks the accrued interest against the interest-payable account.

### 4.18 Own-chart mapping for the cash-flow statements and the notes; texts per year (P1-2k)
**Mapping.** The *Account mapping* screen now also offers B03 direct, B03 indirect and B09: pick a prefix, the row (for B09 the table: V.1 cash … VI.2 expenses) and save. A cash movement against a mapped counter-account counts in the chosen B03 row (in the direction of the row); an indirect row sums the balance change of the mapped accounts; a notes table lists them. **Texts per year.** The texts of the notes are written for a **fiscal year**; a report for a later year shows the latest earlier text until it is overwritten, and writing an empty text for a year makes that year follow the earlier text again.

### 4.19 Day count and the month-end accrual job (P1-2l)
When registering a loan the accountant chooses how its interest is counted: **by the schedule** (as before: the scheduled interest of each installment split by the days of its period), **actual/365** or **actual/360** (each day's outstanding principal × annual rate ÷ 365 or 360; the schedule's interest column may be 0). Repaying an installment of such a loan proposes the interest of the days up to the repayment day (shown as the placeholder of *Interest paid*); typing a figure overrides it. In *Fiscal periods → closing policy* the tenant can switch on **Accrue loan interest automatically at month end**: from the 1st of each month the system accrues the interest of all posted loans to the last day of the previous month, the same entry as the manual button (Dr 635 / Cr 335). A closed period is skipped, never back-filled into the current month. The Loans screen says whether the automatic accrual is on.

### 4.20 History of note texts; notes in Japanese, Chinese and French (P1-2l)
Every change of a note text is kept: who saved it, when, and the text (a clearing is a change too). The text editor of the B09 tab has a **History** link per section listing the changes of that section, language and year (newest first, 50 at most) with *Use this text* to put an old text back into the draft. The notes can be produced in **Japanese, Chinese and French** besides Vietnamese and English: the fixed wording, the section titles and hints and the accountant's texts (kept per language). The Japanese and Chinese notes have no PDF (no CJK font is embedded): Excel and CSV work; French exports PDF like Vietnamese and English. The translations are drafts for a translator or the accountant to review.

### 4.21 VAT on sales, purchases and returns (P1-3)
The tenant states its **VAT rate and price convention** in *Settings → Tax* (default rate, *prices include tax*). Every invoice created from an order (or by hand) is split into **net, VAT and gross**: with prices including tax the order total is the gross (1,100,000 at 10 % → net 1,000,000, VAT 100,000); with prices excluding tax the VAT is added on top (1,000,000 → gross 1,100,000). The invoice posts Dr Receivable gross / Cr Revenue net / Cr VAT output. A rate typed on the invoice overrides the default, and **Adjust VAT** re-splits the same gross at another rate and posts only the difference. Purchase bills are split the same way; when the accountant approves a bill the supplier's real rate can be typed, and the bill posts Dr Goods received not invoiced net / Dr **VAT input** / Cr Payable gross. A customer return takes the VAT inside the refund off the VAT output at the invoice's own rate.

### 4.22 VAT listing, return 01/GTGT and e-invoices (P1-3)
*Reports → VAT* shows the **sales listing** (invoices, plus returns, adjustments, POS sales and manual entries from the journal) and the **purchase listing** (approved bills) with subtotals per rate and a check against the VAT account, and a simplified **return 01/GTGT** (carried-over input VAT, input VAT of the period, output VAT by rate, VAT payable or to carry forward) with CSV, Excel and PDF. On the Invoices screen *E-invoice* issues the electronic invoice through the tenant's provider (the buyer's name, tax code and address are typed), shows its series, number and lookup code, and cancels it with a reason. The provider and series are chosen in *Settings → Tax*; this installation offers a **sandbox provider** (no tax-authority connection) until a real connector (VNPT, Viettel, MISA, BKAV) is added. While an invoice has an issued e-invoice its VAT cannot be adjusted.

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
| FR-48 | **B03-DN** (cash flow, direct method) by indicator code: rows 01 … 07, 20, 21 … 27, 30, 31 … 36, 40, 50, 60, 61, 70, with an optional prior-period column. | M | P1-2d |
| FR-49 | Each cash movement is placed on a B03 row by the counter-account's number and by direction (received / paid); outflows are negative; an operating movement no row names goes to "other receipts" (06) or "other payments" (07). | M | P1-2d |
| FR-50 | Closing cash (row 70) is checked against the cash on the books in every column; a movement that no row takes (investing / financing accounts outside the layout) is listed and makes the check fail visibly. | M | P1-2d |
| FR-51 | B03 shows a draft label and notes (outflows negative, no foreign currency yet) and exports like the other statements. | M | P1-2d |
| FR-52 | Every report route can export **Excel (.xlsx)** (`format=xlsx`) as well as CSV: amounts are numeric cells, text is never evaluated as a formula, the header row is bold and frozen, the ledger export holds every row. | S | P1-2d |
| FR-53 | The accountant can read and set the tenant's **closing policy** (Monthly or YearEnd) and the month the fiscal year ends. | M | P1-2d |
| FR-54 | Under YearEnd, closing a period that is not the last of the year checks and locks it and writes no entry. | M | P1-2d |
| FR-55 | Under YearEnd, closing the last period of the year writes one closing entry for everything not yet closed, dated the period end. | M | P1-2d |
| FR-56 | Balance-sheet accounts carry into the new year and revenue / expense accounts start it at zero, without a separate opening entry. | M | P1-2d |
| FR-57 | The admin app shows the closing policy on the Fiscal Periods page, the B03 tab and "Export Excel" next to "Export CSV" on every report. | M | P1-2d |
| FR-58 | The accountant can read and set the tenant's **report profile**: company name, tax code, address, the three signatories and the default amount unit. | M | P1-2e |
| FR-59 | B01, B02 and B03 can be shown in VND, thousand VND or million VND: the tenant's default, overridable per request (`unit`). | M | P1-2e |
| FR-60 | Rounding is per statement row; total rows add the rounded rows; the integrity checks (270 = 440, 70 = closing cash) are made on the exact amounts and a rounding difference is reported, not hidden. | M | P1-2e |
| FR-61 | The statements and their CSV / Excel exports carry the company block above the title and the signatories below, and say the unit. | M | P1-2e |
| FR-62 | The admin app has a *Unit* selector on the B01 / B02 / B03 tabs and a *Report profile* panel (editable only with `accounting:accounts:manage`). | M | P1-2e |
| FR-63 | B01, B02 and B03 can be exported as **PDF** (`format=pdf`), A4 portrait: company block, centred title, period and unit line, indicator table, notes, signatories, page numbers. | M | P1-2f |
| FR-64 | The PDF shows exactly the figures of the screen / CSV / Excel for the same request (same unit, rounding, prior column, unmapped list, notes, draft label). | M | P1-2f |
| FR-65 | Vietnamese text (diacritics) prints correctly on any server: the font is embedded in the file, nothing depends on fonts installed on the machine. | M | P1-2f |
| FR-66 | Every other report answers `format=pdf` with a clear refusal (HTTP 400) instead of silently returning JSON; the admin shows **Export PDF** only on the B01 / B02 / B03 tabs. | M | P1-2f |
| FR-67 | B03-DN can be produced by the **indirect method** (`method=indirect`): operating cash flow starts from profit before tax, adds back non-cash items, follows working capital and subtracts interest and income tax paid. | M | P1-2g |
| FR-68 | Investing and financing rows (21 … 36), opening cash (60) and closing cash (70) are the same as in the direct method. | M | P1-2g |
| FR-69 | The indirect operating total (20) is checked against the direct method; a difference is reported with its amount and the accounts that explain it, never plugged. | M | P1-2g |
| FR-70 | The indirect statement supports the same prior column, unit and rounding, header, CSV / Excel / PDF and admin screen as the direct one; an unknown method is refused. | M | P1-2g |
| FR-71 | B01 shows trade receivables in two parts: due within 12 months (row 131, in "A. Short-term assets") and due later (row 211, in the new group 210 "I. Long-term receivables" that is part of "B. Long-term assets"). | M | P1-2h |
| FR-72 | B01 shows trade payables in two parts: due within 12 months (row 311) and due later (row 331, in "II. Long-term liabilities"). | M | P1-2h |
| FR-73 | The split uses the due date of the open invoices (receivables) and approved unpaid bills (payables) at the balance-sheet date; it never exceeds what the ledger holds and never changes a total (130, 200, 270, 310, 330, 300, 440). | M | P1-2h |
| FR-74 | When the balance-sheet date is in the past, the statement says the split is approximate; the prior column is split by its own date; unit, rounding, header and the CSV / Excel / PDF exports work as before. | M | P1-2h |
| FR-75 | B01, B02 and B03 (direct and indirect) work for a tenant on the platform's **14-account chart**: the layout is chosen from the tenant's accounts, with the same rounding, header, prior column and exports. | M | P1-2i |
| FR-76 | A tenant with neither chart (its own accounts) gets the Vietnamese layouts with everything it cannot place listed as unmapped (unchanged behaviour). | M | P1-2i |
| FR-77 | The Vietnamese template contains the off-balance accounts 001, 002, 003, 004, 007, 008 (account type OffBalance). | M | P1-2i |
| FR-78 | An accountant can record, list and void movements (In / Out) of off-balance accounts in a register kept outside the journal; the register is locked by a closed fiscal period. | M | P1-2i |
| FR-79 | An off-balance account is refused in a manual journal entry, in the posting profile and in every automatic posting; B01 / B02 / B03, the trial balance and the closing are unaffected by the register. | M | P1-2i |
| FR-80 | A report shows, per off-balance account and range, the opening balance, In, Out and closing balance, flags a negative closing balance, and exports to CSV / Excel. | M | P1-2i |
| FR-81 | An accountant can register a loan with its repayment schedule, record the repayment of each installment with its date, and cancel a loan entered by mistake. | M | P1-2i |
| FR-82 | B01 splits borrowings (341) into short-term (row 320) and long-term (row 338, together with bonds 343) by the schedule, exact for any past date, capped by the ledger, with no total changed. | M | P1-2i |
| FR-83 | The notes B09-DN can be produced for a range: general information, period and unit, standards, accounting policies, detail tables behind B01 / B02 (V.1 … V.12, VI.1, VI.2), non-cash transactions, events after the period and other information. | M | P1-2i |
| FR-84 | The texts of the notes are written by the accountant per section, kept per tenant, and reused in every period; an empty section shows what to write there. | M | P1-2i |
| FR-85 | The notes export to PDF, Excel and CSV with the company header, the unit and the signatories; the admin app has a **Loans** page, an **Off-balance register** page and a **B09** tab (with the text editor). | M | P1-2i |
| FR-86 | Given a new loan of 2,000 starting 1 Nov 2026 with a complete posting profile, when it is saved, then a journal entry dated 1 Nov exists with Dr bank (1121 on the Vietnamese chart) 2,000 / Cr 341 2,000 and the loan shows "posted"; with an incomplete profile the loan is still saved, shows "by hand" and nothing is posted. | UAT-75 |
| FR-87 | Given that posted loan, when installment 1 (principal 1,000, interest 90) is marked repaid on 20 Nov, then an entry dated 20 Nov exists with Dr 341 1,000, Dr 635 90 / Cr bank 1,090; repaying the same installment again is refused and posts nothing twice; an installment with no interest has no interest line. | UAT-76 |
| FR-88 | Given a loan registered with "post drawdown" unticked, then no entry is created at registration and none at repayment; given an active loan with no repayment that is not posted, when *Post drawdown* is used, then the drawdown is posted once; a second attempt, a repaid or a cancelled loan are refused (400). | UAT-77, 78 |
| FR-89 | Given a posted loan with no repayment, when it is cancelled, then the drawdown entry is reversed; cancelling a hand-booked loan reverses nothing. | UAT-79 |
| FR-90 | Given loans and the books, when `loans-reconciliation?asOfDate=` is requested, then it returns the outstanding principal of every loan (cancelled loans and loans starting after the date left out), the credit balance of the loan-payable account (with its sub-accounts), the difference and a "reconciled" flag; csv and xlsx answer 200; it needs `accounting:reports:view`. | UAT-77 |
| FR-91 | Given a payment event, a refund event, a manual "mark paid" of an invoice or of a bill, then a payment application with that day (refund negative) is stored with the invoice / bill; a redelivered event adds nothing. | UAT-80 |
| FR-92 | Given an invoice of 300 due in 30 months that was paid on 20 Nov, then B01 as of 15 Nov shows 300 in row 211 and 1,200 in row 131, and as of 24 Nov, 30 Nov and 10 Dec shows 0 in row 211; a bill approved on 25 Nov is not a payable on 24 Nov; one paid on 5 Dec is still a payable on 30 Nov. | UAT-80, 81 |
| FR-93 | Given a split that uses a payment with an estimated date, or a document without any payment record, for a past date, then the statement carries the note "…some payments have no recorded date… approximate"; for a date today or later, or when every payment is exact, no such note appears. | UAT-82 |
| FR-94 | Given the migration, then every invoice with a paid amount and every paid bill has one estimated payment application, approved bills have `ApprovedOn` and cancelled invoices `CancelledOn`. | UAT-83 |
| FR-95 | Given a tenant with an account 9001 (asset) that no row of B01 takes, when the statement is run, then it is listed as unmapped and 270 ≠ 440; when 9001 is mapped to row 111 (cash), then it is counted there, the unmapped list is empty and 270 = 440; removing the mapping restores the first state. Same for B02. | UAT-84 |
| FR-96 | Given a mapping request that names an unknown statement or row, a total / computed row (for example 270), a prefix no account starts with, the same prefix twice, a prefix the standard layout already places, or an unknown side, then HTTP 400 with the reason and nothing is saved; viewing needs `accounting:accounts:view`, saving `accounting:accounts:manage`. | UAT-84, 85 |
| FR-97 | Given `lang=en`, then the notes have the same 28 sections with English titles, column heads and labels, an English title and period text, an English PDF (page footer, "Tax code", "Item", signatories, number format 1,300); `lang=vi` or no `lang` is Vietnamese; any other language is refused (400). | UAT-86, 90 |
| FR-98 | Given a text saved in English for "policy.cash", then the English notes show it and the Vietnamese notes show their own text (or the hint); the text list answers per language; saving needs `accounting:accounts:manage`. | UAT-87 |
| FR-99 | Given entries with no cash line that debit fixed assets against a liability (300) and debit a liability against capital (200), and a cash purchase and a closing entry, then section VII has a table "Fixed assets acquired on credit 300 / Debt converted into equity 200 / Total 500", the cash entry and the closing entry are not counted, and a note tells the accountant to confirm it; with no such entry section VII stays a text with its hint. | UAT-88 |
| FR-100 | Given a comparison period, then every movement table gets a last column "Prior closing" and the revenue / expense tables a column "Prior period" (found by row label, 0 when the row did not exist); one comparison date without the other, or a start after the end, is refused (400). | UAT-89 |
| FR-101 | The admin screens exist: Loans (posted state, post-drawdown option and button, reconciliation), Reports → Account mapping, and on the B09 tab the language and comparison selectors; they follow the manage permissions. | UAT-91 |
| FR-102 | Given a loan of 2,000 created with receiving account 1112, then the drawdown entry debits 1112 (not the Bank key 1121) and credits 341; an account that is not an asset (331), does not exist, is inactive, groups sub-accounts or is off-balance is refused (400); an older loan without repayment gets its account when *Post drawdown* is used. | UAT-92 |
| FR-103 | Given that loan (installment 1 due 1 Dec: principal 1,000, interest 90), when interest is accrued to 16 Nov, then 45 (15 of 30 days) is posted Dr 635 / Cr 335 dated 16 Nov; the same date again posts nothing; 22 Nov posts the 18 that completes 63; a hand-booked loan, a repaid installment and a cancelled loan accrue nothing; with an incomplete posting profile the interest stays un-accrued for the next run. | UAT-93 |
| FR-104 | Given 63 accrued, when installment 1 is repaid on 1 Dec with interest paid 100, then the entry is Dr 341 1,000, Dr 335 63, Dr 635 37 / Cr receiving account 1,100; with interest paid 80 and 90 accrued, Dr 335 90 / Cr 635 10 / Cr account 1,080; a negative interest paid is refused; the installment keeps the interest paid. | UAT-94 |
| FR-105 | Given interest already accrued on a loan, then cancelling it is refused (400) with the reason. | UAT-95 |
| FR-106 | Given posted loans with accruals, then the reconciliation also returns the accrued interest of the unpaid installments, the balance of the interest-payable account and their difference; on the 14-account chart (one account for both) the ledger side adds the interest. | UAT-96 |
| FR-107 | Given an investing counter-account 9701 that no row of B03 (direct) takes, then it is listed as unmapped; when 9701 is mapped to row 22, then its receipt counts in row 22 (30 = 100) and the unmapped list is empty; a payment against an own expense account mapped to row 02 counts there; mapping a total, formula or fallback row is refused. | UAT-97 |
| FR-108 | Given the indirect method, then a mapped prefix on a prefix-driven row joins the row and the accounts the form deals with, and the direct rows of the indirect statement use the mapped direct layout (20 still equals the direct 20). | UAT-97 |
| FR-109 | Given a prefix mapped to a notes table (for example 9801 to VI.2), then that table lists the account and its total includes it; table codes are V.1 … V.9, V.11, VI.1, VI.2; a prefix the standard sets already hold is refused. | UAT-98 |
| FR-110 | Given texts written for 2026 and 2027, then the notes of a 2026 period show the 2026 text, a January 2027 report shows the 2027 text (or the 2026 text until 2027 has one), a 2025 report shows the year-0 text or none, clearing 2027 makes January 2027 show 2026 again, and the editor marks inherited texts with their year. | UAT-99, 100 |
| FR-111 | The admin screens follow: Loans (receiving account, *Accrue interest*, accrued column, interest paid), Account mapping (B03 / B09, side hidden where it does not apply) and the B09 tab (year of the texts). | UAT-101 |
| UAT-102 | Loan 1,000,000 at 12 %, actual/360, 1 Nov – 1 Dec 2026; accrue to 16 Nov, again, then to 1 Dec. | FR-112, 113 | 5,000 / 0 / 5,000; entries JE-…019 and 020 Dr 635 / Cr 335 (live on glcheck) |
| UAT-103 | Same with actual/365, and a loan with rate 0 and a day count. | FR-112, 113 | 9,863.01 for the period (unit test); rate 0 → 400 "An interest rate above 0 is needed…" |
| UAT-104 | Two installments of 500,000; repay the first on its due date, accrue to the second due date. | FR-114 | first interest 10,000.00; second 5,166.67 (unit test) |
| UAT-105 | Repay the installment of UAT-102 on 1 Dec without interest. | FR-115 | Dr 341 1,000,000, Dr 335 10,000, Cr bank 1,010,000 (live) |
| UAT-106 | Loan 600,000 at 12 % actual/360 from 1 Sep; switch off; restart the service. | FR-116, 117 | no `LoanAccrual` entry |
| UAT-107 | Switch on; restart. | FR-116, 117 | one entry dated 30 Sep 2026: Dr 635 / Cr 335 5,800.00 (29 days) (live) |
| UAT-108 | The period of the date is closed; the job runs; a user runs the accrual for it. | FR-118 | job: nothing posted, `SkippedClosedPeriod`; user: accrued (unit test) |
| UAT-109 | Save a text for FY 2031: "Ban dau", the same again, "Ban sua", then empty. | FR-119 | 3 revisions (the repeat adds none): empty, "Ban sua", "Ban dau", all by the user; unknown key → 400 (live) |
| UAT-110 | Notes with lang fr, ja, zh and de. | FR-120 | 200 with the language's titles; de → 400 (live; titles in unit test) |
| UAT-111 | PDF fr, PDF ja, Excel ja. | FR-121 | 200 application/pdf; 400; 200 |
| UAT-112 | The admin build. | FR-122 | `tsc -b` clean; not clicked through in a browser in this task |
| UAT-113 | Tenant rate 10 %, prices included; create invoice 1,100,000. | FR-123, 124 | amount 1,100,000, net 1,000,000, VAT 100,000; JE Dr 131 1,100,000 / Cr 5111 1,000,000 / Cr 3331 100,000 (live) |
| UAT-114 | Prices excluded, order total 1,000,000; and rate 0. | FR-123 | gross 1,100,000 (unit test); rate 0: unchanged |
| UAT-115 | A rate of 8 on the command; a rate of 150. | FR-125 | VAT 80,000 on 1,080,000; 150 → 400 (unit test) |
| UAT-116 | Adjust the invoice of UAT-113 to 8 %. | FR-126 | JE Dr 3331 18,518.52 / Cr 5111 18,518.52; same rate → no entry (live / unit test) |
| UAT-117 | A return of 550,000 on a 10 % invoice. | FR-127 | Dr sales returns 500,000 / Dr VAT output 50,000 / Cr receivable 550,000 (unit test) |
| UAT-118 | Bill 2,200,000 at 10 %, approve. | FR-128, 129 | JE Dr 3388 2,000,000 / Dr 1331 200,000 / Cr 331 2,200,000 (live) |
| UAT-119 | Bill created at 10 % for 1,080,000, approved with rate 8. | FR-128 | VAT input 80,000, net 1,000,000 (unit test) |
| UAT-120 | VAT listing of October 2026, sales and purchases. | FR-130 | sales: invoice VAT 100,000 = ledger, reconciled; purchases: bill VAT 200,000 = ledger (live); a return and a POS sale appear as Entry rows (unit test); xlsx 200 |
| UAT-121 | VAT return of the same month; csv, xlsx, pdf. | FR-131 | 22 = 0, 24 = 200,000, 25 = 200,000, S10 1,000,000, T10 100,000, 35 = 100,000, 40 = 0, 43 = 100,000; three exports 200 (live); carried-over and payable cases in unit tests |
| UAT-122 | Tax code, series and provider set; issue the e-invoice with a buyer; issue again; no provider. | FR-132 | issued C26TAA-0000001 with a lookup code; second → 400 "already has e-invoice"; no provider / tax code / series → 400 (live / unit test) |
| UAT-123 | Adjust VAT while the e-invoice is issued; cancel it with a reason; adjust again. | FR-126, 133 | 400 "has an issued e-invoice"; cancelled; adjust allowed (live / unit test) |
| UAT-124 | Issue again after the cancellation; provider fails once, then succeeds. | FR-132, 133 | new number C26TAA-0000002; Failed with the error, then Issued on the same record (live / unit test) |
| UAT-125 | The admin build. | FR-134 | `tsc -b` clean; not clicked through in a browser in this task |
| FR-123 | Given the tenant rate 10 % and "prices include tax", an invoice of 1,100,000 has net 1,000,000, VAT 100,000 and gross 1,100,000; with "prices exclude tax" an order total of 1,000,000 gives gross 1,100,000; with rate 0 (the default) nothing changes. | UAT-113, 114 |
| FR-124 | The invoice posts Dr Receivable (gross) / Cr Sales revenue (net) / Cr VAT output (VAT); without VAT it posts the two lines as before. | UAT-113 |
| FR-125 | `CreateInvoiceCommand.TaxRatePercent` overrides the tenant rate for one invoice; a rate outside 0–100 is refused (400). | UAT-115 |
| FR-126 | `POST invoices/{id}/adjust-vat` re-splits the gross at another rate and posts the difference (8 % instead of 10 % on 1,100,000: VAT 81,481.48, entry Dr VAT output / Cr Revenue 18,518.52); the same rate posts nothing; a cancelled invoice, an invoice with an issued e-invoice, or an entry that cannot be posted is refused (400). | UAT-116, 123 |
| FR-127 | A completed return takes the VAT inside the refund (at the invoice's rate) off the VAT output: Dr Sales returns net / Dr VAT output / Cr Receivable gross; an invoice without VAT gives the two-line entry as before. | UAT-117 |
| FR-128 | A bill is split like an invoice (tenant rate, override on creation); `ApproveBillCommand.TaxRatePercent` re-splits it before posting; approval posts Dr Goods received not invoiced net / Dr VAT input / Cr Payable gross. Paying the bill pays the gross. | UAT-118, 119 |
| FR-129 | Posting key `VatInput` exists (VAS chart: 1331; 14-account chart: the same tax account as VAT output, 2200) and is added to every existing tenant's posting profile on its next posting. | UAT-118 |
| FR-130 | `GET reports/vat/listing?fromDate&toDate&direction=output|input` lists the documents of the period (invoices not cancelled by creation date; bills approved or paid by approval date) and the other movements on the VAT account(s), with subtotals per rate, the account movement and the difference; `format=csv|xlsx`; PDF answers 400. | UAT-120 |
| FR-131 | `GET reports/vat/return` returns the simplified 01/GTGT as a statement (codes 22–25, S/T by rate, 35, 40, 43) in the report unit with the company header, notes with the ledger reconciliation, and `format=csv|xlsx|pdf`. | UAT-121 |
| FR-132 | `POST invoices/{id}/e-invoice` issues the e-invoice through the tenant's provider; it is refused when no provider, company tax code or series is configured, when the invoice is cancelled, or when it already has an issued e-invoice; a provider error or exception is kept (status Failed) and a retry reuses the record. | UAT-122 |
| FR-133 | `POST invoices/{id}/e-invoice/cancel` (reason required) cancels an issued e-invoice at the provider; afterwards a new e-invoice can be issued (the number is not reused); `GET invoices/{id}/e-invoice` returns the latest. | UAT-123, 124 |
| FR-134 | Settings → Tax holds the provider and the series; `GET invoices/e-invoice-providers` lists the providers offered. The admin screens follow: VAT columns on invoices and bills, *Adjust VAT*, the e-invoice dialog, the VAT tab of Reports. | UAT-125 |
| FR-112 | A loan has a day count: schedule (default), actual/365 or actual/360. Values outside 0–2, or a day count on a loan with rate 0, are refused (400); it cannot be changed after interest was accrued or a repayment recorded. | UAT-102, 103 |
| FR-113 | Given a loan of 1,000,000 at 12 % on actual/360 from 1 Nov to 1 Dec (schedule interest 0), then accruing to 16 Nov posts 5,000.00, again 0, and to 1 Dec 5,000.00 more (Dr 635 / Cr 335); on actual/365 the full period is 9,863.01. | UAT-102, 103 |
| FR-114 | Given an earlier installment repaid, then the next period's interest is computed on the reduced principal (500,000 × 12 % × 31 ÷ 360 = 5,166.67). | UAT-104 |
| FR-115 | Repaying an installment of a loan counted by days without an interest figure uses the interest of the days up to the payment day (and stores it as the installment's interest); an explicit figure wins; a scheduled loan keeps the scheduled interest. The loan list returns `interestDueToday` per installment. | UAT-105 |
| FR-116 | The closing policy has an `autoAccrueLoanInterest` switch (default off; saving the policy without the field keeps it). | UAT-106, 107 |
| FR-117 | When the switch is on, a background job accrues, once a day per service start and from the 1st of the month, the interest of the tenant's posted loans to the last day of the previous month; with the switch off nothing is accrued; a second run posts nothing. | UAT-106, 107 |
| FR-118 | The job (command flag `SkipClosedPeriod`) posts nothing for a date whose fiscal period is closed and logs it; a user's accrual is never skipped. | UAT-108 |
| FR-119 | Each save of a note text that changes it (new text, changed text, cleared) writes a revision with the user and the time; an identical save writes none. `GET accounting/reports/notes/text/history?key&lang&fiscalYear` lists them (newest first, max 50; unknown key or year → 400). | UAT-109 |
| FR-120 | The notes (`lang`) and the texts accept vi, en, ja, zh, fr (a regional code such as zh-CN counts; anything else → 400). Every wording and every section title and hint exists in all five languages. | UAT-110 |
| FR-121 | The PDF of the notes in ja or zh answers 400 ("use format=xlsx or csv"); fr, vi and en export PDF; Excel and CSV work in every language. | UAT-111 |
| FR-122 | The admin screens follow: Loans (day count, auto-accrual status, interest-due placeholder), closing policy (switch), B09 tab (History, five languages, no PDF button for ja / zh). | UAT-112 |

### 5.5 Acceptance criteria of the P1-2d requirements
(Scenarios are in section 12; the *UAT* column points at them.)

| ID | Acceptance criteria | UAT |
|---|---|---|
| FR-48 | Given a tenant on the Vietnamese chart with cash movements in October, when B03 is run for October, then every row code listed in FR-48 is present in order, the total rows are the signed sums of their members (20 = 01 … 07, 30, 40, 50 = 20 + 30 + 40, 70 = 50 + 60 + 61), and with a prior range each row also has a prior amount. | UAT-25, 26 |
| FR-49 | Given receipts of 400 for sales, payments of 600 to suppliers, a loan of 2,000 and equipment of 1,500, then 01 = 400, 02 = −600, 33 = 2,000, 21 = −1,500; a payment on an account no row names is shown in 07; 341 received → 33 and 341 paid → 34. | UAT-27, 28 |
| FR-50 | Given a cash movement whose counter-account is an investing account the layout leaves out, then the movement appears under "unmapped", the closing-cash check is false and a note explains it. | UAT-29 |
| FR-51 | When B03 is opened, then it shows its title, the "draft" flag and the notes; a CSV / Excel export carries the same. | UAT-25, 31 |
| FR-52 | Given any report, when `format=xlsx` is requested, then the answer is an `.xlsx` file (content type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`), amounts are numeric cells, a description starting with `=`, `+`, `-` or `@` is stored as text, Vietnamese text is intact, the header row is frozen. | UAT-30, 31 |
| FR-53 | Given a tenant that never set a policy, then GET returns Monthly and December; PUT with an unknown mode or a month outside 1 … 12 is refused (HTTP 400). | UAT-32, 38 |
| FR-54 | Given YearEnd with December as year end, when January … November are closed in order, then each becomes Closed, no closing entry exists and revenue / expense balances are unchanged. | UAT-34 |
| FR-55 | Given the same tenant, when December is closed, then exactly one Posted entry of type PeriodClosing exists, dated 31 December, whose lines clear every revenue / expense account by its cumulative balance and book the result to retained earnings; reopening December voids it. | UAT-35, 37 |
| FR-56 | Given that year is closed, then the trial balance of January shows revenue and expense accounts with a zero opening balance, retained earnings with the year's result and the other accounts with their year-end balance; B01 is balanced before and after. | UAT-36 |
| FR-57 | Given a user with `accounting:periods:manage`, then the policy panel can be saved; a user with only `accounting:periods:view` sees it read-only. | manual |
| FR-58 | Given a tenant that never saved a profile, then GET returns empty texts and unit Vnd; after PUT the values are returned and kept per tenant; text longer than the limits (name 200, tax code 20, others 300) or a unit other than Vnd / Thousand / Million is refused (HTTP 400); a user without `accounting:accounts:manage` gets 403 on PUT. | UAT-40, 44 |
| FR-59 | Given a profile with unit Thousand, then B01 / B02 / B03 are in thousand VND and say so; `unit=vnd` returns exact VND; `unit=foo` is refused (400); with no profile and no `unit` the amounts are plain VND. | UAT-41, 42 |
| FR-60 | Given cash 1,400 and inventory 1,400 against equity 2,800 in thousands, then rows are 1 + 1 = 2 assets and 3 sources, `isBalanced` is still true and a note says "Rounding difference of −1 thousand VND …"; a sheet that is really unbalanced stays unbalanced; half amounts round away from zero (1,500 → 2; −1,500 → −2). | UAT-43 |
| FR-61 | Given a filled profile, then the first lines of the CSV / sheet are the company name, tax code and address, the period line names the unit, and the last two lines are the signatory titles and names; a profile with only a name prints only the name and no signature block. | UAT-46 | Given the profile of tenant `glcheck` (company, tax code, address, three names, unit Thousand), when B01 31 Oct (prior 30 Sep), B02 October and B03 October are requested with `format=pdf`, then each answers 200 `application/pdf` with a `.pdf` file name and bytes starting `%PDF-` (76 KB, 62 KB, 65 KB). | FR-63 | Passed |
| UAT-47 | When the three files are rendered to images and their text extracted, then the company block, titles, "Đơn vị tính: nghìn đồng", Vietnamese row names and the signature block are legible with all diacritics; B01 has 2 pages ("Trang 1/2"), B02 and B03 one; the file lists embedded Noto Sans Regular and Bold. | FR-63, 65 | Passed (visual check of rendered pages) |
| UAT-48 | Given B01 with a prior date, then the PDF has the column "Số kỳ trước"; amounts equal the CSV of the same request; negatives print in parentheses; unmapped and notes are listed. | FR-64 | Passed (unit tests + live columns) |
| UAT-49 | When `format=pdf` is requested on `trial-balance/full` and on the flat `cash-flow`, then HTTP 400 with the message of FR-66. | FR-66 | Passed |
| UAT-50 | Given a profile with no text, when B02 is exported as PDF, then there is no company block and no signature block, and the table is unchanged; with only a name, only the name is printed. | BR-85, BR-91 | Unit test only |
| UAT-51 | Given the sample ledger of BR-102 (unit test), when B03 is produced indirectly, then 01 = 170, 08 = 300, 20 = −90, 30 = −780, 40 = 2,000, 50 = 1,130, 60 = 5,000, 70 = 6,130, `isBalanced` is true, nothing is unmapped, and 20 / 50 / 70 equal the direct statement of the same ledger. | FR-67, 68 | Unit test |
| UAT-52 | Given tenant `glcheck` October 2026 (revenue 1,900 on credit, expenses 600, receivables up 1,500, loan 2,000, equipment 1,500), when `method=indirect` is requested, then 01 = 1,300, 09 = −1,500, 20 = −200, 21 = −1,500, 33 = 2,000, 50 = 300, 70 = 300 — equal to the direct statement; `isBalanced` true; no unmapped. | FR-67, 69 | Passed |
| UAT-53 | Given equipment 800 bought on credit, then indirect 20 = 800, direct 20 = 0, `isBalanced` false and a note names the difference; given a movement on account 356, then it is listed as unmapped. | FR-69 | Unit test |
| UAT-54 | When `method=indirect` is requested with `format=pdf` and `format=xlsx`, then each answers 200 with the right content type; `method=foo` answers 400 with the message of FR-70; omitting `method` returns the direct statement (layout "B03-DN"). | FR-70 | Passed |
| UAT-55 | Given a prior range, then each row has a prior amount from its own ledger; given unit Thousand, then 170 → 0, 5,000 → 5, 2,000 → 2 and the total 20 equals the sum of the rounded rows while `isBalanced` is decided on exact amounts. | FR-70, BR-102 | Unit test |
| UAT-56 | Given tenant `glcheck` (131 = 1,500 Dr) with an invoice of 900 due in 18 months, an invoice of 300 due in 3 months and a paid invoice of 500, when B01 is run as of today, then 131 = 600, 211 = 900, 210 = 900, 200 rises from 1,500 to 2,400, 270 = 440 = 3,300, `isBalanced` true, and the notes explain the rule. Without the documents the same date shows 131 = 1,500, 211 = 0. | FR-71, 73 | Passed |
| UAT-57 | Given 700 credited on 331 and an approved bill of 400 due in 14 months, a bill of 100 due in 3 months and a pending bill of 50, when B01 is run as of 30 Nov 2026, then 311 = 1,000, 331 = 400, 310 / 330 follow, and 270 = 440. | FR-72 | Passed (live; ledger 1,400 on 331 at that date) |
| UAT-58 | Unit tests of the rule: 12 months exactly → short-term, one day later → long-term; partly paid 1,000 − 400 → 600 open; Overdue counts, Paid and Cancelled do not; approved bill counts, pending and paid do not; created after the date → ignored; cap by the ledger (900 against 400 → 400); a credit balance on 131 splits nothing. | FR-73 | Unit test |
| UAT-59 | Totals: with a maturity of 300 receivable / 200 payable on a sheet of 1,000 receivables, 800 payables, 500 cash, 700 capital, then 130 and 310 fall, 210 and 330 rise by those amounts and 270, 300, 440 are unchanged. | FR-73, BR-106 | Unit test |
| UAT-60 | Given the date 30 Sep 2026 (before today) with a split applied, then the note "The date is in the past …" is present; for today it is absent; with a prior date each column uses its own split. | FR-74, BR-107 | Unit test; live (30 Sep: documents created today are ignored → nothing to split) |
| UAT-61 | Given `unit=thousand`, then each of 131, 211, 311, 331 is rounded on its own (700 → 1, 300 → 0, 600 → 1, 200 → 0) and `isBalanced` stays true; `format=pdf`, `xlsx` and `csv` answer 200. | FR-74 | Unit test; live 200 |
| UAT-62 | Given the SYSTEM tenant (14 accounts, demo ledger), when B01 for 31 Oct 2026 is requested, then the title is "… biểu đồ 14 tài khoản", rows 110, 131, 141, 311, 313, 319, 411, 421a, 421b carry the ledger, 270 = 440 = −701,711.10 (the demo data is a net loss), `isBalanced` true, nothing unmapped, and the flat balance sheet has the same total assets. | FR-75 | Passed (after fixing 421b, which had taken account 3100 twice) |
| UAT-63 | When B02, B03 direct and B03 indirect of the same tenant are requested, then B02 60 = 50 = −882,151.76; B03 70 = −143,312.40 in both methods; both balanced; PDF 200. | FR-75 | Passed |
| UAT-64 | Unit tests: detection (14 accounts → Simple14; Vietnamese → Vas99; none → Vas99; both → Vas99), every 14-account account is on B01 / B02, retained earnings counted once, maturity split on 1100 / 2000, direct = indirect on a mixed ledger. | FR-75, 76 | Unit test |
| UAT-65 | On `glcheck`: create account 001 (type 6), record In 1,000 on 10 Nov and Out 300 on 20 Nov; the November report is 0 / 1,000 / 300 / 700 and December opens at 700; the list shows both entries. | FR-77, 78, 80 | Passed |
| UAT-66 | A journal entry with account 001 → HTTP 400 "…off-balance account…"; B01 stays 270 = 440 = 3,300 and the trial balance stays balanced with no line for 001. | FR-79 | Passed |
| UAT-67 | Unit tests: amount ≤ 0, wrong account type, unknown direction refused; negative closing flagged; voiding needs a reason and works once; a closed period refuses record and void; CSV / Excel table. | FR-78, 80 | Unit test |
| UAT-68 | On `glcheck` (341 = 2,000): register a 2,000 loan 500 / 700 / 800; B01 of 30 Nov 2026 → 320 = 500, 338 = 1,500; of 31 Mar 2027 → 320 = 1,200, 338 = 800; 300 = 2,000 and 270 = 440 = 3,300 each time. | FR-81, 82 | Passed |
| UAT-69 | A schedule of 40 against a principal of 100 is refused ("must add up"); repaying installment 1 twice is refused ("already repaid"); cancelling after a repayment is refused. | FR-81 | Passed |
| UAT-70 | Unit tests: outstanding 0 before the start, 3,000 at the end of 2026, a repayment lowers it from its own date only, exactly 12 months is short-term, several loans capped by the ledger (5,000 / 2,500 / 0), a cancelled loan owes nothing. | FR-82 | Unit test |
| UAT-71 | On `glcheck` October 2026 in VND: the notes have 28 sections; V.1 closing 300 = B01 110; V.2 closing 1,500 = rows 131 + 211; V.4 net book value 1,500 = row 220; the borrowings table shows the control row of 341. | FR-83 | Passed |
| UAT-72 | Saving texts for general, standards, policy.cash and policy.inventory → sections I, III, IV.1, IV.3 show them (I starts with "Tên doanh nghiệp: …"), other sections show hints; key "nonsense" → 400; over 4,000 characters → 400 (unit test); an empty text clears. | FR-84 | Passed (400 key) / unit tests |
| UAT-73 | The notes as csv, xlsx, pdf answer 200; the PDF (3 pages) renders the Vietnamese titles, the company block, tables with repeated headers and the signatories (page images checked); empty tables say "no data". | FR-85 | Passed |
| UAT-74 | A 14-account tenant's notes contain V.1, V.2, V.3 (inventory 1200), V.7 … V.11 but not V.5 or V.6 and say so in a note. | FR-85, BR-113 | Unit test |
| UAT-75 | On `glcheck`: a loan "P12J-1" of 2,000 (start 1 Nov 2026; installments 1,000 on 20 Nov with interest 90 and 1,000 on 1 Jun 2028 with interest 45) is created with the default options. | FR-86, BR-134, 135 | Passed: `postedToLedger` true; JE dated 2026-11-01, Dr 1121 2,000 / Cr 341 2,000. An incomplete profile leaves the loan unposted: unit test |
| UAT-76 | Installment 1 is repaid with `paidOn` 20 Nov. | FR-87, BR-136 | Passed: JE dated 2026-11-20, Dr 341 1,000, Dr 635 90 / Cr 1121 1,090; no-interest and twice-repaid cases: unit tests |
| UAT-77 | Loans vs books as of 30 Nov, then with a second loan "P12J-2" (500) registered with `postDrawdown=false`. | FR-88, 90, BR-140 | Passed: ledger 2,000 / 1,000 after the repayment, the books moved by the same amounts (the 2,000 difference is a 2,000 credit already on 341 from the P1-2i check); the hand-booked loan raised the ledger by 500 with the books unchanged and the note "Some loans are not posted…" appeared; csv and xlsx 200. The zero-difference case: unit test |
| UAT-78 | Posting the drawdown of an older loan: once, twice, after a repayment, on a cancelled loan. | FR-88, BR-139 | Unit tests (first accepted; the others refused) |
| UAT-79 | Cancelling a posted loan, then a hand-booked one. | FR-89, BR-138 | Unit tests (reversal of `Loan:<id>` only for the posted one) |
| UAT-80 | Invoice 300 (created 1 Oct, due 30 months ahead, paid 20 Nov in the register); B01 as of 15 Nov, 24 Nov, 30 Nov and 10 Dec. | FR-91, 92, BR-143 | Passed: row 211 = 300 and 131 = 1,200 on 15 Nov; 211 = 0 on the other three dates; 270 = 440 each time. Handler recording (payment, refund −400, manual invoice, manual bill): unit tests |
| UAT-81 | A bill approved after the date, paid after the date, paid before the date; an invoice cancelled after / before the date; a partial payment and a later refund. | FR-92, BR-143, 144 | Unit tests (`MaturitySplitTests`) |
| UAT-82 | A past date with an estimated payment, with a document without any record, with only exact payments. | FR-93, BR-145 | Unit tests; live: no note for dates after today (the stack's clock is 25 Oct 2026) |
| UAT-83 | Migration 017 on the local database. | FR-94, BR-146 | Passed: 765 estimated invoice applications (3,848,529.50) and 126 bill applications (1,031,592.26); 62 cancelled invoices got `CancelledOn`; the schema-drift test passes |
| UAT-84 | Account 9001 (asset) with Dr 100 / Cr 4111 100 on `glcheck`; map it to row 111; remove it; refusals. | FR-95, 96, BR-148 … 151 | Passed: unmapped "9001=100" and 270 3,300 ≠ 440 3,400 → after mapping 111 = 400, 270 = 440 = 3,400 and no unmapped → after removal back to the first state; mapping row 270 and statement B99 → 400. Test data removed |
| UAT-85 | Mapping refusals: unknown row, total row, unknown prefix, twice, already placed by the standard layout, bad side. | FR-96, BR-149 | Unit tests (`StatementMappingTests`, 10 tests) |
| UAT-86 | Notes of November with `lang=vi` and `lang=en`; `lang=fr`. | FR-97, BR-152 | Passed: English title "Notes to the financial statements (Form B09-DN) — draft", "Nature of operations", "Cash and cash equivalents", columns Opening / Increase / Decrease / Closing; Vietnamese unchanged; fr → 400. A unit test checks the fixed wording of the English notes has no Vietnamese |
| UAT-87 | Save an English text for policy.cash, list the texts in both languages. | FR-98, BR-152 | Passed: the English list shows it, the Vietnamese list still shows its own text; the test text was removed afterwards |
| UAT-88 | Two non-cash entries in November (Dr 211 300 / Cr 331 300; Dr 331 200 / Cr 4111 200). | FR-99, BR-153 | Passed: section VII table 300 / 200 / total 500 and the confirmation note; cash and closing entries excluded: unit test. Entries removed afterwards |
| UAT-89 | Notes of November compared with October (English). | FR-100, BR-154 | Passed: V.1 has the columns Opening / Increase / Decrease / Closing / Prior closing; total row 300 \| 0 \| 0 \| 300 \| 300; the other cases: unit tests |
| UAT-90 | The English notes as pdf, xlsx, csv. | FR-97 | Passed: 200 each (PDF 65,423 bytes); the PDF labels and number format in English: unit test on the document model |
| UAT-91 | The admin build. | FR-101 | `tsc -b` clean; not clicked through in a browser in this task |
| UAT-92 | On `glcheck`: loan "P12K-1" of 2,000 (1 Nov 2026; installment 1 due 1 Dec 1,000 + interest 90; installment 2 due 1 Jun 2028 1,000 + 400) created with receiving account 1112; then a loan with account 331. | FR-102, BR-156 | Passed: JE dated 2026-11-01 Dr 1112 2,000 / Cr 341 2,000; 331 → 400 "is not an asset account". Other refusals and the older-loan case: unit tests |
| UAT-93 | Interest accrued to 16 Nov, again, then to 22 Nov. | FR-103, BR-157, 158 | Passed: total 45 (1 line), again 0 (no line), 22 Nov 18; entries JE-…013 Dr 635 45 / Cr 335 45 (16 Nov) and JE-…014 18 (22 Nov). Hand-booked, incomplete profile, repaid, cancelled: unit tests |
| UAT-94 | Installment 1 repaid on 1 Dec with `interestPaid` 100. | FR-104, BR-159, 160 | Passed: Dr 341 1,000, Dr 335 63, Dr 635 37 / Cr 1112 1,100. Interest paid below the accrual and negative interest: unit tests |
| UAT-95 | A second loan (500, interest 30) accrued to 16 Nov, then cancelled. | FR-105, BR-161 | Passed: 400 "Interest has been accrued on this loan: reverse the accrual entries first." |
| UAT-96 | Reconciliation of principal and accrued interest. | FR-106, BR-162 | Unit tests (separate account, same account on the 14-account chart). Live: the check date was before the loan start, so only the principal line was meaningful there |
| UAT-97 | Account 9701 (investing asset, flag investing) with Dr cash 100 / Cr 9701 100 and 9801 (expense) paid 40 in cash, Nov 2026. B03 direct and indirect before and after mapping 9701 → row 22 and 9801 → row 02. | FR-107, 108, BR-163, 164 | Passed: before: direct 20 = −40, 30 = 0, 50 = −40, unmapped "9701=100"; after: 20 = −40, 30 = 100, 50 = 60, unmapped none. Mapping screen for B03: side does not apply, 18 mappable rows; indirect layout and total rows: unit tests |
| UAT-98 | Notes (English) November: map 9801 to table VI.2. | FR-109, BR-165 | Passed: VI.2 total 0 before; "9801 Own overhead 40" and total 40 after |
| UAT-99 | Texts: write "Cash policy of 2026." for 2026; read the notes of Nov 2026 and Jan 2027; write a 2027 text; clear it. | FR-110, BR-166, 167 | Passed: Nov 2026 → FY 2026 "…of 2026."; Jan 2027 → FY 2027 inherits "…of 2026."; after the 2027 text it shows "…of 2027." while Nov 2026 is unchanged; after clearing 2027 it shows the 2026 text again; the editor list says inherited from 2026 |
| UAT-100 | Fiscal-year arithmetic and resolution order. | FR-110, BR-166, 167 | Unit tests: year ending in December, March, June; same year / earlier year / year 0 / later-only / other language |
| UAT-101 | The admin build. | FR-111 | `tsc -b` clean; not clicked through in a browser in this task |
| UAT-45 |
| FR-62 | Given a manager, then the profile panel saves; given a viewer, then its fields are read-only. | manual |
| FR-63 | Given a tenant with a filled report profile, when B01 for 31 Oct is requested with `format=pdf`, then the answer is HTTP 200, `application/pdf`, file `balance-sheet-b01-<timestamp>.pdf`, the bytes start with `%PDF-`, and the file has the company block, the title, "Tại ngày 31/10/2026 — Đơn vị tính: nghìn đồng", the rows with codes, and the three signatories; B01 (≈ 100 rows) runs over two pages with the table header repeated and "Trang 1/2" in the footer; B02 and B03 each fit one page. | UAT-46, 47 |
| FR-64 | Given the same request as CSV, then every code and amount in the PDF equals the CSV (same rounding in the same unit); a comparison range adds the column "Số kỳ trước"; unmapped accounts and notes are listed in small print; negatives are in parentheses; thousands are separated by a dot (1.234.567). | UAT-48 |
| FR-65 | Given a Linux container without fonts, then the PDF still shows "Bảng cân đối kế toán", "Kế toán trưởng", "Nguyễn Văn A" with all diacritics, and the file's fonts are embedded Noto Sans (regular and bold). | UAT-47 |
| FR-66 | When `format=pdf` is requested on `trial-balance/full`, `account-ledger` or `cash-flow`, then HTTP 400 "PDF is available for the statements B01, B02 and B03 only; use csv or xlsx for this report."; the Export PDF button exists on the B01, B02 and B03 tabs only. | UAT-49 |
| FR-67 | Given October of tenant `glcheck`, when B03 is requested with `method=indirect`, then the layout is "B03-DN-GT" and rows 01 (profit before tax), 08, 09 (receivables), 20, 21 … 40, 50, 60, 70 are present; in the unit-test ledger (sales on credit 1,000 with 600 collected, stock 500 bought on credit and 300 sold, wages, depreciation 100, interest, tax) the rows are 01 = 170, 02 = 100, 05 = −20, 06 = 50, 08 = 300, 09 = −400, 10 = −200, 11 = 300, 14 = −50, 15 = −40, 20 = −90. | UAT-51, 52 |
| FR-68 | Given the same request, then rows 21 … 36 equal those of `method=direct`, and 50 and 70 equal the direct ones. | UAT-51 |
| FR-69 | Given equipment of 800 bought on credit (payables rise, no cash moves), then indirect row 20 is 800 while the direct 20 is 0, `isBalanced` is false and the note says "differs from the direct method by 800"; a balance-sheet account the form does not follow (e.g. 356) is listed under "unmapped". | UAT-53 |
| FR-70 | Given `method=indirect` with `format=pdf`, `xlsx` or `csv`, a prior range and `unit=thousand`, then the file / columns / rounding behave as for the direct form; `method=foo` → HTTP 400 "The cash-flow method must be direct or indirect."; the admin B03 tab has a *Method* selector (Direct / Indirect). | UAT-54, 55 |
| FR-71 | Given an open invoice of 900 due in 18 months and a ledger balance of 1,500 on 131, when B01 is run as of today, then row 131 = 600, row 211 = 900, row 210 = 900, "B. Long-term assets" (200) rises by 900 and "A. Short-term assets" (100) falls by 900. | UAT-56 |
| FR-72 | Given an approved bill of 400 due in 14 months and 1,400 credited on 331 (as of 30 Nov), then row 311 = 1,000 and row 331 = 400; a pending bill, a paid bill and a bill due in 3 months are not long-term. | UAT-57 |
| FR-73 | Given the cases above, then 270 = 440 still holds, the flat balance sheet has the same total assets (3,300 on the test tenant), and an invoice larger than the 131 balance is cut to the balance (e.g. 900 against 400 → 400). A partly paid invoice counts its open part only; Paid and Cancelled invoices count nothing; a document created after the date counts nothing; exactly 12 months is still short-term. | UAT-56 … 59 |
| FR-74 | Given the date 30 Sep 2026 when today is later, then the note "The date is in the past …" is present; with a prior date each column has its own split; in thousand VND each row is rounded and `isBalanced` stays true on the exact amounts; PDF, Excel and CSV answer 200 with the new rows. | UAT-60, 61 |
| FR-75 | Given the demo tenant (14 accounts, no account 111) with a posted ledger, when B01 is requested for 31 Oct, then the layout is the 14-account one (title ends "biểu đồ 14 tài khoản"), 270 = 440, `isBalanced` true, nothing unmapped, and the totals equal the flat balance sheet; B02 and both B03 methods return rows and B03 direct 70 = indirect 70. Retained earnings in 3100 are counted once (421a) and the unclosed result goes to 421b. | UAT-62, 63, 64 |
| FR-76 | Given a tenant whose chart has neither 111 nor both 1000 and 4000, then the Vietnamese layout is used; given a tenant with both 111 and 1000, then Vietnamese. | UAT-64 |
| FR-77 | Given the Vietnamese template, then accounts 001, 002, 003, 004, 007, 008 exist as type OffBalance with a debit normal balance and no other account number starts with 0. | UAT-65 |
| FR-78 | Given account 001, when In 1,000 (10 Nov) and Out 300 (20 Nov) are recorded, then the list shows both; voiding needs a reason and works once; given a closed period, then recording or voiding in it is refused. | UAT-65, 67 |
| FR-79 | Given a journal entry with account 001 on a line, then HTTP 400 "…off-balance account: it has no double entry…"; the trial balance stays balanced with no line for 001; B01 is unchanged. | UAT-66 |
| FR-80 | Given the entries above, then November = opening 0, In 1,000, Out 300, closing 700; December opens at 700; Out above In makes the closing negative and flagged; voided entries disappear; CSV and Excel answer 200. | UAT-65, 67 |
| FR-81 | Given a loan of 2,000 with installments 500 / 700 / 800, then it is created; a schedule that does not add up to the principal, a duplicate number, an installment dated before the start are refused; repaying an installment twice, repaying a cancelled loan and cancelling after a repayment are refused. | UAT-68, 69 |
| FR-82 | Given that loan and a ledger balance of 2,000 on 341, then as of 30 Nov 2026 row 320 = 500 and row 338 = 1,500; as of 31 Mar 2027 row 320 = 1,200 and row 338 = 800 (the installment due exactly 12 months later is still short-term); 300 / 310 / 330 sum to 2,000 and 270 = 440; a loan larger than the 341 balance is cut to the balance. | UAT-68, 70 |
| FR-83 | Given tenant `glcheck` for October 2026, then the notes have 28 sections numbered I, II, III, IV.1 … IV.8, V.1 … V.12, VI.1, VI.2, VII, VIII.1, VIII.2; V.1 closing = B01 row 110, V.2 closing = rows 131 + 211, V.4 net book value = row 220; V.7 = 311 + 331 where there are payables; VI.1 / VI.2 totals = the revenue and expenses of B02. | UAT-71 |
| FR-84 | Given texts saved for general information, standards, cash policy and inventory policy, then those sections show them (the general section starts with the company block from the profile) and the other sections show their hint; an unknown key is refused (400); a text over 4,000 characters is refused; an empty text clears it. | UAT-72 |
| FR-85 | Given the notes of that period, then `format=pdf`, `xlsx` and `csv` answer 200; the PDF has the company block, the numbered sections with Vietnamese text, tables with a repeated header, "(Không phát sinh số liệu trong kỳ)" for an empty table, notes and the signatories; the screens exist for loans, off-balance and B09 and are read-only without the manage permission. | UAT-73, 74 |

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

### 6.8 Cash-flow statement B03-DN (direct method)
| ID | Rule |
|---|---|
| BR-64 | A cash movement is a non-cash line of an entry that touches a cash account (`IsCash`). Its amount is *credit − debit*: positive = cash received, negative = cash paid. The **first row of the layout** (top to bottom) whose account prefix matches the counter-account **and** whose direction matches takes it — a movement is counted once. |
| BR-65 | A movement no row names falls back by the account's cash-flow category: operating → 06 (received) or 07 (paid). |
| BR-66 | An investing or financing movement that no row names is **not** moved to another activity: it is listed as *unmapped* and the closing-cash check fails, so the accountant sees the gap (fix by extending the layout or the account's category). |
| BR-67 | Outflows are shown negative; total rows are signed sums (20 = 01 … 07; 30 = 21 … 27; 40 = 31 … 36; 50 = 20 + 30 + 40; 70 = 50 + 60 + 61). Row 60 is the cash at the day before the range starts. |
| BR-68 | Row 70 must equal the cash on the books at the end of the range, in every column (current and prior); otherwise the statement is flagged and says why. |
| BR-69 | Row 61 (foreign-exchange effect) is 0 until foreign currency exists (P1-4). The layout is a **draft** like B01 / B02 (BR-63). Entries between cash accounts, closing entries and voided entries never count (BR-51). |

Row mapping (account prefix → row; "In" = cash received, "Out" = cash paid):

| Row | Meaning | Counter-account prefix |
|---|---|---|
| 01 | Cash from sales and services | In: 511, 131, 3331, 3387 |
| 02 | Paid to suppliers for goods and services | Out: 331, 151 … 158, 133, 611, 621, 627, 641, 642 |
| 03 | Paid to employees | Out: 334 |
| 04 | Interest paid | Out: 635, 335 |
| 05 | Income tax paid | Out: 3334, 8211 |
| 06 / 07 | Other receipts / payments from operations | Fallback: any operating movement not above |
| 21 / 22 | Fixed assets bought / sold | Out: 211, 213, 217, 241 / In: 211, 213, 217 |
| 23 / 24 | Loans given and debt instruments bought / collected | Out / In: 128 |
| 25 / 26 | Equity investments made / recovered | Out / In: 221, 222, 228 |
| 27 | Interest and dividends received | In: 515 |
| 31 / 32 | Capital received / capital returned and shares bought back | In: 411 / Out: 411, 419 |
| 33 / 34 | Borrowings received / principal repaid | In / Out: 341, 343, 344 |
| 35 | Finance-lease principal paid | Out: 342 |
| 36 | Dividends and profit paid | Out: 421 |
| 60 / 61 / 70 | Opening cash / exchange-rate effect (0) / closing cash | formulas |

### 6.9 Closing policy and year end
| ID | Rule |
|---|---|
| BR-70 | A tenant with no policy is **Monthly** with a **December** year end; the behaviour of BR-41 then applies unchanged. |
| BR-71 | The policy has two settings: the mode (Monthly, YearEnd) and the fiscal-year end month (1 … 12). Anything else is refused (HTTP 400). |
| BR-72 | Under **YearEnd**, closing a period that is not the last of the year runs the checks of BR-40, writes **no** closing entry and locks the period. |
| BR-73 | Under YearEnd, closing the **last period of the year** writes one closing entry that clears every leaf revenue / expense account by its **remaining cumulative balance up to the period end** (not by the period's own activity) into the income-summary key, then retained earnings. Months closed earlier under Monthly are already at zero, so mixing the modes never closes anything twice. The entry is dated the period end and described "Year-end closing …". |
| BR-74 | A period is the **last of the year** when its end date is the last day of the fiscal-year end month (a quarterly period ending 31 December also qualifies; a period ending 15 December does not). |
| BR-75 | The policy may be changed only **between years**: refused while the most recently closed period is not the last period of a year ("the fiscal year is in progress"). Saving the same values is always allowed. Reopen the closed periods first if a change is really needed. |
| BR-76 | Reopening works as before (BR-42): only the latest closed period, with a reason; if that period holds the closing entry, the entry is voided. |
| BR-77 | **Carry-forward.** Account balances are cumulative over all dates. After the year-end entry revenue and expense accounts therefore open the next year at zero, retained earnings holds the year's result and every balance-sheet account keeps its balance — no opening entry is written, and the trial balance of the new year opens with the closing balances of the old one. |
| BR-78 | The option "close without closing entries" (`postClosingEntries=false`) stays available in both modes; it locks the period only. |

### 6.10 Amount unit and report header (P1-2e)
| ID | Rule |
|---|---|
| BR-80 | The unit of a statement is the request's `unit` if given, else the tenant's profile unit, else VND. An unknown value is refused (400). |
| BR-81 | **Rounding applies to the statement rows only** (B01, B02, B03), never to the books: an account-driven row is the exact sum of its accounts divided by the unit and rounded **half away from zero**. The trial balance, the ledger and the older flat reports stay exact. |
| BR-82 | **Total rows are the signed sum of the rounded rows**, so the printed form foots. A total can therefore differ by a few units from the rounded exact total (example below). |
| BR-83 | The integrity checks are made on the **exact** amounts: B01 270 = 440 and B03 70 = cash on the books. When they hold but the rounded totals differ, `isBalanced` stays true and a note reports the rounding difference in the chosen unit. |
| BR-84 | Unmapped accounts and movements are always listed in VND, whatever the unit. |
| BR-85 | The header (company block, signatories) is printed only if the profile has text; empty lines are omitted, and the signature block is omitted when no signatory is filled in. The header never changes an amount. |
| BR-86 | Reading the profile needs `accounting:reports:view`; saving it needs `accounting:accounts:manage` (chart / settings level). A save replaces the whole profile. Text is trimmed. |

*Worked example (thousand VND).* Cash 1,400 and inventory 1,400 are assets; equity is 2,800. Exact: 2,800 = 2,800. Rounded rows: cash 1, inventory 1 → total assets 2; equity 3 → total sources 3. The statement shows 2 and 3, says
"Rounding difference of −1 thousand VND between total assets (270) and total sources (440): the exact amounts agree" and `isBalanced` is true. On the local test tenant, 3,300 VND of assets split over several rows showed 4 against 3 the same way.

### 6.15 Statements for the 14-account chart (P1-2i)
| ID | Rule |
|---|---|
| BR-109 | **Which layout.** The chart is read from the tenant's accounts: account **111** → Vietnamese; else accounts **1000 and 4000** → 14-account; else Vietnamese. The same choice applies to B01, B02, B03 direct and B03 indirect, and to the notes B09. |
| BR-110 | **B01 (14 accounts):** 110 cash = 1000; 131 receivables = 1100 (debit balance), 132 supplier advances = 2000 (debit balance); 141 inventory = 1200; 221 fixed assets = 1500; 311 payables = 2000 (credit balance), 312 customer advances = 1100 (credit balance), 313 taxes = 2200, 319 other payables (accrued liabilities, salaries payable, goods received not invoiced) = 2100; 411 owner equity = 3000; 421a retained earnings = 3100; **421b = only the result not yet closed** (3100 is taken once, by 421a). The maturity split of trade items works on 1100 / 2000. |
| BR-111 | **B02 (14 accounts):** 01 revenue = 4000 + 4100; 11 cost of goods = 5000; 26 administration = 5100 + 5200; no deductions, financial or other lines and no income-tax lines (the chart has none); 60 = 50 = 30. |
| BR-112 | **B03 (14 accounts), direct:** receipts 4000 / 4100 / 1100 (row 01); payments to suppliers and for expenses 2000 / 1200 / 5000 / 5100 (02); to employees 5200 / 2100 (03); taxes 2200 (05); investing 1500 (21 / 22); financing 3000 (31 / 32) and 3100 (36); other operating movements fall back to 06 / 07. **Indirect:** profit before tax, changes of 1100, 1200 and of payables 2000 + 2100 + 2200, then the direct investing and financing rows; the operating total must still equal the direct one (BR-100). |
| BR-113 | Every account of the 14-account chart is on B01 or B02 (a test checks it). The notes of each layout say it is a simplification and which chart it was written for. |

### 6.16 Off-balance accounts and register (P1-2i)
| ID | Rule |
|---|---|
| BR-114 | **Outside the double entry.** Class 0 accounts (type OffBalance) are never used in a journal line, a posting key or an automatic posting: the system refuses it with a message pointing to the register. Therefore no trial balance, closing, statement or cash-flow figure can be affected by them. |
| BR-115 | A register entry has an off-balance account (001 – 008, active), a direction **In** (raises the balance) or **Out** (lowers it), an amount **above 0**, a date (default today), an optional description and reference. The normal balance of all class 0 accounts is debit. |
| BR-116 | **Period lock.** If a fiscal period contains the entry date and is **closed**, recording or voiding in it is refused; a date outside every period is accepted. |
| BR-117 | **Void.** Needs a reason, works only on a posted entry and once; a voided entry never counts. There is no edit: correct by voiding and re-entering. |
| BR-118 | **Report.** For a range: opening = all posted entries dated before the range (In minus Out); In and Out = posted entries inside the range; closing = opening + In − Out. An account with no opening, In, Out or closing is left out; a **negative closing** is flagged and noted. The range is by entry date (inclusive). |
| BR-119 | The register uses the journal permissions: `accounting:journals:view` (read) and `accounting:journals:manage` (record, void); the report needs `accounting:reports:view`. |

*Worked example.* Account 001 *Tài sản thuê ngoài*: 10 Nov In 1,000 (a rented forklift); 20 Nov Out 300 (part returned). November: opening 0, In 1,000, Out 300, closing 700. December: opening 700, no movement, closing 700. Voiding the 1,000 entry leaves an Out of 300 → closing −300, flagged negative.

### 6.17 Loans and the split of borrowings (P1-2i)
| ID | Rule |
|---|---|
| BR-120 | A loan has a unique number (per tenant), a lender, a principal above 0, an interest rate (information only: no interest is accrued or posted from it) and a start date. |
| BR-121 | **Schedule.** Installments (due date, principal, interest) must have principals adding up to the loan principal (±0.01), each above 0, each due **after** the start date. With no schedule the loan is one installment of the whole principal on its maturity date. |
| BR-122 | **Repayment.** An installment is repaid once, with a date (default today) that is not before the start; the loan becomes Repaid when all installments are repaid; nothing can be recorded on a Cancelled or Repaid loan. |
| BR-123 | **Cancel.** Only an Active loan **without any repayment**, with a reason. A cancelled loan owes nothing at any date. |
| BR-124 | **Outstanding at a date D** = principal of the installments not repaid on or before D, for loans started on or before D, not cancelled. **Long-term part** = outstanding installments due **later than D + 12 months**. Both are exact for any past date because each repayment keeps its date. |
| BR-125 | **B01.** The long-term part of all loans, capped by the credit balance of 341, is taken out of row 320 (short-term borrowings = 341 − long-term part) and added to row 338 (long-term borrowings = 343 + long-term part). No total changes. A loan not yet booked in the ledger (341 too small) is cut to the ledger. |
| BR-126 | *Superseded for new loans by BR-134 … 140 (P1-2j).* A loan registered without posting (or created before P1-2j) still has its drawing, repayments and interest as **journal entries** made by hand. Permissions reuse `accounting:bank:view` / `accounting:bank:manage`. |

*Worked example (start 1 Oct 2026, 341 = 2,000).* Installments: 500 due 30 Jun 2027, 700 due 31 Mar 2028, 800 due 31 Mar 2029. As of **30 Nov 2026** the limit is 30 Nov 2027: 700 and 800 are later → long-term 1,500, short-term 500 (row 320 = 500, row 338 = 1,500). As of **31 Mar 2027** the limit is 31 Mar 2028: the 700 installment is due exactly then → still short-term, only 800 is long-term (row 320 = 1,200, row 338 = 800). After the first installment is repaid on 1 Dec 2026, B01 of 30 Nov 2026 still shows it as owed; B01 of 31 Dec 2026 does not.

### 6.18 The notes B09-DN (P1-2i)
| ID | Rule |
|---|---|
| BR-127 | **Two kinds of content.** *Written by the accountant:* sections I (general), III (standards), IV.1 … IV.8 (policies: cash, receivables, inventory, fixed assets, provisions, revenue and expenses, tax, foreign currency), VII (non-cash transactions), VIII.1 (events after the period), VIII.2 (other). *Generated:* II (period, fiscal-year end from the closing policy, currency, unit), the company block at the top of I, and every table V.1 … V.12, VI.1, VI.2. A section without text shows its hint, not a blank. |
| BR-128 | **Movement table** (V.1, V.2, V.3, V.4, V.5, V.6, V.7, V.8, V.9, V.11): per account, on the group's natural side (debit for assets, credit for liabilities and equity): opening = balance the day before the range; increase and decrease = debit / credit activity of the range **including closing entries**; closing = balance at the end of the range; one subtotal per group. Accounts with nothing to show are left out; a table with no rows prints "no data" instead of an empty frame. |
| BR-129 | **Fixed assets (V.4):** cost (211, 212, 213, 217) and accumulated depreciation (214, credit-normal, shown as a positive amount) are two groups; **net book value = cost − depreciation** in the opening and the closing columns, and equals B01 row 220 (14-account chart: 1500 only). |
| BR-130 | **Trade items (V.2, V.7)** carry a memo row "of which due after 12 months" from the maturity split (BR-103 … 107). **Borrowings (V.10)** come from the **loan ledger**: per active loan its principal, the outstanding amount at the end of the range, the part due within 12 months and the part due later, a total, and the ledger balance of 341 as a control row. |
| BR-131 | **Revenue and expenses (VI.1, VI.2)** use the activity **without** closing entries (as B02): revenue 511, 512, 515, 711; costs 521, 632, 635, 641, 642, 811, 821 (14-account chart: 4000 + 4100; 5000 + 5100 + 5200). **V.12 off-balance** is the register report (BR-118) in the unit. |
| BR-132 | **Unit and rounding.** Every amount is rounded **row by row** in the chosen unit (BR-80 … 83); a subtotal adds the rounded rows; a row can differ by a unit from opening + increase − decrease (the note says so). The header and the signatories come from the report profile; the layout is a **draft** for the accountant's review. |
| BR-133 | **Texts.** One text per section key per tenant; trimmed; at most 4,000 characters; an empty text clears it; a key that is not a note section is refused. Reading needs `accounting:reports:view`, saving needs `accounting:accounts:manage` (as the report profile). Texts are not versioned and are reused in every period. |

### 6.19 Loans posted to the books (P1-2j)
| ID | Rule |
|---|---|
| BR-134 | Two posting keys are added: **LoanPayable** (Vietnamese chart 341; 14-account chart 2100, shared with accrued liabilities because that chart has no borrowings account) and **InterestExpense** (635; 5100). A tenant's profile gets them by default the first time something is posted; an incomplete profile only logs and never blocks the loan. |
| BR-135 | **Drawdown** = Dr Bank / Cr LoanPayable for the principal, dated the start date, source "Loan:<loan id>" (idempotent). Since P1-2k the debit goes to the loan's **receiving account** when it has one (BR-156), else to the Bank key. |
| BR-136 | **Repayment** of an installment = Dr LoanPayable (principal) + Dr InterestExpense (the interest scheduled for that installment) / Cr Bank (both), dated the day it was repaid, source "LoanRepayment:<installment id>". Interest is booked **when it is paid** unless it was accrued first (P1-2k, BR-157 … 159); the interest rate of the loan stays information. |
| BR-137 | A loan has a flag **PostedToLedger**: set when its drawdown was posted. Repayments are posted only for a posted loan. Loans created before P1-2j are not posted (their entries were made by hand). |
| BR-138 | **Cancel** (allowed only while nothing is repaid) reverses the drawdown entry of a posted loan, dated today; it reverses nothing for a hand-booked loan. |
| BR-139 | **Post drawdown** later: only for an active loan with no repayment that is not yet posted; refused for a posted, repaid or cancelled loan, and when the posting profile is incomplete. |
| BR-140 | **Reconciliation.** Ledger = Σ outstanding principal at the date of loans that are not cancelled and started on or before it. Books = credit − debit of the LoanPayable account and its sub-accounts (accounts whose number starts with it). Difference = ledger − books; zero = reconciled. Notes tell the accountant when some loans are not posted and, on the 14-account chart, that the account also holds accrued liabilities. |
| BR-141 | **Chart specifics.** On the Vietnamese chart the key points at 341 (sub-accounts such as 3411 are included in the balance by prefix); a tenant with its own chart maps the key in the posting profile. The key must be a postable account (not a parent, not inactive, not off-balance), as for every posting key. |

### 6.20 Payment history (P1-2j)
| ID | Rule |
|---|---|
| BR-142 | A **payment application** = (invoice or bill, day, amount; a refund is negative, source key unique per tenant). It is written by the payment event (day = the event's processed time), the refund event, "mark invoice paid" (the outstanding amount, today) and "mark bill paid" (the bill amount, today). The documents keep their running totals; the register adds the dates. |
| BR-143 | **Invoice open on date D** = amount − Σ applications dated on or before D (floored at 0), for an invoice created on or before D; a cancelled invoice is left out from its cancellation day (`CancelledOn`; unknown = always void). It is long-term when its due date is later than D + 12 months; the sum is capped by the ledger as before (BR-105). |
| BR-144 | **Bill payable on date D**: approved on or before D (`ApprovedOn`, else its creation date) and not paid by D (applications on or before D below its amount). Pending bills are never payables. |
| BR-145 | **Fallback and honesty.** A document with no application at all uses its state today (Paid = settled, otherwise its running total); an estimated application or such a fallback on a past date marks the split **Estimated** and the statements / notes say so. Exactness therefore holds for payments recorded from now on. |
| BR-146 | **Backfill** (migration 017): one estimated application per invoice with a paid amount (dated by its last update) and per paid bill; `ApprovedOn` from the last update of an approved bill; `CancelledOn` from the last update of a cancelled invoice. |
| BR-147 | Only documents that can matter are read: due later than (earliest date + 12 months) and existing by the latest date, with **all** their applications (including later ones, so a document paid later is not mistaken for one without records). |

### 6.21 Account mapping on B01 / B02 (P1-2j)
| ID | Rule |
|---|---|
| BR-148 | A mapping = (statement B01-DN or B02-DN, row code, account-number prefix, side Net / DebitOnly / CreditOnly). It **adds** a term to the row on top of the standard layout of the chart (Vietnamese layout for a chart that is neither template); the engine, the unmapped check and every export read the terms, so they need no other change. |
| BR-149 | Only rows that **add up accounts** take a mapping (no formula, no computed source). A prefix must match at least one account, may be mapped once per statement, and may not be a prefix the standard layout already places in some row. Saving replaces the mapping of one statement: unchanged entries are kept, new ones added, the others removed. |
| BR-150 | *Extended in P1-2k (BR-163 … 165):* the mapping also feeds B03 and the notes B09. The maturity caps (B01 specials) still use the standard prefixes of the layout. |
| BR-151 | The unmapped list of the mapping screen = accounts of the statement's types with a non-zero balance (B01) or activity of the period without closing entries (B02) that no row takes. Permissions: view `accounting:accounts:view`, save `accounting:accounts:manage`. |

### 6.22 Notes in English, non-cash and comparison (P1-2j)
| ID | Rule |
|---|---|
| BR-152 | **Language.** `lang` = `vi` (default, also when empty) or `en`; other values are refused. All fixed wording comes from one table per language; account names, the company block and the accountant's texts are not translated. Texts are stored per (tenant, section, language); old texts are Vietnamese. The language is explicit (not taken from the browser's Accept-Language) so a report never changes language by surprise. |
| BR-153 | **Non-cash transactions.** From the journal lines of the range (closing entries and entries that touch an account flagged as cash are left out), per entry: assets acquired on credit = min(debit on fixed-asset and construction-in-progress accounts, credit on liability accounts); debt converted into equity = min(debit on liability accounts, credit on equity accounts other than retained earnings). The table is a starting point: the note asks the accountant to confirm it. |
| BR-154 | **Comparison.** Both dates or none; start ≤ end. The same tables are built for the prior range and joined by row label: movement tables get "Prior closing", revenue / expense tables "Prior period"; a row the prior range lacks shows 0; the borrowings and off-balance tables have no comparison column. |
| BR-155 | **English form.** Amounts print as 1,300 and (1,300); dates stay dd/MM/yyyy; the unit reads "dong / thousand dong / million dong". The CSV / Excel table already used English headers and now ends the period line with the language. |

### 6.23 Loans: receiving account and accrued interest (P1-2k)
| ID | Rule |
|---|---|
| BR-156 | **Receiving account.** Optional per loan, an active postable asset account (bank or cash). It replaces the Bank key on the money line of the drawdown and of every repayment. It can be set when the loan is created and when an older, unposted loan is posted; it cannot be changed afterwards (the entries already refer to it). |
| BR-157 | **Accrual.** For an unpaid installment with scheduled interest I, the period is from the previous installment's due date (or the loan start) to its due date, D days long. On date X (after the period start) the interest accrued to X is round(I × min(1, days(X − start) ÷ D), 2). The amount posted is that figure minus what is already accrued on the installment; nothing is posted when it is not positive. Worked example: start 1 Nov, due 1 Dec (30 days), I = 90 → 16 Nov: 45; 22 Nov: 63 (post 18); 1 Dec or later: 90. Calendar days, not actual/360. |
| BR-158 | **Accrual entry** = Dr InterestExpense / Cr InterestPayable, dated the accrual date, source "LoanAccrual:<id derived from the installment and the date>" (idempotent). Only posted active loans accrue; if the posting profile is incomplete the accrued amount is not remembered, so the next run catches up. Running it needs `accounting:bank:manage`. |
| BR-159 | **Repayment entry** (replaces BR-136 when interest is accrued): Dr LoanPayable (principal); Dr InterestPayable = interest accrued on the installment; Dr InterestExpense = interest paid − accrued (a **credit** when negative); Cr the receiving account (principal + interest paid). With nothing accrued it is the BR-136 entry. Examples: accrued 63, paid 100 → Dr 63 payable + Dr 37 expense; accrued 90, paid 80 → Dr 90 payable, Cr 10 expense. |
| BR-160 | **Interest paid** is optional on the repayment command; empty = the scheduled interest; it must be ≥ 0; it replaces the installment's interest from then on (the schedule keeps what was actually paid). The principal paid is always the scheduled principal. |
| BR-161 | **Cancel** (BR-138) is also refused once any interest has been accrued on the loan: the accrual entries must be reversed by hand first. |
| BR-162 | **Interest check.** Ledger = Σ interest accrued on the unpaid installments of posted loans (today's state; accruals are not kept by date); books = credit − debit of the InterestPayable account (and its sub-accounts) on the date. When the key and LoanPayable are the same account (14-account chart: 2100) there is one check: ledger principal + accrued interest against the account's balance, and a note says so. |

### 6.24 Mapping for B03 and the notes; texts per fiscal year (P1-2k)
| ID | Rule |
|---|---|
| BR-163 | **B03 direct.** A mapping adds a term (prefix, direction of the row) to a row that takes counter-accounts (rows with terms); the first row whose term matches the counter-account and the direction takes the movement (BR-64 …). Totals, formula, fallback and special rows take no mapping. The "balances taken" side does not apply. |
| BR-164 | **B03 indirect.** A mapping adds the prefix to a prefix-driven row (balance-change or expense/income rows) and to the accounts the form deals with, so it leaves the unmapped list; direct-source rows use the direct layout **with** the tenant's mapping, so 20 (indirect) still equals 20 (direct). |
| BR-165 | **Notes.** The mapping row code is the table: V.1 cash, V.2 receivables, V.3 inventories, V.4 fixed assets (cost), V.5 construction in progress, V.6 prepaid, V.7 payables, V.8 taxes, V.9 accrued, V.11 equity, VI.1 revenue, VI.2 expenses. The prefix is added once to that table's prefix set. A prefix already in the standard sets of the chart is refused. B03 and the notes show their unmapped items on the report itself, not on the mapping screen. |
| BR-166 | **Fiscal year** of a date = the year in which its fiscal year ends: with year end December the calendar year; with year end month M < 12, the date's year if its month ≤ M, else the next year (year end March: 31 Mar 2026 and 1 Apr 2025 are year 2026; 1 Apr 2026 is year 2027). The notes use the year of their end date; the editor defaults to it. |
| BR-167 | **Text resolution.** For a section, language and fiscal year Y the text is the non-empty text with the greatest year ≤ Y (year 0 = "every year", the texts written before versions; it is the oldest). Saving writes the chosen year only; an empty text for a year is stored as empty and ignored, so the year follows the earlier text again. A year is 0 or 1 … 9999. One text per tenant, section, language and year (migration 021). Edits overwrite the year's text: there is no history of edits. |

### 6.25 Day count, month-end job, history and languages (P1-2l)
| ID | Rule |
|---|---|
| BR-168 | **Day count** of a loan: 0 schedule (BR-157), 1 actual/365, 2 actual/360. 1 and 2 need a rate > 0. It is set when the loan is created and fixed once interest is accrued or a repayment recorded. Migration 022 (`Loans.DayCountBasis`, default 0, so existing loans keep their behaviour). |
| BR-169 | **Interest by days.** For an unpaid installment whose period runs from the previous due date (or the loan start) to its due date, the interest to date X is round(Σ over each day d in [period start, min(X, due date)) of outstanding principal(d) × rate ÷ 100 ÷ basis, 2), where outstanding(d) = loan principal − the principal of installments repaid on or before d. The amount posted is that figure minus what is already accrued (BR-158 entry, idempotent). The schedule's interest column is not used. Example: 1,000,000, 12 %, 30 days: actual/360 → 10,000.00, actual/365 → 9,863.01; leap year 29 days actual/360 → 9,666.67. |
| BR-170 | **Interest paid default** (changes BR-160 for loans counted by days): empty = the interest by days from the period start to the payment day (at most the due date), stored as the installment's interest; for the schedule day count it stays the scheduled interest. |
| BR-171 | **Month-end job.** Opt-in per tenant (`ClosingPolicies.AutoAccrueLoanInterest`, default false). It checks hourly, handles each day once per service run, and runs the accrual command for each opted-in tenant (inside that tenant's scope) with as-of = the last day of the month before today. It only posts the difference since the last accrual. One tenant's failure is logged and does not stop the others. It starts one minute after the service. |
| BR-172 | **Closed period.** The job sets `SkipClosedPeriod`: when the fiscal period containing the as-of date exists and is not Open, nothing is posted (a normal posting into a closed period would fall into the current month). A period that does not exist yet is created Open by the posting service. |
| BR-173 | **History.** `NoteTextRevisions` is append-only: (tenant, section, language, fiscal year, text, user = CreatedBy, time = CreatedAt). A revision is written when the trimmed text differs from the stored one, for a new non-empty text, and for a clearing (empty text). Reading returns the newest 50. The current text stays in `NoteTexts` (BR-167 now has a history of edits). Restoring is a normal save of the old text. |
| BR-174 | **Languages.** vi, en, ja, zh, fr; the code is matched by prefix. Texts are stored per language (`NoteTexts.Language` ≤ 5 characters); a section's title and hint come from the section list (vi, en) or `NoteSectionTranslations` (ja, zh, fr). Amounts: en, ja, zh show 1,234.56; vi, fr the Vietnamese form in the PDF. Dates are dd/MM/yyyy in all languages. |
| BR-175 | **PDF.** Needs a font with the glyphs: the embedded Noto Sans covers vi, en and fr. ja and zh are refused with 400 (BusinessRuleException) until a CJK font is embedded. |

### 6.26 VAT and e-invoices (P1-3)
| ID | Rule |
|---|---|
| BR-176 | **Split.** Prices including tax: VAT = round(gross × r ÷ (100 + r), 2), net = gross − VAT. Prices excluding tax: net = amount, VAT = round(net × r ÷ 100, 2), gross = net + VAT. Rounded half away from zero, so net + VAT always equals gross. r is 0–100; r = 0 gives VAT 0. |
| BR-177 | **Amounts.** `Invoice.Amount` / `Bill.Amount` stay the **gross** (what is owed and paid: payments, aging and maturity use it). Net is `SubTotal`, VAT `TaxAmount`, the rate `TaxRatePercent` (migration 023; the first two existed since migration 001). Documents created before P1-3 have net = gross and VAT 0. |
| BR-178 | **Rate source.** The tenant's `TenantTaxSettings` (default rate, prices include tax; default 0 % and included) unless the command carries a rate. Orders and purchase orders carry no tax, so the rate is a tenant choice; with "prices exclude tax" the invoice is larger than the order total the customer paid through Payment (risk, see 15). |
| BR-179 | **Invoice entry** Dr AccountsReceivable gross / Cr SalesRevenue net / Cr VatOutput VAT (VAT line only when VAT > 0). |
| BR-180 | **Adjust VAT** keeps the gross: the new split is computed as "included". The difference in VAT is posted once (source `InvoiceVatAdjust`, id from the invoice, rate and time): more VAT → Dr SalesRevenue / Cr VatOutput; less VAT → the reverse. No change → nothing posted. Refused for a cancelled invoice, for a rate outside 0–100, while an e-invoice is issued, and when the entry cannot be posted (the document is not changed). |
| BR-181 | **Return.** The refund is gross; VAT inside = round(refund × r ÷ (100 + r), 2) at the invoice's rate; Dr SalesReturns (refund − VAT) / Dr VatOutput / Cr AccountsReceivable. |
| BR-182 | **Bill entry** Dr GoodsReceivedNotInvoiced net / Dr VatInput VAT / Cr AccountsPayable gross. The rate can be replaced when the bill is approved (the supplier's invoice is the authority); a bill already approved keeps its split. |
| BR-183 | **VatInput** maps to 1331 on the VAS chart (a leaf under 133) and to the VAT output account on the 14-account chart (one tax account: input nets against output; the return then does not take a carried-over amount from the ledger). |
| BR-184 | **Listing.** Sales: invoices that are not cancelled with creation date in the period, plus every ledger line on the VAT output account (and sub-accounts) whose entry is not an invoice, bill or their reversal (returns, adjustments, POS, manual) with VAT = credit − debit; purchases: bills approved or paid by approval date, plus other lines on the VAT input account with VAT = debit − credit. Subtotals by rate (entries have no rate). Difference = listing total − account movement; non-zero is explained in a note (e.g. an invoice cancelled after the period). |
| BR-185 | **Return 01/GTGT (simplified).** [22] = debit balance of the input VAT account before the period (0 when input and output share one account); [23] net of the purchases listing; [24] input VAT; [25] = [22] + [24]; by rate S = net and T = VAT of the sales listing; [35] total output VAT of the listing; [40] = max(0, [35] − [25]); [43] = max(0, [25] − [35]). Amounts are rounded per row in the report unit (default from the report profile). DRAFT: not the tax authority's filing file; the accountant reviews the form and codes. |
| BR-186 | **E-invoice.** Needs the tenant's provider, series and company tax code (Settings → Tax) and an invoice that is not cancelled. One active (not cancelled) e-invoice per invoice (unique index); Failed ones are reused. The declaration (seller from the report profile and tax code, buyer, one aggregated line "Order …", rate, net, VAT, gross, VND) is stored as JSON. The buyer without a name is "Retail customer". |
| BR-187 | **Cancel** needs the reason and the provider's acceptance; afterwards the invoice may get a new e-invoice. The accounting invoice and its entries are not changed by issuing or cancelling an e-invoice. |
| BR-188 | **Sandbox provider** (`sandbox`): numbers the series one after another per tenant (7 digits), lookup code = first 10 hex characters of a SHA-256 of seller tax code, series, number and gross; no network, no tax-authority code. Real providers implement `IEInvoiceProvider` and register under their own code. |



### 6.14 Short- / long-term split of trade receivables and payables (P1-2h)
| ID | Rule |
|---|---|
| BR-103 | **Twelve-month rule.** At the balance-sheet date D, an open trade invoice or an approved unpaid bill whose due date is **later than D + 12 months** is long-term; due on D + 12 months or earlier it is short-term. |
| BR-104 | **Open amount.** Invoice: Amount − AmountPaid while the status is Unpaid or Overdue (Paid and Cancelled count nothing). Bill: its amount while Approved (a Pending bill has no ledger entry yet and a Paid bill is settled). A document created after D is ignored. |
| BR-105 | **The ledger caps the split.** Long-term receivables ≤ the sum of the debit balances of the 131 accounts; long-term payables ≤ the sum of the credit balances of the 331 accounts. Amounts with no document behind them (hand-written entries) stay short-term. A credit balance on 131 (customer advances, row 312) or a debit balance on 331 (supplier advances, row 132) is never split. |
| BR-106 | **Only presentation moves.** Row 131 = account total − long-term receivables; row 211 = long-term receivables; row 311 = account total − long-term payables; row 331 = long-term payables. The group totals change (130 and 100 fall, 210 and 200 rise; 310 falls, 330 rises) but 270, 300 and 440 do not. |
| BR-107 | **Past dates are approximate.** The platform keeps the total paid on an invoice, not the date of each payment, so for a date before today the open amounts are today's. The statement adds a note whenever a split was applied to a past date. For today's date the split is exact. |
| BR-108 | Loans have no due dates in the platform: 341 stays short-term, 343 and 344 long-term by account (BR-60). The split uses the due date of the document, not the payment terms of the order. |

*Worked example (as of 30 Nov 2026, VND).* The ledger holds 1,500 on 131 and 1,400 credited on 331. Open documents: invoice A 900 due 25 Apr 2028 (18 months), invoice B 300 due 25 Jan 2027 (3 months), invoice C 500 already paid; bill X 400 due 25 Dec 2027 (13 months), bill Y 100 due 25 Jan 2027, bill Z 50 pending.
Limit = 30 Nov 2027. Receivables: A is later → long-term 900; B is earlier → short; C is paid → nothing. Payables: X is later → long-term 400; Y short; Z pending → nothing. Result: 131 = 1,500 − 900 = 600, 211 = 900; 311 = 1,400 − 400 = 1,000, 331 = 400. Total assets and total sources are unchanged.

### 6.13 B03 by the indirect method (P1-2g)
| ID | Rule |
|---|---|
| BR-94 | `method` is `direct` (default, any case) or `indirect`; anything else is refused (400). The indirect statement has the layout id "B03-DN-GT" and is a **draft** like the other statements (BR-63). |
| BR-95 | **Row 01** = profit before tax: revenue minus expenses of the range on every revenue / expense account except income tax (821x); closing entries never count. |
| BR-96 | **Balance-change rows** use the cumulative balances the day before the range starts and at its end: *cash effect = increase of (credit − debit)*. So an increase of a liability (or accumulated depreciation, provisions) adds cash; an increase of an asset (receivables, inventory, prepaid) takes cash. |
| BR-97 | **Add-backs and removals**: row 06 adds back the interest expense (635) of the range; row 05 removes the interest income (515), which the form shows in investing (row 27). Row 04 (exchange-rate effect) is 0 until foreign currency exists. |
| BR-98 | **Rows 14 and 15** (interest paid, income tax paid) are the cash movements of the direct form (rows 04 and 05). Rows 13, 16 and 17 are not derived (0): securities changes are shown in investing and other operating cash is already in profit and working capital — counting them again would double count. |
| BR-99 | Rows 21 … 36, 60, 61 are the direct-method cash rows; 20 = 08 + 09 … + 17, 30, 40, 50 = 20 + 30 + 40, 70 = 50 + 60 + 61 as in BR-67. |
| BR-100 | **Reconciliation.** Both methods describe the same cash, so indirect 20 must equal direct 20 and indirect 70 must equal the cash on the books, in every column. Otherwise `isBalanced` is false and a note gives the difference (in the chosen unit). The statement is **never forced** to agree. |
| BR-101 | A difference arises from **non-cash transactions** (e.g. equipment or stock bought on credit moves payables but no cash), from accounts the form does not follow, and from the approximation of rows 02 / 03 (a disposal lowers accumulated depreciation without being an expense). The accountant clears it by correcting the postings or the layout; the *unmapped* list names the accounts that moved but are not on the form (covered prefixes: 214, 229, 131, 133, 136, 138, 141, 244, 15x, 142, 242, 331, 333, 334, 335, 338, the investing and financing accounts 121, 128, 211 … 241, 411 … 421, 911, 341 … 344). |
| BR-102 | Rounding and unit follow BR-80 … BR-83: leaf rows rounded in the unit, totals from rounded rows, the checks on exact amounts. |

*Worked example (VND).* Before the range: cash 5,000 against capital 5,000. In the range: a sale on credit 1,000 (600 collected); stock 500 bought on credit, 200 paid, 300 sold; wages 400 accrued and paid; depreciation 100; interest 50 paid; interest received 20; income tax 40 paid; equipment 800 for cash; a loan 2,000.
Profit before tax = 1,000 + 20 − 300 − 500 (wages 400 + depreciation 100) − 50 = **170**. Add depreciation 100, remove interest income 20, add back interest 50 → **300**. Receivables +400 → −400; inventory +200 → −200; payables +300 → +300; interest paid −50; tax paid −40 → **20 = −90**.
Direct method: received from customers 600, paid to suppliers 200, wages 400, interest 50, tax 40 → 600 − 200 − 400 − 50 − 40 = **−90**. They agree. Investing −800 + 20 = −780, financing +2,000, net change 1,130, closing cash 5,000 + 1,130 = 6,130.

### 6.12 PDF export (P1-2f)
| ID | Rule |
|---|---|
| BR-87 | The PDF is built from the **statement data** (the same result as the JSON answer), never from a copy of the books, so it cannot disagree with the screen, the CSV or the Excel. It writes nothing to the books. |
| BR-88 | Page: A4 portrait, margins 2 cm left / 1.5 cm right / 1.8 cm top and bottom. Order: company block (name, "Mã số thuế", "Địa chỉ" — only the filled lines), centred title, the line "Bản nháp — kế toán cần rà soát trước khi nộp" while the layout is a draft, the period line (B01: "Tại ngày …"; B02 / B03: "Từ … đến …") with "Đơn vị tính: đồng / nghìn đồng / triệu đồng", the table, the balanced check (B01 / B03), unmapped lines and notes in small print, the signature block, the footer "Trang x/y". |
| BR-89 | Table columns: "Mã số" · "Chỉ tiêu" · "Số cuối kỳ" · "Số kỳ trước" (the last only when a comparison was asked for). Rows are indented by level, total rows are bold, the header row repeats on every page. |
| BR-90 | Number format: dot as thousands separator, negatives in parentheses, no decimals when the amount is whole (always true in thousand / million); an exact-VND amount with a fraction shows a decimal comma and two digits. |
| BR-91 | The signature block (Người lập biểu / Kế toán trưởng / Giám đốc, with the names below) is printed only if at least one signatory is filled in the report profile, and is kept together on one page. |
| BR-92 | The font (Noto Sans, SIL Open Font License) is embedded in the application and in the PDF; the licence text is shipped with it. There is no digital signature and the file is not PDF/A. |
| BR-93 | PDF needs the same permission as the other exports (`accounting:reports:view`); only B01, B02 and B03 have it (BR-79 stays true for CSV and Excel). |

### 6.11 Export
| ID | Rule |
|---|---|
| BR-79 | `format=csv` and `format=xlsx` are built from the same table of cells, so a column added to a report shows in both. Amounts are numbers in Excel (`#,##0.00`, negatives red) and plain invariant numbers in CSV; any other `format` value returns the normal JSON. Text cells starting with `=`, `+`, `-` or `@` are stored as text with Excel's quote prefix. |

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
*Alternative:* under the YearEnd policy only the locking happens until the last period of the year (UC-12). *Post:* no entry can be posted into the period; the profit and loss still shows its result; the balance sheet shows the result in retained earnings.

### UC-07 Reopen a fiscal period
*Main flow:* Reopen → reason → the closing entry is voided and the period is open. *Refused:* a later period is closed; the period is not closed. After corrections the period is closed again (a new closing entry).

### UC-08 Produce the books and statements
*Actor:* accountant / auditor. *Main flow:* Reports → choose the tab (trial balance, ledger, profit and loss, balance sheet, cash flow, B01, B02, **B03**) → dates (and a prior period) → read → Export CSV or **Export Excel**.
*Alternatives:* a tenant on another chart opens B01 / B02 → every account is listed as unmapped with a note; no cash account flagged → the cash-flow tab says to flag cash and bank accounts.

### UC-09 Handle a posting problem
*Trigger:* a document was saved but its entry is missing (log: "No journal entry was posted for … : reason"). *Flow:* the accountant reads the reason (key without an account, inactive or parent account, no open period today), fixes the posting profile or opens the period, and books the missing entry by hand (UC-04). *Note:* there is no automatic back-posting (section 15).

### UC-10 Produce the cash-flow statement B03-DN
*Actor:* accountant / auditor (`accounting:reports:view`). *Pre:* the cash and bank accounts are flagged *cash* (accounts screen); the tenant is on the Vietnamese chart.
*Main flow:* 1. Reports → **B03** → enter *from* and *to* → 2. (optional) tick *Compare with a prior period* and enter its range → 3. the table shows code, indicator, amount and prior amount; totals are bold → 4. check the line "closing cash agrees with the books" → 5. review the notes (draft, outflows negative) → 6. Export CSV / Excel.
*Alternative flows:* A1 — the statement is not balanced: read the *unmapped* list (movements whose counter-account no row takes), correct the account's cash-flow category or ask for the layout to be extended. A2 — no account flagged as cash: all rows are 0 and a note says to flag the accounts.
*Exceptions:* from after to → "The start date is after the end date" (400); a prior range with only one date → "A comparison needs both its start and its end date" (400); no permission → 403.
*Post:* nothing is written to the books.

### UC-11 Configure the closing policy
*Actor:* accountant (`accounting:periods:manage`; view needs `accounting:periods:view`). *Pre:* none.
*Main flow:* Fiscal Periods → *Closing policy* panel → choose **Monthly** or **Year end** and the month the fiscal year ends → Save → the panel shows the saved values and a hint what the mode does.
*Exceptions:* the most recent closed period is in the middle of a year → refused: "The fiscal year is in progress … change the closing mode after the year is closed, or reopen the closed periods first" (BR-75). Unknown mode / month outside 1 … 12 → refused. *Post:* later closings follow the policy; entries already written are untouched.

### UC-12 Close the fiscal year (YearEnd policy)
*Actor:* accountant. *Pre:* policy YearEnd; periods of the year exist (e.g. one per month); each earlier period is closed or is being closed in order.
*Main flow:* 1. Close January … November one after another: each is checked and locked, **no entry** → 2. close the last period (December) → the system runs the checks, reads the cumulative balance of every revenue and expense account up to 31 December, writes **one** entry (revenue Dr, expenses Cr, difference through the income-summary account to retained earnings), locks the period → 3. produce B01 / B02 for the year → 4. open the next year's periods and start posting.
*Alternative flows:* A1 — no revenue or expense balance remains: no entry, the period only locks. A2 — a correction is needed: reopen December (reason) → the year-end entry is voided → correct → close again. A3 — "close without closing entries": locks only.
*Exceptions:* an earlier period is open, there are draft entries or the trial balance does not balance → refused with the reason (BR-40); the posting keys *income summary* / *retained earnings* are not usable → refused with the reason.
*Post:* B01 of 31 December shows the year's result in retained earnings (row 421b) with 270 = 440; the trial balance of 1 January opens revenue / expense accounts at 0 (BR-77).

### UC-14 Set the report profile (header, signatories, unit)
*Actor:* accountant / chief accountant (`accounting:accounts:manage`; reading needs `accounting:reports:view`). *Pre:* none.
*Main flow:* 1. Reports → B01, B02 or B03 → open *Report profile* → 2. enter company name, tax code, address, người lập biểu, kế toán trưởng, giám đốc and the default unit → 3. Save → 4. the statements now show the company block, the unit and the signatories, and the exports carry them.
*Alternative flows:* A1 — pick another unit for one look without changing the profile: use the *Unit* selector of the tab (it sends `unit`). A2 — leave texts empty: those lines are not printed.
*Exceptions:* a text above its limit or an unknown unit → refused with the reason (400); no permission → 403 (the fields are read-only on screen). *Post:* only the presentation of later reports changes; no amount in the books changes.

### UC-18 Prepare and read the notes B09-DN
*Actor:* accountant / chief accountant (read: `accounting:reports:view`; texts: `accounting:accounts:manage`). *Pre:* the report profile is filled in (company block, signatories, unit); loans are registered; off-balance movements are recorded.
*Main flow:* 1. Reports → **B09** → open "Edit the texts" → write the policies and the other texts → Save → 2. enter the period (from, to) and optionally the unit → 3. read sections I … VIII: the figures are generated, the written parts show your texts and, where empty, a hint → 4. compare V.1, V.2, V.4, V.7 with B01 (they agree) → 5. Export **PDF** (filing pack), Excel or CSV.
*Alternative flows:* A0 (P1-2j) — choose the language (vi / en) and a comparison range: see UC-25. A1 — a tenant on the 14-account chart: only the tables its chart has are filled; a note says so. A2 — a table with no figures: the PDF prints "(Không phát sinh số liệu trong kỳ)". A3 — another unit: choose it in the selector; amounts are rounded row by row.
*Exceptions:* from after to → 400; unknown unit → 400; a text over 4,000 characters or an unknown key → 400; no permission → 403 (the text fields are read-only).
*Post:* nothing is written to the books; only the texts are saved.

### UC-19 Record an off-balance movement
*Actor:* accountant (`accounting:journals:manage`). *Pre:* the tenant has an off-balance account (001 – 008): they come with the Vietnamese chart; a tenant that already has entries creates the ones it needs with account type OffBalance.
*Main flow:* **Off-balance register** → choose the account, direction In / Out, amount, date, description, reference → Add → the entry appears in the list; the report tab shows the account's balance.
*Alternative flows:* A1 — a wrong entry: Void with a reason, enter the correct one. A2 — read the position: Reports → Off-balance (class 0) with the range.
*Exceptions:* a non-off-balance account, an amount ≤ 0, an unknown direction → 400; the date in a closed fiscal period → 400; voiding twice or without a reason → 400.
*Post:* no journal entry, no change to any financial statement; the balances appear in the notes (V.12).

### UC-20 Register a loan and record its repayments
*Actor:* accountant (`accounting:bank:manage`; reading `accounting:bank:view`). *Pre:* the loan agreement with its schedule.
*Main flow:* 1. **Loans** → Register a loan: number, lender, start date, principal, rate and the schedule (one line per installment; the screen shows whether the principals add up) → Save → 2. book the drawing as a journal entry (Dr bank / Cr 341) → 3. on each repayment date: book the payment (Dr 341 / Cr bank, plus interest) and press **Mark repaid today** on the installment → 4. read B01 / B09: the borrowings are split by the schedule.
*Alternative flows:* A1 — loan entered by mistake: **Cancel** with a reason while nothing is repaid. A2 — a loan with no schedule: give a maturity date only (API). A3 — the loan is not yet in the ledger: B01 shows at most what 341 holds.
*Exceptions:* duplicate number, schedule not adding up, installment before the start, repaying twice, repaying a cancelled loan, cancelling after a repayment → 400 with the reason.
*Post:* since P1-2j the ledger posts the drawdown and the repayments of a new loan (UC-22); for a loan marked "already booked by hand" nothing is posted (BR-126, BR-137).

### UC-21 Produce the statements for a tenant on the 14-account chart
*Actor:* accountant. *Pre:* the tenant never applied the Vietnamese chart (accounts 1000 … 5200). *Main flow:* Reports → B01 / B02 / B03 (direct or indirect) as in UC-08 / UC-10 / UC-16 → the title shows "biểu đồ 14 tài khoản"; rows are filled; export as usual.
*Alternative:* to get the full forms, apply the Vietnamese chart while the tenant has no journal entries, or add accounts and re-map the posting profile. *Note:* VAT, income tax, interest and depreciation lines do not exist in this chart.

### UC-22 Register a loan whose drawdown and repayments are posted
*Actor:* accountant (`accounting:bank:manage`). *Pre:* the posting profile has a bank account and the loan-payable and interest-expense accounts (defaults are provided).
*Main flow:* 1. **Loans** → Register a loan with its schedule; leave "Post the drawdown" ticked → Save → 2. the loan shows *posted*; the entry Dr bank / Cr 341 exists on the start date → 3. on each repayment date press **Mark repaid today** (or the API with `paidOn`) → the entry Dr 341 principal, Dr 635 interest / Cr bank is created for that day → 4. check the card *Loan ledger vs books*: difference 0.
*Alternative flows:* A1 — the loan was booked by hand: untick the option; or register it, then book nothing here (it is "by hand"). A2 — an older loan with no repayment: use *Post drawdown*. A3 — a wrong loan: **Cancel** with a reason (drawdown reversed). A4 — the repayment amount differs from the schedule: correct the schedule by cancelling and registering again before any repayment, or book the difference by hand.
*Exceptions:* the posting profile is incomplete → the loan is saved as "by hand" and the error is logged; duplicate number, bad schedule, repaying twice, cancelling after a repayment → 400.
*Post:* journal entries `Loan:<id>` and `LoanRepayment:<installment id>`; B01 rows 320 / 338 still follow the schedule.

### UC-23 Check the loan ledger against the books
*Actor:* accountant / chief accountant (`accounting:reports:view`). *Main flow:* Loans → the card *Loan ledger vs books* → pick the date → read ledger, books and difference → investigate a difference with the account ledger of 341: a repayment booked by hand, a loan not posted, an entry on 341 that is not a loan. *Post:* nothing changes.

### UC-24 Map the accounts of an own chart to the statement rows
*Actor:* accountant (`accounting:accounts:manage`). *Pre:* B01 / B02 show accounts as unmapped (since P1-2k the same screen maps B03 direct, B03 indirect and the B09 tables: pick the statement, the row — for B09 the table — and the prefix; the "balances taken" choice is hidden there; the unmapped items of B03 are read on the report).
*Main flow:* 1. Reports → **Account mapping** → pick B01-DN or B02-DN → 2. the list shows the accounts with postings that no row takes; click one to fill its number → 3. pick the row (only rows that add up accounts are offered) and the balances to count → Add → 4. run the statement: the account is counted and 270 = 440.
*Alternative flows:* A1 — a whole range of accounts: use a prefix (`62` takes 621, 622 …). A2 — remove a mapping with × next to it.
*Exceptions:* see FR-96. *Post:* only the presentation of B01 / B02 changes; no amount in the books.

### UC-25 Produce the notes in English with a comparison period
*Actor:* accountant. *Main flow:* Reports → **B09** → choose *English*, the period, optionally the comparison range → write the English texts in "Edit the texts" (one set per language) → read the sections; section VII shows the non-cash table to confirm → export PDF / Excel / CSV. *Alternative:* switch back to Tiếng Việt to see the Vietnamese texts. Since P1-2k the texts are per fiscal year: the *Fiscal year of the texts* field defaults to the year of the period end; texts of an earlier year show greyed with "from 2025" until the year gets its own text. *Exceptions:* a language other than vi / en, or half a comparison range → 400.

### UC-26 Accrue loan interest and repay with the interest actually paid
*Actor:* accountant (`accounting:bank:manage`). *Pre:* posted loans with scheduled interest; the posting profile has InterestExpense and InterestPayable (defaults exist).
*Main flow:* 1. **Loans** → *Accrue interest up to* → pick the month-end date → Accrue → the screen says how much was accrued; the entries Dr 635 / Cr 335 exist → 2. on the repayment date open the loan, type the interest the bank actually charged in *Interest paid* (empty = scheduled) → **Mark repaid today** → the entry clears 335 and books the rest → 3. check the card *Loan ledger vs books* (principal and interest).
*Alternative flows:* A1 — run the accrual again later in the month: only the increase is posted. A2 — a hand-booked loan: nothing is accrued (register it with the drawdown posted to include it). A3 — the receiving account differs per loan: choose it when registering.
*Exceptions:* an incomplete posting profile → the interest is not accrued and the next run catches up; a negative interest paid or a repaid installment → 400; cancelling a loan with accruals → 400.
*Post:* journal entries `LoanAccrual:…` and `LoanRepayment:<installment>`.

### UC-27 Count a loan's interest by days and let the month-end job accrue it
*Actor:* accountant (`accounting:bank:manage`; the policy switch needs the fiscal-period manage permission). *Pre:* the posting profile has InterestExpense and InterestPayable.
*Main flow:* 1. **Loans** → add the loan, rate above 0, *Day count of the interest* = actual/360, schedule principals (interest column may stay 0) → 2. **Fiscal periods → closing policy** → tick *Accrue loan interest automatically at month end* → Save → 3. on the 1st of the next month the job accrues to the last day of the previous month (entry Dr 635 / Cr 335); the Loans screen shows the accrual → 4. on the repayment day open the loan: *Interest paid* shows the interest of the days as placeholder; leave it empty or type the lender's figure → repay.
*Alternative flows:* A1 — accrue by hand any day with the button (only the increase is posted). A2 — switch the policy off: the job does nothing. A3 — an earlier installment was repaid: the next period's interest uses the reduced principal.
*Exceptions:* rate 0 with a day count → 400; the period of the as-of date is closed → the job posts nothing (accrue by hand after reopening, or book it manually); incomplete posting profile → not accrued, next run catches up.
*Post:* `LoanAccrual:…` entries; `LoanRepayment:<installment>`.

### UC-28 Read the history of a note text and write the notes in another language
*Actor:* accountant (`accounting:reports:view` to read the history, `accounting:accounts:manage` to save texts). *Main flow:* 1. **Reports → Notes** → pick the language (vi, en, 日本語, 中文, Français) and the fiscal year → *Edit texts* → write and save → 2. **History** under a section lists the earlier versions with user and time → *Use this text* puts one into the draft → Save (a new revision). 3. The report and its Excel / CSV use the language; PDF is offered for vi, en, fr.
*Exceptions:* unknown language → 400; PDF in ja / zh → 400 (button hidden); unknown section → 400.

### UC-29 Configure VAT and invoice with VAT
*Actor:* accountant (`settings:tax:tenant:manage`, `accounting:invoices:manage`). *Main flow:* 1. **Settings → Tax** → company tax code, default rate 10 %, *prices include tax* → Save → 2. an order arrives (or **Invoices → New**) → the invoice shows net, VAT (rate) and amount; the entry Dr 131 / Cr 5111 / Cr 3331 exists → 3. if the real rate differs, **Adjust VAT** → type the rate → the difference is posted.
*Alternative flows:* A1 — a rate typed when creating by hand. A2 — prices excluding tax: the VAT is added to the order total. *Exceptions:* rate outside 0–100 → 400; cancelled invoice or issued e-invoice → 400; no open fiscal period → the invoice exists without an entry (logged; post it after opening the period).

### UC-30 Record a purchase with input VAT
*Actor:* accountant (`accounting:bills:manage`). *Main flow:* 1. the approved purchase order creates a bill split at the tenant rate → 2. **Bills** shows net, VAT, amount → 3. approve (optionally with the rate on the supplier's invoice) → Dr GRNI net / Dr 1331 / Cr 331 → 4. pay: the gross.
*Exceptions:* rate outside 0–100 → 400.

### UC-31 Produce the VAT listing and the VAT return
*Actor:* accountant (`accounting:reports:view`). *Main flow:* **Reports → VAT** → dates → choose *Listing — sales*, *Listing — purchases* or *Return 01/GTGT* → read the rows, the subtotals per rate and the check against the VAT account → export CSV / Excel (PDF for the return). *Alternative:* a difference against the ledger is explained in the notes (documents cancelled later, entries by hand). *Post:* nothing is posted; the return does not settle the VAT (no Dr 3331 / Cr 133 entry).

### UC-32 Issue and cancel an e-invoice
*Actor:* accountant (`accounting:invoices:manage`). *Pre:* provider, series and company tax code set. *Main flow:* 1. **Invoices → E-invoice** on the row → enter the buyer's name, tax code, address → *Issue e-invoice* → the dialog shows series-number and lookup code → 2. if wrong: *Cancel e-invoice* with a reason → issue a new one.
*Alternative flows:* A1 — the provider fails: the dialog shows the error; issue again. A2 — a consumer: leave the buyer empty ("Retail customer"). *Exceptions:* already issued → 400; invoice cancelled → 400; no provider → 400. *Post:* none; the VAT of the invoice is locked while the e-invoice is issued.

### UC-17 Read the balance sheet B01 with the maturity split
*Actor:* accountant / chief accountant (`accounting:reports:view`). *Pre:* open invoices carry due dates (they do: every invoice and bill is created with one); the tenant is on the Vietnamese chart.
*Main flow:* 1. Reports → **B01** → date (and optional prior date, unit) → 2. read "III. Short-term receivables" (row 131) and "I. Long-term receivables" (row 211), "I. Short-term liabilities" (row 311) and "II. Long-term liabilities" (row 331) → 3. check "balanced" (270 = 440) → 4. read the notes → 5. export.
*Alternative flows:* A1 — a receivable that should be long-term does not move: it is a hand-written entry without an invoice, or the invoice's due date is within 12 months, or the ledger balance is smaller (BR-105); fix the due date on the invoice or book the amount to the long-term account. A2 — past date: the approximation note is shown (BR-107); for an audit date, check the invoices by hand. A3 — a tenant with no invoices or bills: nothing moves and the statement is as before.
*Exceptions:* invalid dates, unknown unit → refused (400); no permission → 403. *Post:* nothing is written to the books.

### UC-16 Produce the cash-flow statement by the indirect method
*Actor:* accountant / chief accountant (`accounting:reports:view`). *Pre:* cash and bank accounts are flagged *cash*; the tenant is on the Vietnamese chart.
*Main flow:* 1. Reports → **B03** → choose *Method: Indirect*, the dates, optional prior period and unit → 2. read: profit before tax, adjustments (02 … 06), working-capital changes (09 … 12), interest and tax paid, total 20, then investing, financing, opening and closing cash → 3. check the line "balanced" → 4. Export CSV / Excel / PDF.
*Alternative flows:* A1 — the statement says it is not balanced: read the note (difference between the methods) and the *unmapped* list; typical causes are equipment or stock bought on credit (non-cash) and accounts outside the form; switch to *Direct* to see the cash movements behind it; correct and re-run. A2 — profile with header and signatories: printed as on the direct form (UC-14, UC-15).
*Exceptions:* unknown method / invalid dates / one-sided comparison → refused (400); no permission → 403.
*Post:* nothing is written to the books. *Business meaning:* the indirect form answers "why did cash differ from profit?"; the direct form answers "where did the money come from and go?" — they must agree.

### UC-15 Export a statement as PDF
*Actor:* accountant / chief accountant / auditor (`accounting:reports:view`). *Pre:* B01, B02 or B03 shows the wanted period; the report profile is filled in if the company block and signatories are wanted (UC-14).
*Main flow:* 1. Reports → B01 / B02 / B03 → choose date(s), optional prior period and unit → 2. press **Export PDF** → 3. the browser downloads `<report>-<timestamp>.pdf` → 4. open it: company block, title, unit, table, notes, signatories, page numbers → 5. print or archive; the chief accountant reviews the draft rows before filing.
*Alternative flows:* A1 — a prior period is ticked: the PDF gets the extra column. A2 — the unit selector is changed: the PDF is in that unit. A3 — profile empty: no company block and no signature block, the table and notes are still printed.
*Exceptions:* invalid dates, unknown unit or a comparison with one date → the same refusal (400) as the screen; no permission → 403; the screen shows "Export failed". A direct API call with `format=pdf` on another report → 400 (FR-66).
*Post:* nothing is written to the books. *Not covered:* digital signature, PDF/A, landscape or other paper sizes (DEFERRED W1).

### UC-13 Export a report to Excel
*Actor:* any user with `accounting:reports:view`. *Main flow:* on any report tab press **Export Excel** → the browser downloads `<report>-<timestamp>.xlsx` → open it: amounts are numbers (sortable, summable), the header row is frozen.
*Alternative:* **Export CSV** gives the UTF-8 file (BR-79). B01 / B02 / B03 exports are in the selected unit and carry the company block and signatories (BR-85). *Exceptions:* the export fails (no permission, invalid range) → the screen shows "Export failed". *Note:* the ledger export contains every row, not one page.

---

## 8. Data requirements

### 8.1 Main entities
| Entity | Key attributes | Notes |
|---|---|---|
| Account | number (≤ 20, unique per tenant), name, type (Asset, Liability, Equity, Revenue, Expense, **OffBalance**), normal balance (Debit, Credit), parent, level, active, control, **IsCash**, **CashFlowCategory** | VAS class = first digit of the number (derived). |
| PostingAccountMapping | tenant, posting key, account | Unique per (tenant, key). |
| JournalEntry | number, date, fiscal period, status, description, total debit / credit, reference type and id, source key, posted / voided audit fields | Source key unique per tenant. |
| JournalLine | entry, account, debit, credit, description | One positive side. |
| FiscalPeriod | name, start, end, status (Open, Closed) | Months are auto-created. |
| Invoice | number, customer, order, amount, paid amount, status (Unpaid, Paid, Overdue, Cancelled) | `AmountPaid` goes down on refunds. |
| Bill | number, supplier, purchase order, amount, status | Header only (no lines). |
| **ReportProfile** (table `ReportProfiles`, migration `012_ReportProfile.sql`) | tenant (unique), company name (≤ 200), tax code (≤ 20), address, prepared by, chief accountant, director (each ≤ 300), amount unit (1 VND, 2 thousand, 3 million) | One row per tenant; no row = empty texts and VND. |
| **OffBalanceEntry** (table `OffBalanceEntries`, migration `013_OffBalance.sql`) | tenant, off-balance account, entry date, direction (1 In, 2 Out), amount (> 0), description, reference, status (1 Posted, 2 Voided), void reason and time | Register of the class 0 accounts; no journal lines. |
| **Loan** (tables `Loans` and `LoanInstallments`, migration `014_Loans.sql`) | tenant, number (unique per tenant), lender, principal, interest rate %, start date, status (1 Active, 2 Repaid, 3 Cancelled), cancel reason, notes; installments: due date, principal, interest, **repaid on** | The schedule and the repayment dates give the exact outstanding amount for any date. |
| **NoteText** (table `NoteTexts`, migration `015_NoteTexts.sql`) | tenant, section key, text (≤ 4,000) | One row per tenant and key; the figures of the notes are not stored. |
| **Loan additions** (migration `016_LoanPosting.sql`) | `Loans.PostedToLedger` (bit, default 0) | 1 once the drawdown is in the journal. |
| **PaymentApplication** (table `PaymentApplications`, migration `017_PaymentApplications.sql`) | tenant, document type (1 invoice, 2 bill), document id, paid on (date), amount (negative = refund), source key (unique per tenant), estimated flag | Plus `Bills.ApprovedOn` and `Invoices.CancelledOn`. Backfilled (estimated) from the documents. |
| **StatementAccountMapping** (table `StatementAccountMappings`, migration `018_StatementAccountMappings.sql`) | tenant, statement id, row code, account prefix (≤ 30), side (0 net, 1 debit, 2 credit) | Unique per (tenant, statement, row, prefix). |
| **NoteText language** (migration `019_NoteTextLanguage.sql`) | `NoteTexts.Language` ('vi' / 'en', default 'vi') | Unique per (tenant, section, language). |
| **Loan interest and account** (migration `020_LoanInterestAndAccount.sql`) | `Loans.ReceivingAccountId` (nullable), `LoanInstallments.AccruedInterest` (decimal, default 0) | Accrued interest is cleared on display once the installment is repaid. |
| **VAT and e-invoice** (migration `023_VatAndEInvoice.sql`) | `Invoices` / `Bills`: `TaxRatePercent` (new), `SubTotal` = net and `TaxAmount` (since 001), `TotalAmount` = gross; `TenantTaxSettings.EInvoiceProvider`, `EInvoiceSeries`; table `EInvoices` (invoice, provider, status 1 Issued / 2 Cancelled / 3 Failed, series, number, lookup code, buyer, payload JSON, error, cancel reason) | Unique (tenant, invoice) while not cancelled. |
| **Loan day count / auto accrual / revisions** (migration `022_LoanDayCountPolicyRevisions.sql`) | `Loans.DayCountBasis` (int 0–2, default 0); `ClosingPolicies.AutoAccrueLoanInterest` (bit, default 0); `NoteTextRevisions` (tenant, section key, language, fiscal year, text ≤ 4000, CreatedBy, CreatedAt) | Append-only history; index (tenant, key, language, year, time). |
| **NoteText year** (migration `021_NoteTextFiscalYear.sql`) | `NoteTexts.FiscalYear` (int, 0 = every year) | Unique per (tenant, section, language, year). |
| **ClosingPolicy** (table `ClosingPolicies`, migration `011_ClosingPolicy.sql`) | tenant (unique), closing mode (1 Monthly, 2 YearEnd), fiscal-year end month (1 … 12) | One row per tenant; no row = Monthly / December. Checked by database constraints. |

### 8.2 Posting keys and default accounts
`Cash, Bank, AccountsReceivable, AccountsPayable, SalesRevenue, Inventory, VatOutput, GoodsReceivedNotInvoiced, CostOfGoodsSold, InventoryAdjustment, CashOverShort, SalaryExpense, SalariesPayable,
PayrollWithholdingsPayable, SalesReturns, IncomeSummary, RetainedEarnings, LoanPayable, InterestExpense, InterestPayable` (the last three since P1-2j / P1-2k). The defaults per template are listed in AutoPosting.md (14-account chart) and ChartOfAccounts.md (Vietnamese chart).

### 8.3 State models
- **Fiscal period:** Open → (close) → Closed → (reopen, latest only) → Open. Whether *close* writes an entry depends on the policy: Monthly — always; YearEnd — only the last period of the year.
- **Loan:** Active → Repaid (all installments repaid) | Cancelled (only without repayments; a posted loan's drawdown is then reversed). **Posted flag:** by hand → posted (drawdown posted at registration or later, only before any repayment). **Installment:** outstanding → repaid on a date. **Off-balance entry:** Posted → Voided.
- **Closing policy:** (none) = Monthly/December → set (Monthly ⇄ YearEnd, any month) — a change is allowed only when no year is partly closed (BR-75).
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
| B01-DN (`statements/balance-sheet`) | as-of date, optional prior date | Indicator code, name, amount, prior amount; unmapped; balanced flag; trade receivables / payables split by due date (rows 131 / 211, 311 / 331) | 270 = 440 |
| B02-DN (`statements/income-statement`) | dates, optional prior range | Same | 60 = 50 − 51 − 52 |
| **B03-DN** (`statements/cash-flow`) | dates, optional prior range | Indicator code, name, amount (outflows negative), prior amount; unmapped movements; balanced flag; notes | 70 = closing cash on the books |
| Closing policy (`fiscal-periods/closing-policy`) | — | Mode, fiscal-year end month | — |
| Report profile (`accounting/reports/profile`) | — | Company block, signatories, default unit | — |
| Off-balance register (`accounting/off-balance`, report `reports/off-balance`) | list: account, dates; report: range | Entries; per account opening, In, Out, closing, negative flag | closing = opening + In − Out |
| Loans (`accounting/loans`) | — | Loans with schedule, outstanding, due within 12 months and later | principals add up to the principal |
| **B09-DN** (`reports/statements/notes`, texts `reports/notes/text`) | range, optional unit | Numbered sections with text and / or tables (V.1 … V.12, VI.1, VI.2 from the books) | V.1 = B01 row 110; V.2 = rows 131 + 211; V.4 net = row 220 |
| **Loans vs books** (`reports/loans-reconciliation`) | as-of date | Loans with outstanding and posted flag; ledger total, book balance of the loan-payable account, difference | difference 0 when every loan is posted and nothing was booked by hand |
| **Account mapping** (`accounting/statement-mappings`) | statement id | Rows (mappable or not, standard prefixes, the tenant's mappings) and the unmapped accounts | after mapping, unmapped is empty and 270 = 440 |
The loans check (P1-2k) also returns the accrued interest against the interest-payable account; the account mapping answers for B01-DN, B02-DN, B03-DN, B03-DN-GT and B09-DN; the note texts take `fiscalYear`.
B09 also takes `lang=vi|en` and `compareFromDate` / `compareToDate` (P1-2j); the texts take `lang`.
B01 / B02 / B03 take an optional `unit` (`vnd`, `thousand`, `million`) and answer with `unit`, `unitDivisor` and `header`.
All reports accept `format=csv` and `format=xlsx`.

---

## 11. Security, audit and non-functional requirements

| Area | Requirement |
|---|---|
| **Permissions** | `accounting:accounts:view|manage`, `accounting:journals:view|manage`, `accounting:periods:view|manage`, `accounting:reports:view`, `accounting:invoices:view|manage`, `accounting:bills:view|manage`, `accounting:tax:view|manage`, `accounting:bank:view|manage`. Cross-module actions: `hrm:payroll:manage` (generate / pay / cancel pay slips), `payments:refund`, `sales_order:approve` (complete a return). |
| **Tenant isolation** | Every row belongs to one tenant; consumers drop events without a tenant; the account queries are not cached (a tenant-less cache key once leaked another tenant's chart). |
| **Audit** | Entries keep created / posted / voided timestamps and reasons; reversals and voids keep both entries visible; closing entries are flagged. |
| **Consistency** | The document, its entry and its outbox rows commit together; idempotent consumers; no double posting. |
| **Performance** | The ledger and cash flow read journal lines by account and date through an index; the ledger pages in memory (fine for normal volumes — DEFERRED R2). Excel files are built in memory (a full ledger of very many rows is large — DEFERRED R6). |
| **Localisation** | Admin screens in English and Vietnamese; statutory rows in Vietnamese; CSV is UTF-8 with a byte-order mark; Excel is Unicode. |
| **Safety of exports** | Text that starts with `= + - @` can never run as a formula (CSV apostrophe, Excel quote prefix). |
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
| UAT-14 | Given a tenant with entries, when a template is applied, then it is refused; given a clean tenant, then the template's accounts (now including the six off-balance accounts 001 – 008) and 17 posting keys are created. | FR-23 | Passed |
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
| UAT-25 | Given tenant `glcheck` (Vietnamese chart, October 2026: sales receipt 400, supplier payment 600, equipment −1,500, loan +2,000), when B03 is run for October, then 01 = 400, 02 = −600, 20 = −200, 30 = −1,500, 40 = 2,000, 50 = 300, 60 = 0, 70 = 300, nothing is unmapped, the check says balanced, and 50 equals the net change of the older `cash-flow` report. | FR-48, 50, 51 | Passed |
| UAT-26 | When a prior range (September) is added, then every row also has a prior amount (0 for September). | FR-48 | Passed |
| UAT-27 | Given movements 5111 +600, 131 +300, 331 −300, 334 −200, 635 −50, 8211 −30, then rows 01 = 900, 02 = −300, 03 = −200, 04 = −50, 05 = −30, 20 = 320. | FR-49 | Unit test only |
| UAT-28 | Given 341 +300 and 341 −100, then 33 = 300 and 34 = −100; given 711 +100 and 3388 −30, then 06 = 100 and 07 = −30; investing (211, 128, 515) and financing (411, 421) movements land on 21 … 36. | FR-49 | Unit test only |
| UAT-29 | Given an investing-category account the layout does not name (242), when it moves 50 of cash, then it is listed as unmapped, the check is false and the note "Closing cash (70) differs …" is shown. | FR-50 | Unit test only |
| UAT-30 | When a workbook contains `=1+1`, `+84 9`, `-cmd`, then each cell is text with the quote prefix, not a formula; amounts are numeric cells; the header row is bold and frozen; a sheet name with `: \ / ? * [ ]` is cleaned and cut to 31 characters. | FR-52 | Unit test only (workbook read back) |
| UAT-31 | When B03, B01 and the trial balance are requested with `format=xlsx`, then each answer is an `.xlsx` (zip, content type `…spreadsheetml.sheet`, file name with the report and timestamp) and the Vietnamese row text is intact. | FR-52 | Passed |
| UAT-32 | Given a tenant with no policy, then GET returns Monthly / 12; given October closed (mid-year) then PUT YearEnd is refused with "The fiscal year is in progress …". | FR-53, BR-75 | Passed |
| UAT-33 | When October is reopened (its closing entry becomes Voided), then PUT YearEnd / 12 is accepted. | FR-53 | Passed |
| UAT-34 | Given YearEnd, October (revenue 1,900, expenses 600) and November (revenue 500 posted by hand) are closed in order, then both are Closed and **no** PeriodClosing entry exists; B02 of October still shows 1,900 / 1,300. | FR-54 | Passed |
| UAT-35 | When December is closed, then exactly one Posted PeriodClosing entry exists, dated 31 December, description "Year-end closing December 2026": Dr 5111 2,400, Cr expenses 600, through 911 to Cr 4212 1,800. | FR-55 | Passed |
| UAT-36 | Then B01 at 31 December has 270 = 440 = 3,800 with 1,800 in row 421b, and the January 2027 trial balance opens 5111 and 632 at 0, 4212 at Cr 1,800, 1111 at Dr 400, and balances. | FR-56, BR-77 | Passed |
| UAT-37 | When December is reopened, then the year-end entry is Voided; given a fiscal year ending in June, then only the period ending 30 June writes the entry. | FR-55, BR-74 | Passed (reopen) / unit test (June) |
| UAT-38 | When the mode is `Weekly` or empty, or the month is 0 or 13, then PUT is refused with HTTP 400. | FR-53, BR-71 | Unit test only |
| UAT-40 | Given a tenant with no profile, then GET returns empty texts and Vnd; PUT with company, tax code, address, the three names and unit Thousand returns them and later GETs return the same; an unknown unit is refused (400). | FR-58 | Passed |
| UAT-41 | Given that profile, then B01 at 31 Oct answers `unit` "thousand VND", `unitDivisor` 1000 and the header; `unit=vnd` returns exact VND (cash 300, assets 3,300) and `unit=million` rounds to millions. | FR-59 | Passed |
| UAT-42 | When `unit=foo` is requested, then the answer is 400 "The amount unit must be Vnd, Thousand or Million."; with no profile and no `unit` the statement is plain VND with no header. | FR-59, BR-80 | Passed |
| UAT-43 | When B01 rows are rounded in thousands, then the form shows assets 4 and sources 3 for the same exact 3,300 VND with a rounding-difference note and `isBalanced` true; the unit-test example (1 + 1 against 3) and half-away-from-zero cases (1,500 → 2, −1,500 → −2) pass; a really unbalanced sheet stays unbalanced. | FR-60 | Passed (live) / unit tests |
| UAT-44 | When a user without `accounting:accounts:manage` saves the profile, then 403; text above the limits is refused (400). | FR-58, BR-86 | Unit test only (limits); 403 by code path |
| UAT-45 | When B01 is exported as CSV, then the file starts with the BOM, the company name, `Tax code,0312345678`, `Address,"1 Lê Lợi, Quận 1"`, the title, a period line ending `Unit,thousand VND`, and ends with the signatory titles and names; the xlsx download succeeds. | FR-61 | Passed |
| UAT-39 | Given November is closed mid-year, then switching back to Monthly is refused; saving the same values is accepted. | BR-75 | Passed (refusal) / unit test (same values) |

**How QA reproduces UAT-32 … 36** (gateway `/acc/api/v1`, tenant host, token with `accounting:periods:manage` and `accounting:journals:manage`):
`GET /fiscal-periods/closing-policy` → `PUT /fiscal-periods/closing-policy {"closingMode":"YearEnd","fiscalYearEndMonth":12}` → `POST /fiscal-periods` for the months → `POST /journal-entries` (hand-written entry in November, e.g. Dr 1111 / Cr 5111 500)
→ `POST /fiscal-periods/{id}/close` for each month in order → check `JournalEntries` where `ReferenceType='PeriodClosing'` (SQL) → `GET /accounting/reports/trial-balance/full?fromDate=2027-01-01&toDate=2027-01-31&rollUp=false&hideZeroBalance=false`.

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
| View the closing policy | | | view | | |
| Change the closing policy | | | manage | | |
| Any report (incl. B03, both methods) and CSV / Excel export; PDF of B01 / B02 / B03 | | | | view | |
| View the report profile | | | | view | |
| Change the report profile (header, signatories, unit) | manage | | | | |
| Write the texts of the notes B09 | manage | | | | |
| View off-balance register, loans | | view (register) | | | `accounting:bank:view` (loans) |
| Record / void off-balance entries | | manage | | | |
| Register / repay / cancel a loan; post the drawdown of an older loan | | | | | `accounting:bank:manage` |
| Loans vs books check | | | | view | |
| Accrue loan interest | | | | | `accounting:bank:manage` |
| View the account mapping of B01 / B02 | view | | | | |
| Change the account mapping | manage | | | | |
| Pay / cancel a pay slip | | | | | `hrm:payroll:manage` |
| Refund a payment | | | | | `payments:refund` |
| Complete a return | | | | | `sales_order:approve` |

---

## 14. Assumptions and constraints

1. One currency (VND) and one legal entity per tenant.
2. The Vietnamese chart and the statement layouts are **suggested drafts** based on Circular 200's numbering (largely kept by Circular 99/2025/TT-BTC); the accountant reviews them before real use.
3. The SYSTEM tenant and the demo data stay on the 14-account chart.
4. Fiscal periods are normally months and are opened by the accountant (or automatically for posting); closing is monthly unless the tenant chose YearEnd. The fiscal year is the twelve months ending in the policy's year-end month; periods of one year are assumed to be contiguous.
4f. The 14-account layouts are a simplification (no VAT, income tax, interest, depreciation or foreign-currency accounts). Off-balance accounts exist only in the Vietnamese template: a tenant that already has entries (so cannot apply a template) creates the accounts it needs with type OffBalance. Loans are information for the statements: the platform does not post drawing, repayment or interest from them. The notes' tables follow the Circular 200 form and the account prefixes of each chart; the accountant reviews wording and structure before filing.
4e. Due dates are those on the invoice or bill; the order's payment terms and instalment plans are not modelled. Trade items only (131, 331): receivables from other parties (138, 141, 244) keep the account-based classification; the 12-month threshold is fixed.
4d. The indirect statement relies on the account prefixes of the Vietnamese chart (BR-101); balance changes are measured between the day before the range and its end; depreciation and provisions are measured by the change of their balance sheet accounts, not by the expense; non-cash transactions are not separated (no "non-cash investing and financing" note — part of B09).
4c. The PDF is A4 portrait with one embedded font (Noto Sans, OFL); the statements are the only reports with a PDF; no digital signature, no PDF/A; the file is generated on demand and not stored.
4b. Rounding is half away from zero per statement row and totals follow the rounded rows (the common practice on filed forms); the profile is edited with the chart-level permission `accounting:accounts:manage` because no dedicated permission exists yet; one profile per tenant (one legal entity).
4a. The B03 row mapping (6.8) is a draft by account prefix; salary payments are recognised by the payroll-payable account (334), so a salary paid straight from expense accounts lands in row 02, and withholdings paid to the authorities (3388, 3382 … 3384) land in row 07.
4g. (P1-2j) Loan drawdowns go to the bank account of the posting profile and interest is booked when paid, from the scheduled amounts; the loan-payable key of the 14-account chart shares account 2100 with accrued liabilities. Payment dates are exact only for payments recorded after migration 017; older ones are estimated. The account mapping covers B01 / B02 only. Non-cash transactions are detected by account patterns and must be confirmed by the accountant. Notes exist in Vietnamese and English only.
4h. (P1-2k) Interest accrues by calendar days of each installment's period from the scheduled interest, on the date the accountant chooses (no automatic monthly job); the repayment posts the scheduled principal and the interest typed. The receiving account of a loan is fixed once posted. Note texts are versioned per fiscal year (not per edit). B03 and notes mapping ignore the balance side.
4i. (P1-2l) The month-end job is opt-in per tenant, runs once per day of service uptime and needs the service running on the 1st (a restart re-runs it; it never double-posts). Actual/360 and actual/365 use one rate for the life of the loan (no rate changes) and the day-by-day principal; interest paid is an amount, not recomputed from bank statements. Revisions record the user name (or id) at the time and are never edited. ja / zh / fr wording is a draft translation; ja / zh have no PDF.
4j. (P1-3) VAT is derived in Accounting from the tenant's settings because Sales and Purchasing send no tax; one rate per document (no per-line rates or exempt/non-taxable lines); a return uses the invoice's rate; the VAT return is a simplified draft and does not settle the VAT or produce the tax authority's file; the e-invoice is declared to a provider, never to the tax authority, and the sandbox provider stands in for a real one; adjustment and replacement e-invoices, the authority's code and signing are later work.
5. Warehouse owns costing; Accounting never computes a cost.
6. A tenant that already has journal entries cannot change its chart template.

---

## 15. Out of scope and open issues

Full list with owners and impact: [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md). The business-relevant ones:
VAT split and e-invoice (P1-3); foreign currency and bank reconciliation (P1-4); restocking of returned goods and reversing their cost, and triggering the refund from a return (P1-5); real payroll with insurance, personal income tax and employer
contributions (P1-7); an automatic monthly accrual job, actual/360 interest and rate changes, history of edits of the note texts, notes in further languages (follow-ups of P1-2k); PDF of the other reports, digital signature / PDF-A, other paper sizes (DEFERRED W1);
no automatic back-posting of documents that were saved without an entry; accountant review of the Vietnamese chart and the B01 / B02 / B03 layouts.
Open questions for the chief accountant: (1) is the B03 row mapping right for the company's usual transactions (VAT paid, withholdings, deposits)? (2) does the company want the fiscal year to end in December? (3) should a year-end preview
(what would be closed) be shown before closing December?

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
| P1-2i 14-account layouts, off-balance, loans, B09 | 2026-10-25 | FR-75 … 85; BR-109 … 133; UC-18 … 21; UAT-62 … 74 | Reports.md, ChartOfAccounts.md | `Simple14LayoutTests`, `OffBalanceTests`, `LoanLedgerTests`, `NotesTests`, `LiveSchemaDriftTests` (migrations 013 – 015) |
| P1-2j loan posting, payment history, own-chart mapping, notes in English | 2026-10-25 | FR-86 … 101; BR-134 … 155; UC-22 … 25; UAT-75 … 91 | Reports.md, AutoPosting.md, ChartOfAccounts.md | `LoanPostingTests`, `MaturitySplitTests`, `StatementMappingTests`, `NotesTests`, `AutoPostingFlowTests`, `RefundPostingTests`, `LiveSchemaDriftTests` (migrations 016 – 019) |
| P1-2k loan receiving account and accrued interest, B03 / notes mapping, versioned note texts | 2026-10-25 | FR-102 … 111; BR-156 … 167; UC-26 (and UC-24 / 25 updated); UAT-92 … 101 | Reports.md, AutoPosting.md, ChartOfAccounts.md | `LoanInterestTests`, `StatementMappingB03Tests`, `NotesTests`, `LoanPostingTests`, `LiveSchemaDriftTests` (migrations 020 – 021) |
| P1-2l day count (actual/365, actual/360), opt-in month-end accrual job, history of note texts, notes in ja / zh / fr | 2026-10-25 | FR-112 … 122; BR-168 … 175; UC-27, UC-28; UAT-102 … 112 | Reports.md, AutoPosting.md | `LoanDayCountTests`, `LoanAccrualJobTests`, `NoteTextHistoryTests`, `NotesTests` (languages), `LiveSchemaDriftTests` (migration 022) |
| P1-3 VAT on invoices, bills and returns, VAT listing and return 01/GTGT, e-invoice adapter with a sandbox provider | 2026-10-25 | FR-123 … 134; BR-176 … 188; UC-29 … 32; UAT-113 … 125 | Reports.md, AutoPosting.md, ChartOfAccounts.md | `VatSalesTests`, `VatPurchaseTests`, `VatReportTests`, `EInvoiceTests`, `LiveSchemaDriftTests` (migration 023) |
| P1-2h maturity split on B01 | 2026-10-25 | FR-71 … 74; BR-103 … 108; UC-17; UAT-56 … 61 | Reports.md | `MaturitySplitTests` (12 tests) + live scenario on `glcheck` |
| P1-2g B03 indirect method | 2026-10-25 | FR-67 … 70; BR-94 … 102; UC-16; UAT-51 … 55 | Reports.md | `IndirectCashFlowTests` (16 tests incl. the direct-vs-indirect reconciliation) |
| P1-2f PDF export of B01 / B02 / B03 | 2026-10-25 | FR-63 … 66; BR-87 … 93; UC-15; UAT-46 … 50 | Reports.md | `PdfWriterTests` (11 tests) + live render check |
| P1-2e amount unit, company header | 2026-10-24 | FR-58 … 62; BR-80 … 86; UC-14; UAT-40 … 45 | Reports.md | `RoundingTests`, `ReportProfileTests`, `XlsxWriterTests`, `LiveSchemaDriftTests` (migration 012) |
| P1-2d B03, year-end closing policy, Excel | 2026-10-24 | FR-48 … 57; BR-64 … 79; UC-10 … 13; UAT-25 … 39 | Reports.md, ChartOfAccounts.md | `CashFlowStatementTests`, `XlsxWriterTests`, `YearEndClosingTests`, `PeriodClosingTests`, `LiveSchemaDriftTests` (migration 011) |
