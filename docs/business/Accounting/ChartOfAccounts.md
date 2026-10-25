# Chart of accounts, void / reverse and period-end closing

Status: ROADMAP P1-2 (2026-10-24). The ledger, trial balance and cash-flow reports are described in Reports.md (P1-2b); the B01/B02/B03 layouts are P1-2c.

## Read this first: the Vietnamese chart is a starting point, not legal advice

The `Vas99` template follows **Circular 99/2025/TT-BTC**, which replaced Circular 200/2014/TT-BTC for general enterprises from **2026-01-01**
(the chart is in its Appendix II; a business may add, rename or restructure accounts). It lists the commonly used accounts of classes 1–9 with
their usual sub-accounts, numbered as in Circular 200, which the new circular largely keeps. It was written from summaries, **not checked against
the official text**: some numbers or names may differ. Before real use the company's accountant must review the account list and the posting
profile (which account each automatic entry uses). Small enterprises that follow Circular 133/2016 can simply deactivate what they do not use.

## Account tree

* An account has an optional **parent**; a sub-account has the same account type as its parent and `Level = parent.Level + 1`. No cycles.
* A **parent groups its sub-accounts and takes no postings**: manual entries, the posting profile and the engine refuse it. Only leaves are postable.
* The class of an account is the first digit of its number (1–2 assets, 3 liabilities, 4 equity, 5–8 revenue and expenses, 9 profit determination).
  Account types stay 1–5: classes 5 and 7 are Revenue, 6 and 8 Expense, 911 is a temporary Equity account that closing brings back to zero.
  A **contra account** (214 accumulated depreciation, 521 sales deductions …) keeps its own normal balance; reports sign by the type's natural side.
* The number and type of an account never change; rename it or move it with `PUT accounts/{id}`. `POST accounts/{id}/deactivate` is refused while the
  account is used by the posting profile, has active sub-accounts, or has a balance. Class 0 (off-balance accounts) is not supported.
* API: `GET accounts`, `GET accounts/tree`, `POST accounts` (optional `parentAccountId`; a duplicate number is a 400), `PUT accounts/{id}`,
  `POST accounts/{id}/activate|deactivate`. The admin page shows the tree with edit / deactivate / apply-template.

## Templates

| Template | What it is |
|---|---|
| `Simple14` (default) | The platform's original 14 flat accounts (1000 … 5200). SYSTEM, demo data and tests use it. |
| `Vas99` | ~110 accounts of the Vietnamese chart with sub-accounts and its own posting map (below). |

`GET accounting/chart/templates`, `POST accounting/chart/apply-template {templateId}`: adds the template's missing accounts and points **every** posting
key at the template's account. Allowed only while the tenant has **no journal entries** (otherwise 400: re-map the posting profile instead).
`Accounting:DefaultChartTemplate` (default `Simple14`) chooses the chart of a tenant that has no accounts when its first document is posted.

Posting map of `Vas99` (suggested): Cash 1111, Bank 1121, AccountsReceivable 131, AccountsPayable 331, SalesRevenue 5111, Inventory 1561, VatOutput 3331,
GoodsReceivedNotInvoiced 3388, CostOfGoodsSold 632, InventoryAdjustment 632, CashOverShort 811, SalaryExpense 6421, SalariesPayable 334,
PayrollWithholdingsPayable 3388 (until insurance / PIT are split, P1-7), SalesReturns 5212, IncomeSummary 911, RetainedEarnings 4212, LoanPayable 341, InterestExpense 635 (the last two since P1-2j; on the `Simple14` chart LoanPayable is 2100 and InterestExpense 5100).

### Class 0 — off-balance accounts (P1-2i)

The Vietnamese template carries 001 (tài sản thuê ngoài), 002 (vật tư, hàng hóa nhận giữ hộ, nhận gia công), 003 (hàng hóa nhận bán hộ, ký gửi), 004 (nợ khó đòi đã xử lý), 007 (ngoại tệ các loại) and 008 (dự toán chi sự nghiệp, dự án) as account type **OffBalance (6)**, debit normal. They
take **no journal postings** (manual, profile or automatic): their movements go to the off-balance register (`accounting/off-balance`, Reports.md). A tenant with journal entries cannot apply a template; it creates the accounts it needs with type OffBalance (the account form offers the type).
Account types are 1 – 6; the template count is now the previous 109 plus these six.

## Void vs reverse

| | Reverse | Void |
|---|---|---|
| What happens | a mirror entry is posted (at `reversalDate`, default today); the original becomes `Reversed` | the entry becomes `Voided` and every report ignores it |
| For | any posted entry, including one that belongs to a document | a **hand-written** entry (no source document) of an **open** period |
| Audit trail | both entries stay in the books | the entry stays visible, flagged Voided, with the reason |
| Endpoint | `POST journal-entries/{id}/reverse` | `POST journal-entries/{id}/void` |

Both need a reason. A reversal does not depend on `Accounting:AutoPosting:Enabled` (that switch is for documents posting by themselves). A reversal dated
in a closed period is booked today. Period-closing entries are never reversed or voided by hand: reopen the period.

## Period-end closing

`POST fiscal-periods/{id}/close` (`?postClosingEntries=false` skips the entry):

1. checks: every earlier period is closed; the period has no draft entries; its debits equal its credits;
2. writes **one entry** (`ReferenceType = PeriodClosing`, key `PeriodClosing:<periodId>`, dated the period end, inside the period): every leaf revenue and
   expense account with a balance is cleared on its own side, the net result goes to the **IncomeSummary** account (VAS 911) and from there to
   **RetainedEarnings** (VAS 421/4212) — a profit as a credit, a loss as a debit; with the `Simple14` chart both keys are 3100 and the transfer lines are skipped;
3. locks the period (and raises `FiscalPeriodClosed`). A period with no revenue or expense activity is closed without an entry.

`POST fiscal-periods/{id}/reopen {reason}` works for the latest closed period only: its closing entry is voided (its key is renamed so a new one can be posted)
and the period is open again.

### Closing policy (P1-2d)

`GET / PUT fiscal-periods/closing-policy` (`accounting:periods:view|manage`), table `ClosingPolicies` (migration 011): `ClosingMode` **Monthly** (default, everything above) or **YearEnd**, and `FiscalYearEndMonth` (default 12).

* **Monthly:** every close writes its closing entry (moves the result to retained earnings each month).
* **YearEnd:** a close that is not the last period of the fiscal year runs the checks and **only locks**; the last period (it ends on the last day of the year-end month) writes **one** `PeriodClosing` entry built from the
  **remaining cumulative balance** of every revenue / expense account up to the period end ("Year-end closing …"). Because it reads what is still open, switching modes never closes a month twice.
* The mode can change only between years (refused while the latest closed period is not the last of a year). Reopening the year-end period voids its entry as before.
* **Carry-forward:** balances are cumulative, so after the year-end entry revenue / expense accounts open the next year at zero and balance-sheet accounts keep their balance — no opening entry exists. Details and rules:
  [BA-Accounting.md](BA-Accounting.md) §4.6, UC-11, UC-12, BR-70 … BR-78.

Reports: the **profit and loss** leaves out `PeriodClosing` entries, so a closed period still shows its result; the **balance sheet** adds the result that has
not been closed yet to equity (assets = liabilities + equity before and after closing); the trial balance counts the closing entry (the summary account nets to 0).

## Known limits

migrating a tenant that already has entries to another chart (re-map the profile by hand); the SYSTEM tenant
and the demo data stay on the 14-account chart.
