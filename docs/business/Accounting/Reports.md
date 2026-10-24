# Accounting reports

Status: ROADMAP P1-2b, P1-2c and P1-2d (2026-10-24). Endpoints are under `api/v1/accounting/reports/…` (permission `accounting:reports:view`); the admin app has a tab for each.
Every report counts **Posted** and **Reversed** entries only (a reversed entry is offset by its reversal entry); Draft and Voided entries never count.

| Report | Route | What it shows |
|---|---|---|
| Trial balance | `trial-balance` (list), `trial-balance/full` (with totals) | Opening, period and closing balance per account |
| Account ledger (sổ cái / sổ chi tiết) | `account-ledger` | Every journal line of an account with counter-accounts and a running balance |
| Profit and loss | `profit-and-loss` | Revenue and expenses of a range (closing entries left out) |
| Balance sheet | `balance-sheet` | Assets, liabilities, equity as of a date (adds the not-yet-closed result to equity) |
| Cash flow | `cash-flow` | Direct method, by counter-account, with opening / closing cash |
| **B01-DN** balance sheet by indicator code | `statements/balance-sheet` | Vietnamese form, prior-period column, CSV (draft layout) |
| **B02-DN** income statement by indicator code | `statements/income-statement` | Vietnamese form, prior-period column, CSV (draft layout) |
| **B03-DN** cash-flow statement by indicator code | `statements/cash-flow` | Direct method, outflows negative, prior-period column, CSV / Excel (draft layout) |

## Trial balance (bảng cân đối số phát sinh)

`GET trial-balance/full?fromDate&toDate&fiscalPeriodId&hideZeroBalance=true&rollUp=true`

* **Opening** = everything dated before `fromDate` (no `fromDate` → no opening); **period** = the range; **closing** = opening + period. Opening and closing are shown as a debit
  *or* a credit amount (the net of the account), the period as its debit and credit movement. A fiscal period id stands for its start and end dates.
* `rollUp` (default) makes each parent carry the sum of its sub-accounts (`isParent`, `level` tell the screen how to indent). The grand **totals** add only the accounts that are not
  rolled into a parent, so nothing is counted twice; `isBalanced` says debits equal credits in the opening, period and closing columns.
* The older `trial-balance` route returns the same lines as a plain list (old fields `totalDebit`, `totalCredit`, `balance` = the period's movement, plus the new ones).

## Account ledger

`GET account-ledger?accountId&fromDate&toDate&includeSubAccounts=true&pageNumber=1&pageSize=100` (page size at most 500)

* `openingBalance` = everything before `fromDate`; `closingBalance` = opening + the range; both on the account's normal side. Each line has the entry number and date, the description,
  the counter-accounts of the entry (e.g. `131, 3331`), debit, credit and the **running balance**, which covers all rows of the range (paging only slices what is shown).
* A parent account includes its sub-accounts (`includesSubAccounts`); a transfer between two sub-accounts nets out in the parent. Period-closing entries appear (`isClosingEntry`):
  the ledger is the real book, unlike the profit and loss.

## Cash flow (direct method)

`GET cash-flow?fromDate&toDate`

* **Cash accounts** are the accounts flagged `IsCash` (set in the chart; `Simple14`: 1000; `Vas99`: 111 / 1111 / 1112 / 112 / 1121 / 1122 / 113).
* Every posted entry of the range that touches a cash account is read line by line. The cash lines are the movement; **each other line says what the money was for** and is attributed to
  that account's **cash-flow category**: *operating* (default), *investing* (fixed assets, investments, construction in progress: 211–213, 217, 221, 222, 228, 121, 128, 241; `1500` in
  `Simple14`) or *financing* (borrowings and bonds 341 / 343 / 344, and every equity account by default). The category is editable per account (`PUT accounts/{id}`, `cashFlowCategory`
  1/2/3, 0 = default).
* A counter-line contributes `credit − debit` (money in is positive, out is negative); lines of one account are shown as *received* and *paid*. Entries whose lines are all cash accounts
  (cash ↔ bank transfers) move no money and are not shown. Closing and voided entries never count; a reversal cancels its original inside the same statement.
* Because an entry balances, the counter-lines add up to the cash movement exactly. The report returns **opening cash** (cash accounts before `fromDate`), **closing cash**, the net
  change and the check `opening + net change − closing` (`difference`, `isReconciled`). A non-zero difference means data that bypassed the ledger rules; it is shown, not hidden.
* **Not** the official B03-DN form: it has no indicator codes, no foreign-currency effect and does not apply the circular's rules for interest and tax paid (they follow the account's
  category). The default categories are suggestions to be reviewed with the accountant.

## B01-DN and B02-DN by indicator code (P1-2c)

`GET statements/balance-sheet?asOfDate&compareAsOfDate&format` and `GET statements/income-statement?fromDate&toDate&compareFromDate&compareToDate&format`
(`format=csv` downloads the file). The rows — indicator code (mã số), name, amount, prior amount — come from a **layout stored as data** (`StatementLayouts`): an *account row* is the sum
of the balances of every account whose number starts with one of its prefixes, a *formula row* is a signed sum of other rows (`10 = 01 − 02`, `270 = 100 + 200`).

* **Signs.** A balance is taken on the row's natural side, so a contra account (214 accumulated depreciation, 229 provisions, 419 treasury shares, 521 sales deductions) reduces its group by
  itself. Parent accounts carry no postings of their own, so prefix sums never count a posting twice (a test checks that no account is taken by two terms of one statement).
* **Balance side.** B01 splits 131 and 331 by the side of each account's balance: a credit balance on 131 is *customers' advances* (312), a debit balance on 331 is *advances to suppliers* (132).
* **B02** uses period activity **without** the period-closing entries; **B01** uses cumulative balances as of the date (closing entries included) and puts the result not yet moved to retained
  earnings into row 421b, so total assets (270) equal total sources (440) before and after closing. `isBalanced` says so for every column.
* **Nothing silently missing.** `unmapped` lists accounts with a balance that no row takes (production-cost accounts 621 / 622 / 627, or every account of a tenant on another chart, e.g. `Simple14`).
  A test checks that every account of the `Vas99` template is on a statement except those documented exclusions (and the temporary account 911).
* **Prior-period column:** each row returns `amount` and `priorAmount` when a comparison date (B01) or range (B02) is given.
* **CSV** (all report routes with `format=csv`): UTF-8 with a byte-order mark so Excel shows Vietnamese correctly, CRLF, RFC 4180 quoting, numbers with a dot decimal and no thousands separator
  (a Vietnamese-locale Excel may need the import wizard for decimals), and text cells starting with `= + @ -` prefixed with an apostrophe against formula injection. The ledger export has every row.
* **Draft.** The rows and account groups were written from the Circular 200 forms (numbering largely kept by Circular 99/2025/TT-BTC) and **have not been checked against the official forms**.
  Short- versus long-term is decided by the account (no maturity data): borrowings 341 are short-term, bonds 343 and deposits 344 long-term, prepaid 242 long-term. The accountant must review the
  layout before the statements are filed. The Vas99 template gained the sub-accounts 2291–2294 (provisions) and 2147 for this.

## B03-DN by indicator code (P1-2d)

`GET statements/cash-flow?fromDate&toDate&compareFromDate&compareToDate&format` returns the same shape as B01 / B02 (`rows` with code, name, amount, priorAmount; `unmapped`; `isBalanced`; `notes`; `isDraft`).
The layout (`CashFlowLayouts`, data like `StatementLayouts`) numbers the rows 01 … 07, 20, 21 … 27, 30, 31 … 36, 40, 50, 60, 61, 70 of the Vietnamese form. Business rules BR-64 … BR-69 and the full
prefix → row table are in [BA-Accounting.md](BA-Accounting.md) §6.8.

* **Source.** The same cash movements as the older `cash-flow` report (`IJournalLineRepository.GetCashEntryLinesAsync`: lines of posted / reversed entries that touch an `IsCash` account, closing entries left out;
  transfers between cash accounts dropped). Each non-cash line contributes *credit − debit* (received positive, paid negative).
* **Placement.** `CashFlowStatementEngine` walks the layout top to bottom and gives each movement to the first row whose prefix **and direction** match its counter-account (a movement counts once). Operating movements no row
  names go to 06 / 07 by the account's category; investing / financing ones no row names are listed in `unmapped` and make the check fail (they are never pushed into another activity).
* **Check.** Row 70 (50 + opening cash 60 + FX effect 61 = 0) must equal the cash on the books at the end of the range, in every column — `isBalanced`.
* **Draft.** Like B01 / B02 the rows come from the Circular 200 form and need the accountant's review. The older `cash-flow` route (by account, with its own reconciliation) stays.

## Amount unit and company header (P1-2e)

B01 / B02 / B03 accept `unit=vnd|thousand|million` (default: the tenant's report profile, else VND) and answer with `unit`, `unitDivisor` and `header`. **Rounding** is per statement row (account-driven rows: exact sum ÷ unit, half away from zero);
total rows add the rounded rows so the form foots; `isBalanced` (270 = 440, 70 = closing cash) is decided on the exact amounts and a note reports any rounding difference. Unmapped accounts stay in VND; the trial balance and ledger are never rounded.
The **report profile** (`GET/PUT accounting/reports/profile`, table `ReportProfiles`) holds the company name, tax code, address, the three signatories and the default unit; CSV and Excel print the company block above the title, the unit in the
period line and the signatories below. Rules BR-80 … BR-86 and a worked example: [BA-Accounting.md](BA-Accounting.md) §6.10.

## Excel and CSV export (P1-2c / P1-2d)

Every report route accepts `format=csv` and `format=xlsx`. `ReportTables` turns each report into rows of cells (text or decimal); `CsvWriter` and `XlsxWriter` (ClosedXML, MIT) serialize the same table.
In Excel amounts are numeric cells (`#,##0.00`, negatives red), codes and names are text, the header row is bold and frozen, the sheet name is the report code (cleaned to Excel's rules, ≤ 31 characters), and text that
starts with `= + - @` is stored as text with Excel's quote prefix. Content type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`. PDF is not built.

## Known limits (see docs/DEFERRED-ISSUES.md)

The ledger and the cash flow read journal lines by account and range (index `IX_JELines_AccountId`); the ledger pages in memory after computing the running balance, which is fine
for normal volumes but a very busy account over a very long range should use date-bounded snapshots later. B09 notes, off-balance accounts, indirect-method cash flow, PDF export and layouts for charts other than Vas99 are not built (ROADMAP P1-2f). Excel files are built in memory.
