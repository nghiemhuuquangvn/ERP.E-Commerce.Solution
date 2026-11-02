# Accounting — Admin posting screens: Journal entries, Bills, Invoices (and their shared parts)

Business analysis of the three Admin screens where documents are booked to the ledger, as shipped on 2026-11-02 (ROADMAP P2-9, U3 batch 10). The accounting rules behind them are in [BA-Accounting.md](BA-Accounting.md) (baseline), [AutoPosting.md](AutoPosting.md) (posting keys, idempotence), and [AdminSetup.md](AdminSetup.md) (the set-up screens of batch 9). This batch changes **how the user is asked**, not what the server does: no endpoint, payload, permission or amount rule changed.

## 1. Purpose and scope
- **Journal entries** (`accounting/journal-entries`): list by fiscal period, view an entry's lines, post a hand-written entry, reverse a posted entry, void a hand-written entry.
- **Bills** (`accounting/bills`, accounts payable): list and filter, create a bill (one amount, several VAT lines or a foreign-currency amount), **Approve** (books the payable and, optionally, corrects the VAT rate), **Mark paid**, AP aging tab.
- **Invoices** (`accounting/invoices`, accounts receivable): list and filter, create, **Mark paid**, **Adjust VAT** (re-split net / VAT), **E-invoice** (issue, cancel, replace, adjust), AR aging tab.
- Shared parts: the **missing journal entries bar** (documents saved without an entry, with "Post missing entries"), the foreign-currency fields, the VAT-lines editor and the **foreign-currency revaluation panel** on the Fiscal periods page (closes DEFERRED R96).

Out of scope: editing a posted entry (there is none; correct by reversing), a draft entry (the API creates and posts in one call), approval workflows for journals, bill / invoice cancellation.

## 2. Actors and permissions
| Actor | Permission | What it allows |
|---|---|---|
| General accountant | `accounting:journals:manage` | post, reverse, void entries |
| Payables accountant | `accounting:bills:manage` | create, approve, mark paid, post missing entries |
| Receivables accountant | `accounting:invoices:manage` | create, mark paid, adjust VAT, e-invoice actions, post missing entries |
| Chief accountant | `accounting:periods:manage` | post the foreign-currency revaluation (Fiscal periods page) |
| Reader | `…:view` of each area | lists, entry lines, aging; no action buttons |

## 3. State models and business rules
**Journal entry.** Posted → Reversed (a mirror entry is added, the original stays in the books) or Posted → Voided (only a hand-written entry of an open period; ignored by reports). An entry that belongs to a document (`referenceType` set) can be reversed but not voided; a `PeriodClosing` entry is reopened through the period, not reversed here. Posting needs a balanced entry (total debit = total credit, above 0), an open fiscal period and postable accounts (a parent account and an off-balance class 0 account are refused by the server with its own explanation).
**Bill.** Pending → Approved → Paid. Approving books the bill to the ledger as a payable; the VAT rate on the supplier's invoice may differ from the split made when the bill was created. A foreign-currency bill is paid at a **payment rate** (base currency per 1 unit) that books the realised exchange difference.
**Invoice.** Unpaid → Paid (Overdue is derived from the due date). Adjust VAT re-splits an invoice at one rate or at several VAT lines whose amounts add up to the invoice amount; the amount owed never changes. E-invoice: Issue (sent to the e-invoice provider; the invoice's VAT is locked while one is in force), Cancel (with a reason), Replace (a new e-invoice with a reason and, optionally, a new rate), Adjust (signed net / VAT changes with a reason).
**Missing entries.** A bill or invoice saved when no fiscal period was open or the posting profile was incomplete has no journal entry; **Post missing entries** retries all of them (idempotent per source document) and reports how many could not be posted yet.
**Revaluation.** For a fiscal period, open foreign receivables and payables are revalued at the closing rates (typed, else the master data's); one entry dated the last day of the period is posted and reversed the next day.

**Changes in this batch (plan-approved):**
- Every step that books or locks something **asks first, stating the consequence**, and sends nothing before the confirm: Approve bill (with the VAT-rate field, replacing `window.prompt`), Mark paid for bills and invoices (replacing one-click and `window.prompt`; a foreign-currency document needs a payment rate above 0 in the same dialog), Issue / Replace / Adjust e-invoice, Post missing entries, Post the revaluation (R96). Reverse and Void keep their written reason; success and refusal are now toasts or dialog text.
- Forms use the component library: linked, translated field errors ("Bắt buộc", "The amount must be greater than 0."), the server's refusal text inside the dialog (an off-balance account, a closed period …), no silent failure.
- The journal line grid names each input by row and column ("Line 2 Debit"); the balance box is a live status that writes the difference in words; Post is never silently dead: pressing it while unbalanced says why.
- Bills and Invoices tabs keep the tab in the URL (`?tab=aging`) and follow the tablist keyboard pattern.

## 4. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| AP-1 | Booking is never one click | The seven steps above open a confirmation that states the consequence; no request is sent before it; Esc keeps everything as it was |
| AP-2 | Approve a bill with the real VAT rate | The dialog shows the bill amount and the current rate; an unchanged or empty rate sends none (the split of creation is kept), a changed rate is sent as `taxRatePercent` |
| AP-3 | Payment rate for foreign documents | Mark paid on a document in a foreign currency requires a rate above 0 ("Enter a rate greater than 0."); the rate is sent as `paymentRate`; a base-currency document sends none |
| AP-4 | A journal entry is balanced before it is posted | The balance box shows both totals and, when unequal, "Not balanced … (difference n)"; pressing Post while unbalanced shows an error and sends nothing; a balanced entry sends the exact lines |
| AP-5 | Forms validate in words | Empty required fields → "Bắt buộc" linked to the field; the line account error is linked to its message; the server's text is shown for any refusal |
| AP-6 | E-invoice steps are deliberate | Issue states the e-invoice goes to the provider and locks the VAT; Replace and Adjust each need their reason before the button enables and ask again; Cancel needs a written reason |
| AP-7 | Missing entries and revaluation are explicit | Post missing entries names how many documents will be booked and reports "All posted" or "n could not be posted yet"; the revaluation confirm names the period, the net difference and that the entry is reversed the next day |
| AP-8 | Lists are usable and accessible | Empty states with the add action; every row button is named after its document ("Approve B-1"); tables have scoped headers; every dialog traps focus and has no axe violation |

## 5. Use cases
**UC-1 Post a hand-written entry.** Journal entries → New Journal Entry → number, fiscal period, description → two or more lines (account, debit or credit) → Balanced → Post → toast "Entry posted." *Exception:* an off-balance (class 0) or parent account, or a closed period → the server's sentence appears in the dialog and nothing is posted.
**UC-2 Correct a mistake.** Hand-written entry → Void (reason) — or any posted entry → Reverse (reason, date) → toast. *Exception:* an entry that belongs to a document cannot be voided; a closed period refuses both.
**UC-3 Approve a supplier bill.** Bills → Approve on a Pending bill → read "books the bill to the ledger" → correct the VAT rate if the supplier's invoice differs → Approve. *Exception:* the posting profile is incomplete → the bill is approved but shows the warning icon "Not posted to the ledger"; use Post missing entries after fixing the profile.
**UC-4 Pay a bill or collect an invoice in a foreign currency.** Mark Paid → the dialog asks for the rate of the day (prefilled with the document's rate) → Mark paid → realised exchange difference is booked.
**UC-5 Fix the VAT of an invoice.** Invoices → Adjust VAT → one rate, or Several VAT lines until the total equals the invoice amount (the Save button stays off until it does) → Save.
**UC-6 E-invoice.** Invoices → E-invoice → buyer details → Issue e-invoice → confirm → status Issued with number and lookup code. Later: Cancel (reason), Replace (reason, new rate) or Adjust (net / VAT change, reason), each with its own confirmation.
**UC-7 Close the month's foreign balances.** Fiscal periods → Revaluation: pick the period, type closing rates if the master data has none → read the table and totals → Post the revaluation → confirm the entry and its reversal.

## 6. Data and integrations
JournalEntry(EntryNumber, FiscalPeriod, Description, Lines(Account, Debit, Credit, Description), Status, ReferenceType), Bill(BillNumber, Supplier, PO, Net, Tax, Amount, Foreign currency / rate, DueDate, Status, PostedToLedger), Invoice(… plus ShippingNetAmount, e-invoices), PostingGap, Revaluation. Endpoints unchanged: `journal-entries` (+ `reverse`, `void`), `bills` (+ `approve`, `mark-paid`, `ap-aging`), `invoices` (+ `mark-paid`, `adjust-vat`, `e-invoice*`, `ar-aging`), `accounting/posting-gaps` (+ `repost`), `accounting/fx/revaluation` (+ `preview`).

## 7. Permissions
`accounting:journals:view|manage`, `accounting:bills:view|manage`, `accounting:invoices:view|manage`, `accounting:periods:manage` (revaluation). The page shows "You do not have permission" without the view permission.

## 8. Assumptions
Amounts follow the browser's locale; the journal totals keep two decimals. The account list in the entry dialog is the whole chart (parents and off-balance accounts included): the server, not the form, decides which can be posted, and says so.

## 9. UAT scenarios
1. Journal entries → New → press Post empty → errors under the fields; fill, leave unbalanced → "Not balanced (difference …)", Post refuses; balance it → posts; the row appears.
2. Void that entry with a reason → row Voided. Reverse a posted entry → mirror entry appears.
3. Bills → open Approve → text says it books the bill; change the VAT rate → approve → the bill is Approved.
4. Mark paid on a base-currency bill → confirm text → Mark paid; on a foreign one → empty rate refused.
5. Invoices → Adjust VAT with lines that do not add up → Save is off; fix → Save.
6. Invoices → E-invoice → Issue → confirmation mentions the VAT lock → Esc leaves it unissued.
7. A document with the warning icon → Post missing entries → confirm → result toast.
8. Fiscal periods → Revaluation → Post the revaluation → confirmation names the period and the reversal; Esc posts nothing.
9. Tab through Bills with the arrow keys; reload keeps the tab; screen reader reads "Approve B-1" on the row button.

## 10. Open issues (DEFERRED-ISSUES R71 / R96 / R103)
- The posting dialogs are verified live up to their confirmation; the real posting of approve / pay / issue / revalue is covered by unit tests with a mocked API (the live walk executed only a throw-away journal entry, then voided and deleted it).
- Parent and off-balance accounts are offered in the entry dialog and refused by the server (R103).
