# Accounting — Admin set-up screens: Tax rates, Fiscal periods, Chart of accounts, Bank accounts and reconciliation

Business analysis of four set-up screens of the Admin as shipped on 2026-11-02 (ROADMAP P2-9, U3 batch 9). The accounting rules behind them are in [BA-Accounting.md](BA-Accounting.md) (baseline), [ChartOfAccounts.md](ChartOfAccounts.md) (chart, void / reverse, closing) and [AutoPosting.md](AutoPosting.md) (posting keys); this document describes what the screens ask of the accountant and what they now protect. Vietnamese user guide: [docs/user-guides/accounting-setup.md](../../user-guides/accounting-setup.md).

## 1. Purpose and scope
Give the accountant four places where the books are prepared and kept in order:
- **Tax rates** (`accounting/tax-rates`): the list of VAT rates an invoice or bill can use.
- **Chart of accounts** (`accounting/accounts`): the tree of accounts, a chart template, activation.
- **Fiscal periods** (`accounting/fiscal-periods`): open, close and reopen periods; the closing policy; the foreign-currency revaluation panel (unchanged in this batch).
- **Bank accounts** (`accounting/bank-accounts`): bank accounts, their statements and the line-by-line **reconciliation** of a statement against the ledger.

Out of scope: journal entries, bills and invoices (the next batch), reports, loans. Editing a statement, a bank account or a closed period is not offered by the API.

## 2. Actors and permissions
| Actor | Permission | What it allows |
|---|---|---|
| Chief / general accountant | `accounting:accounts:manage`, `accounting:periods:manage`, `accounting:tax:manage` | add / edit / deactivate accounts and rates, apply a chart template, open / close / reopen periods, change the closing policy |
| Treasury accountant | `accounting:bank:view`, `accounting:bank:manage` | see bank accounts; add accounts and statements, import lines, match, post fees, reconcile |
| Reader | page access only | sees the lists; no action buttons |

## 3. State models and rules
**Account.** Active ↔ Inactive. Number, type and normal balance never change after creation (the form shows them locked with the reason). A parent takes no postings; a sub-account has the same type as its parent (the parent list offers only active accounts of the chosen type). A tenant with journal entries cannot apply a chart template (the server refuses; its reason is shown).
**Tax rate.** Active → Inactive. Code unique per tenant. Rate typed as a percentage, 0 to 100.
**Fiscal period.** Open → Closed → (reopen, with a written reason) → Open. Closing writes the `PeriodClosing` entry according to the closing policy and locks the period; the server refuses while an earlier period is open, while draft entries exist or when the trial balance does not balance. Reopening voids that entry.
**Bank statement.** Open → Reconciled. A reconciled statement is locked. Statement line: Unmatched → Matched (to a ledger line of the same amount) or Posted (a fee / interest entry booked from the line, keys `BankFee` / `BankInterestIncome`). A statement can be reconciled **only when the difference is 0**.

**Changes in this batch (plan-approved):**
- Every step that changes the books asks first, stating the consequence, and sends nothing before the confirm button: **Apply chart template**, **Deactivate account**, **Post as fee / interest** (books a journal entry), **Delete statement line**, **Reconcile statement** (locks it). Close period and Reopen period already asked (Close now also toasts or shows the server reason; it was an unnoticed line under the page).
- The reconciliation difference is written in words ("Difference 0 – ready to reconcile" / "Difference 1,200 – cannot reconcile yet"), not only green / red.
- Refusals from the server (a rate in use, a period that cannot close, a CSV with bad rows) are shown as the server's text in a toast or inside the dialog; nothing fails silently any more.
- The three stacked dialogs (statements → reconciliation → confirm) close one at a time with Esc.

## 4. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| AS-1 | A step that changes the books is never one click | The five steps above open a confirmation that states the consequence; no request is sent before it; Esc on the confirmation keeps everything as it was |
| AS-2 | Forms validate first, in words | Empty required field → "Bắt buộc" linked to the field; tax rate outside 0–100 → "Enter a rate from 0 to 100."; a period whose end is before its start → "The end date must not be before the start date."; nothing is sent while invalid |
| AS-3 | Immutable fields are explained | On edit, account number / type / normal balance are disabled and carry the hint "Cannot be changed after the account is created." |
| AS-4 | Refusals are explained | The server's reason appears (toast or inside the dialog) for: rate deactivation, account deactivation, template apply, period close / reopen, CSV import, post, reconcile |
| AS-5 | The reconciliation is readable | The difference, the four balances (as a labelled list), line status as a text badge and every row button named after its line ("Post as fee Monthly fee"); the table has an accessible actions header |
| AS-6 | Stacked dialogs behave | Statements → reconciliation → confirm: Esc closes the top dialog only; after closing the focus stays inside the dialog still open (Modal fallback added in this batch) |
| AS-7 | Lists are usable | Each list has an empty state with the add action; the "active only" filter is a labelled checkbox; amounts are right-aligned |
| AS-8 | Accessible | Every dialog traps focus, locks the scroll and has no axe violation |

## 5. Use cases
**UC-1 Set up VAT rates.** Tax rates → Add Tax Rate → name, code, rate (10 = 10 %) → Save → "Đã lưu." *Alternative:* edit; *exception:* rate 150 → refused in the form.
**UC-2 Start a chart from a template.** Chart of accounts → Apply chart template → choose a template (the Vas99 review warning is shown) → Apply → read "Add the n accounts … only possible while there are no journal entries" → Apply. *Exception:* the tenant already has entries → the server's reason is shown and nothing changes.
**UC-3 Retire an account.** Deactivate → confirmation ("takes no new postings; history stays") → Deactivate. *Exception:* the account has a balance → the server refuses, toast with the reason.
**UC-4 Close the month.** Fiscal periods → Close on the period → read "the period-end transfer is posted and the period is locked. It can be reopened later." → Close. *Exception:* an earlier period is still open → toast with the reason. *Reopen:* Reopen → written reason → the closing entry is voided.
**UC-5 Reconcile a bank statement.** Bank accounts → View statements → Add statement (date, opening, closing) → Lines → choose the bank's CSV → Match automatically → for each remaining line: Match to a candidate of the same amount, or Post as fee / interest (confirm), or Delete (confirm) → when the difference is 0 the footer **Reconcile** enables → confirm → statement locked. *Exception:* a CSV row is wrong → nothing is imported, the row numbers and reasons are listed in the dialog.

## 6. Data and integrations
Account(Number, Name, Type, NormalBalance, Parent, IsCash, CashFlowCategory, IsActive), TaxRate(Name, Code, Rate, IsActive), FiscalPeriod(Name, StartDate, EndDate, Status), ClosingPolicy(Mode, FiscalYearEndMonth, AutoAccrueLoanInterest), BankAccount, BankStatement(Date, OpeningBalance, ClosingBalance, Status), BankStatementLine(Date, Description, Reference, Amount, MatchStatus). Events published by the services: `FiscalPeriodClosed`, `JournalEntryPosted` (fee / interest). No new endpoint or event in this batch.

## 7. Assumptions
Amounts on these screens are shown with the browser's locale (a Vietnamese user sees `1.200`, an English one `1,200`); the reconciliation difference text uses the same format.

## 8. Open issues (recorded in DEFERRED-ISSUES R71 / R96)
- Reopening a period has always asked for a reason; its confirmation is the shared `ReasonDialog` (unchanged).
- The foreign-currency revaluation panel on the fiscal periods page still uses the old classes and posts without a confirmation (next batches).
- There is no edit for a bank account or a statement, and a statement line cannot be edited (delete and re-import).
- The walk tenant had no closable period of its own to close for real, so Close / Reopen / Apply template / Post as fee / Reconcile were exercised up to their confirmation only.

## 9. UAT scenarios
1. Tax rates → Add → Save empty → two linked "Bắt buộc"; rate 150 → "Enter a rate from 0 to 100."; rate 8 → saved, listed, toast.
2. Tax rates → Deactivate → Esc → still Active → Deactivate → confirm → Inactive.
3. Chart of accounts → Add account 99911 → Edit → number and type are disabled with the hint → Esc.
4. Chart of accounts → Deactivate the new account → text "takes no new postings" → Esc keeps it Active → confirm → Inactive.
5. Chart of accounts → Apply chart template → pick a template → Apply → the second dialog names the template and the number of accounts → Esc → Esc: nothing applied.
6. Fiscal periods → Open Period → end before start → refused in words; Esc.
7. Fiscal periods → Close on an open period → the text says the period is locked → Esc: nothing closed.
8. Bank accounts → Add → empty Save → three linked errors; add a bank account and a statement.
9. Reconciliation of a throw-away statement: import a one-line CSV → line listed, difference in words → Post as fee → confirmation "A journal entry is booked" → Esc → Delete → confirmation names the line → confirm → line gone → Esc closes only the reconciliation dialog, Esc again closes the statements dialog.
