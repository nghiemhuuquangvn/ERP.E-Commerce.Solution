# Accounting reports

Status: ROADMAP P1-2b (2026-10-24). Endpoints are under `api/v1/accounting/reports/…` (permission `accounting:reports:view`); the admin app has a tab for each.
Every report counts **Posted** and **Reversed** entries only (a reversed entry is offset by its reversal entry); Draft and Voided entries never count.

| Report | Route | What it shows |
|---|---|---|
| Trial balance | `trial-balance` (list), `trial-balance/full` (with totals) | Opening, period and closing balance per account |
| Account ledger (sổ cái / sổ chi tiết) | `account-ledger` | Every journal line of an account with counter-accounts and a running balance |
| Profit and loss | `profit-and-loss` | Revenue and expenses of a range (closing entries left out) |
| Balance sheet | `balance-sheet` | Assets, liabilities, equity as of a date (adds the not-yet-closed result to equity) |
| Cash flow | `cash-flow` | Direct method, by counter-account, with opening / closing cash |

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

## Known limits (see docs/DEFERRED-ISSUES.md)

The ledger and the cash flow read journal lines by account and range (index `IX_JELines_AccountId`); the ledger pages in memory after computing the running balance, which is fine
for normal volumes but a very busy account over a very long range should use date-bounded snapshots later. B01 / B02 / B03 layouts by indicator code, off-balance accounts,
indirect-method cash flow, comparative periods and export to Excel / PDF are not built (ROADMAP P1-2c).
