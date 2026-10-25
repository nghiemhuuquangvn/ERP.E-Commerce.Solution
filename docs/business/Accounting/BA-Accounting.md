# Accounting — Business Analysis (general ledger, automatic posting, closing, reports)

| | |
|---|---|
| **Domain** | Accounting (AccountingService, port 5005, code ACC) |
| **Covers** | ROADMAP P1-1 … P1-1e (automatic posting), P1-2 (chart of accounts, void / reverse, closing), P1-2b (ledger, trial balance, cash flow), P1-2c (B01 / B02 statements, CSV), P1-2d (B03 cash-flow statement by indicator code, year-end closing policy, Excel export), P1-2e (amount unit VND / thousand / million and company header on the statements), **P1-2f (PDF export of B01 / B02 / B03)** |
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

### 1.3 Scope
**In scope:** posting from sales, payment, purchasing, warehouse, POS, payroll, refunds and returns; chart of accounts and templates; manual entries, void, reverse; fiscal periods and closing;
trial balance, ledger, profit and loss, balance sheet, cash flow, B01 / B02 / **B03** by indicator code; CSV and **Excel** export; **a closing policy (monthly or at year end)**; admin screens for all of it.
**Out of scope (see section 15):** VAT, e-invoice, foreign currency, bank reconciliation, real payroll calculation (insurance, personal income tax), restocking of returned goods, B09 notes, indirect-method cash flow,
off-balance (class 0) accounts, short- / long-term split by due date, PDF of reports other than B01 / B02 / B03. Rounding and the header apply to B01 / B02 / B03 only; the trial balance and the ledger stay exact in VND.

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
| UAT-45 |
| FR-62 | Given a manager, then the profile panel saves; given a viewer, then its fields are read-only. | manual |
| FR-63 | Given a tenant with a filled report profile, when B01 for 31 Oct is requested with `format=pdf`, then the answer is HTTP 200, `application/pdf`, file `balance-sheet-b01-<timestamp>.pdf`, the bytes start with `%PDF-`, and the file has the company block, the title, "Tại ngày 31/10/2026 — Đơn vị tính: nghìn đồng", the rows with codes, and the three signatories; B01 (≈ 100 rows) runs over two pages with the table header repeated and "Trang 1/2" in the footer; B02 and B03 each fit one page. | UAT-46, 47 |
| FR-64 | Given the same request as CSV, then every code and amount in the PDF equals the CSV (same rounding in the same unit); a comparison range adds the column "Số kỳ trước"; unmapped accounts and notes are listed in small print; negatives are in parentheses; thousands are separated by a dot (1.234.567). | UAT-48 |
| FR-65 | Given a Linux container without fonts, then the PDF still shows "Bảng cân đối kế toán", "Kế toán trưởng", "Nguyễn Văn A" with all diacritics, and the file's fonts are embedded Noto Sans (regular and bold). | UAT-47 |
| FR-66 | When `format=pdf` is requested on `trial-balance/full`, `account-ledger` or `cash-flow`, then HTTP 400 "PDF is available for the statements B01, B02 and B03 only; use csv or xlsx for this report."; the Export PDF button exists on the B01, B02 and B03 tabs only. | UAT-49 |

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
| Account | number (≤ 20, unique per tenant), name, type (Asset, Liability, Equity, Revenue, Expense), normal balance (Debit, Credit), parent, level, active, control, **IsCash**, **CashFlowCategory** | VAS class = first digit of the number (derived). |
| PostingAccountMapping | tenant, posting key, account | Unique per (tenant, key). |
| JournalEntry | number, date, fiscal period, status, description, total debit / credit, reference type and id, source key, posted / voided audit fields | Source key unique per tenant. |
| JournalLine | entry, account, debit, credit, description | One positive side. |
| FiscalPeriod | name, start, end, status (Open, Closed) | Months are auto-created. |
| Invoice | number, customer, order, amount, paid amount, status (Unpaid, Paid, Overdue, Cancelled) | `AmountPaid` goes down on refunds. |
| Bill | number, supplier, purchase order, amount, status | Header only (no lines). |
| **ReportProfile** (table `ReportProfiles`, migration `012_ReportProfile.sql`) | tenant (unique), company name (≤ 200), tax code (≤ 20), address, prepared by, chief accountant, director (each ≤ 300), amount unit (1 VND, 2 thousand, 3 million) | One row per tenant; no row = empty texts and VND. |
| **ClosingPolicy** (table `ClosingPolicies`, migration `011_ClosingPolicy.sql`) | tenant (unique), closing mode (1 Monthly, 2 YearEnd), fiscal-year end month (1 … 12) | One row per tenant; no row = Monthly / December. Checked by database constraints. |

### 8.2 Posting keys and default accounts
`Cash, Bank, AccountsReceivable, AccountsPayable, SalesRevenue, Inventory, VatOutput, GoodsReceivedNotInvoiced, CostOfGoodsSold, InventoryAdjustment, CashOverShort, SalaryExpense, SalariesPayable,
PayrollWithholdingsPayable, SalesReturns, IncomeSummary, RetainedEarnings`. The defaults per template are listed in AutoPosting.md (14-account chart) and ChartOfAccounts.md (Vietnamese chart).

### 8.3 State models
- **Fiscal period:** Open → (close) → Closed → (reopen, latest only) → Open. Whether *close* writes an entry depends on the policy: Monthly — always; YearEnd — only the last period of the year.
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
| B01-DN (`statements/balance-sheet`) | as-of date, optional prior date | Indicator code, name, amount, prior amount; unmapped; balanced flag | 270 = 440 |
| B02-DN (`statements/income-statement`) | dates, optional prior range | Same | 60 = 50 − 51 − 52 |
| **B03-DN** (`statements/cash-flow`) | dates, optional prior range | Indicator code, name, amount (outflows negative), prior amount; unmapped movements; balanced flag; notes | 70 = closing cash on the books |
| Closing policy (`fiscal-periods/closing-policy`) | — | Mode, fiscal-year end month | — |
| Report profile (`accounting/reports/profile`) | — | Company block, signatories, default unit | — |
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
| Any report (incl. B03) and CSV / Excel export; PDF of B01 / B02 / B03 | | | | view | |
| View the report profile | | | | view | |
| Change the report profile (header, signatories, unit) | manage | | | | |
| Pay / cancel a pay slip | | | | | `hrm:payroll:manage` |
| Refund a payment | | | | | `payments:refund` |
| Complete a return | | | | | `sales_order:approve` |

---

## 14. Assumptions and constraints

1. One currency (VND) and one legal entity per tenant.
2. The Vietnamese chart and the statement layouts are **suggested drafts** based on Circular 200's numbering (largely kept by Circular 99/2025/TT-BTC); the accountant reviews them before real use.
3. The SYSTEM tenant and the demo data stay on the 14-account chart.
4. Fiscal periods are normally months and are opened by the accountant (or automatically for posting); closing is monthly unless the tenant chose YearEnd. The fiscal year is the twelve months ending in the policy's year-end month; periods of one year are assumed to be contiguous.
4c. The PDF is A4 portrait with one embedded font (Noto Sans, OFL); the statements are the only reports with a PDF; no digital signature, no PDF/A; the file is generated on demand and not stored.
4b. Rounding is half away from zero per statement row and totals follow the rounded rows (the common practice on filed forms); the profile is edited with the chart-level permission `accounting:accounts:manage` because no dedicated permission exists yet; one profile per tenant (one legal entity).
4a. The B03 row mapping (6.8) is a draft by account prefix; salary payments are recognised by the payroll-payable account (334), so a salary paid straight from expense accounts lands in row 02, and withholdings paid to the authorities (3388, 3382 … 3384) land in row 07.
5. Warehouse owns costing; Accounting never computes a cost.
6. A tenant that already has journal entries cannot change its chart template.

---

## 15. Out of scope and open issues

Full list with owners and impact: [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md). The business-relevant ones:
VAT split and e-invoice (P1-3); foreign currency and bank reconciliation (P1-4); restocking of returned goods and reversing their cost, and triggering the refund from a return (P1-5); real payroll with insurance, personal income tax and employer
contributions (P1-7); indirect-method cash flow, B09 notes, off-balance (class 0) accounts, short- / long-term split by due date, layouts for charts other than `Vas99` (P1-2g); PDF of the other reports, digital signature / PDF-A, other paper sizes (DEFERRED W1);
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
| P1-2f PDF export of B01 / B02 / B03 | 2026-10-25 | FR-63 … 66; BR-87 … 93; UC-15; UAT-46 … 50 | Reports.md | `PdfWriterTests` (11 tests) + live render check |
| P1-2e amount unit, company header | 2026-10-24 | FR-58 … 62; BR-80 … 86; UC-14; UAT-40 … 45 | Reports.md | `RoundingTests`, `ReportProfileTests`, `XlsxWriterTests`, `LiveSchemaDriftTests` (migration 012) |
| P1-2d B03, year-end closing policy, Excel | 2026-10-24 | FR-48 … 57; BR-64 … 79; UC-10 … 13; UAT-25 … 39 | Reports.md, ChartOfAccounts.md | `CashFlowStatementTests`, `XlsxWriterTests`, `YearEndClosingTests`, `PeriodClosingTests`, `LiveSchemaDriftTests` (migration 011) |
