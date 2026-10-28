# POS back office in the Admin app (ROADMAP P2-1a)

Branch `feat/p2-1a-admin-promotion-pos` (2026-10-28). No migration. Builds on [module-18-ecommerce-pos.md](module-18-ecommerce-pos.md).

## 1. Purpose and scope
POSService had terminal, shift, sale and report APIs for the till, but no screen for the people who run the shops. This task gives the Admin app a **POS section**: manage terminals, review cash shifts (with the cash difference), search and inspect transactions (and void one), and print the end-of-day Z report. Three read endpoints were added because a back office cannot work from "get by id" alone: a paged transaction list, a shift list and a single terminal.

Out of scope (§8): opening / closing shifts and ringing sales from the Admin app (that is the till's job - staff app), cashier names (only ids are known to POS), refunds of a completed sale (a void is the only correction), exports.

## 2. Actors
| Actor | Does |
|---|---|
| Store manager (`pos:shifts:view`, `pos:reports:view`, `pos:transactions`) | Reviews shifts and the Z report, searches transactions, voids a wrong sale |
| POS administrator (`pos:terminals:manage`) | Creates, renames, deactivates and reactivates terminals |
| Auditor (`pos:reports:view`, `pos:shifts:view`) | Reads shifts and the Z report only |

## 3. Requirements and acceptance criteria
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-1 | Manage terminals | List with status, last sync, created date; add, rename; deactivate (with confirmation) / activate; `GET pos/terminals/{id}` returns one, unknown → 404 |
| FR-2 | Search transactions | `GET pos/transactions` filters by terminal, cashier, status (Open / Completed / Voided / Suspended) and days (the last day is **inclusive**), newest first, paged (page size capped at 100); an unknown status is HTTP 400 |
| FR-3 | Inspect a transaction | Detail shows items, subtotal, discount, tax, total, tenders, void reason and, for a completed sale, the receipt text |
| FR-4 | Void a transaction | Needs `pos:transactions` and a written reason; the list refreshes; the sale leaves the shift figures |
| FR-5 | Review shifts | `GET pos/shifts` filters by terminal, status (Open / Closed), days, paged; detail shows opening / counted cash, **difference = counted − (opening + cash sales)**, sales by payment method, completed / voided counts and the denomination counts |
| FR-6 | Z report | Pick a day (not in the future) and a terminal or all: shifts, completed / voided sales, sales, discount, tax, sales by method; *Print* uses the browser print |
| FR-7 | Menu and permissions | Sidebar group *Point of sale*: Transactions (`pos:transactions`), Shifts (`pos:shifts:view`), Z report (`pos:reports:view`), Terminals (`pos:terminals:manage`) |
| FR-8 | Two languages | Every label exists in English and Vietnamese |

## 4. Business rules
1. A POS list is always scoped to the signed-in shop (tenant filter of the POS DbContext); the new endpoints add no cross-tenant path.
2. Day filters are calendar days: `from` = start of that day, `to` = through the end of that day.
3. A void is allowed for any sale that is not already voided (the domain's rule); a voided sale is not counted in shift totals (see G6 in DEFERRED-ISSUES for sales voided after the shift closed).
4. The cash difference is informational; closing a shift is done at the till and is not changed here.
5. A deactivated terminal cannot open shifts or sell; reactivating restores it.

## 5. Use cases
**UC-1 End-of-day check (main).** Manager opens *Z report* for today → sees totals by payment method → opens *Shifts*, filters *Closed*, opens the shift with a difference → compares the counted denominations → asks the cashier.
*Alternative:* no shift today → the Z report shows zeros.

**UC-2 Wrong sale.** Manager opens *Transactions*, filters the terminal and day, opens the sale, reads the receipt, *Void* with the reason "scanned twice".
*Exception:* the sale is already voided → the button is not offered.

**UC-3 New till.** POS admin opens *Terminals* → *New terminal* → the terminal appears active and can open a shift.

## 6. Data and integrations
No new tables or events. New endpoints: `GET pos/transactions` (`pos:transactions`), `GET pos/shifts` (`pos:shifts:view`), `GET pos/terminals/{id}` (`pos:terminals:manage`); gateway prefix `/pos/api/v1`. A void still raises the existing POS events (Accounting / Reporting consume them as before).

## 7. UAT (all passed live on tenant `glcheck`, API level; data and the reporting facts of the three test sales removed afterwards)
| # | Scenario | Result |
|---|---|---|
| S1 | Create, get, rename a terminal; get an unknown one | 200 / 200 / 200 / 404 |
| S2 | Open a shift, three sales (cash 2×, card, open), complete two, void the card sale | all 200 |
| S3 | Transaction list for the terminal | total 3, statuses Open, Voided, Completed |
| S4 | Filter status Completed; page 2 of size 2 | total 1; 1 item of total 3 |
| S5 | Day filter: today, yesterday only | 3; 0 (last day inclusive) |
| S6 | Unknown status | 400 |
| S7 | Shifts: open filter, close with counted cash, closed filter | total 1; sales 220 000 |
| S8 | Shift summary and Z report | cash 220 000, 1 completed, 1 voided; Z total 220 000, 1 shift |
| S9 | Receipt; deactivate terminal | 200; 200 |
Not done: a browser walk-through of the screens (checked: `tsc -b` clean and the deployed bundle contains the routes).

## 8. Open issues
Cashier is shown as a short id (the POS service has no user names; needs a lookup through Identity); no refund of a completed sale from the back office; lists are not exported; the Z report prints with the browser's print only. See DEFERRED-ISSUES R52–R53.
