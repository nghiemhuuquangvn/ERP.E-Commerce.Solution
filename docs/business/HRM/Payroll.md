# HRM — Payroll, insurance, personal income tax and annual leave (ROADMAP P1-7)

Business analysis of real payroll in HRMService, written 2026-10-26. It replaces the stub that gave every employee 5,000 gross and a 30 % lump deduction. The older Vietnamese-law analyses stay in `BA-Analysis/06-payroll.md`, `07-social-insurance.md`, `08-personal-income-tax.md`; this document describes what the system now does. The ledger side (account numbers, the pay slip entry) is in `docs/business/Accounting/BA-Accounting.md` (FR-160…163, BR-215…216, UC-41, UAT-161…164) and `AutoPosting.md`.

## 1. Purpose and scope
Produce a monthly pay slip per employee that is correct under the tenant's rules: pay for the days worked, overtime, social / health / unemployment insurance (employee and employer shares), personal income tax (PIT) with the personal and dependent deductions, and a ledger entry that shows each liability separately. Annual leave becomes a balance that requests draw down.

In scope: salary terms with history, dependents, date-effective rate sets (insurance rates, caps, deductions, tax brackets), worked days from attendance and unpaid leave, the annual leave balance, the pay slip breakdown, the posting of the split.
Out of scope (recorded in DEFERRED-ISSUES §4q): PIT finalisation at year end, overtime premium tax exemption, seniority leave accrual, regional minimum wage, several contracts at once, payroll cost centres, a payslip PDF, bank payment files.

## 2. Actors and permissions
| Actor | What they do | Permission |
|---|---|---|
| Payroll officer / HR | Maintains salary profiles and dependents, processes the month, pays or cancels slips | `hrm:payroll:manage` |
| Chief accountant | Reviews and confirms the rate sets (insurance rates, deductions, brackets) | `hrm:payroll:manage` |
| Line manager / HR | Submits and approves leave | `hrm:leave:manage` |
| Accountant | Reads the payroll entries in Journal entries | `accounting:journals:view` |
| System | Posts the pay slip to the ledger (Kafka `hrm-topic`) | service token |

There is no separate `:view` permission for payroll (convention of the HRM admin guide).

## 3. Process
1. The accountant opens **HR → Payroll settings**. A tenant that has no rate set gets the default one, marked **DRAFT**; the accountant checks it against the current regulations, edits it and ticks *Confirmed*.
2. HR enters each employee's **salary terms** (HR → Salary profiles → *Salary*): base salary, taxable and tax-exempt allowance, insurance salary (empty = base salary), whether insurance is paid, tax code, insurance number, effective month. A raise is a new row from a later month; the old row stays.
3. HR adds the **dependents** (name, relationship, tax code, from month, optional to month).
4. At month end HR presses **Process payroll** for a year and month. For every active employee without a (non-cancelled) pay slip for that month the system calculates a slip. Employees with no salary terms are **skipped and listed** in the result; nothing is invented.
5. HR opens a slip (*Details*) to see the calculation, then **Mark as paid** (net pay leaves the bank) or **Cancel** an unpaid slip with a reason (the entry is reversed and the month can be processed again).
6. Accounting receives `PaySlipGenerated` and posts the entry; `PaySlipPaid` clears the net pay against the bank; `PaySlipVoided` reverses.

## 4. Calculation (per employee and month)
Let the rate set in force on the last day of the month be R and the salary terms in force on that day be S.

| Step | Rule |
|---|---|
| Standard days | Monday–Friday days of the month |
| Paid days | standard days − Absent days − ½ × HalfDay days (only when the month has attendance records) − approved *Unpaid* leave days (working days inside the month); never below 0 |
| Base pay | base salary × paid days / standard days |
| Gross | base pay + taxable allowance + exempt allowance |
| Overtime pay | overtime hours (from attendance) × base salary / (standard days × 8) × 1.5 |
| Insurance salary | S.InsuranceSalary or the base salary; **not** prorated |
| Employee insurance | social 8 % and health 1.5 % of min(insurance salary, social cap); unemployment 1 % of min(insurance salary, unemployment cap) |
| Employer insurance | social 17.5 %, health 3 %, unemployment 1 % on the same bases (cost on top of the wages, not deducted) |
| Taxable income | max(0, gross + overtime − exempt allowance − employee insurance − personal deduction − dependents in force × dependent deduction) |
| PIT | each slice of taxable income taxed at its bracket's rate |
| Net pay | gross + overtime − employee insurance − PIT |

Every amount is rounded to whole VND per line. The percentages, caps, deductions and brackets above are the **seeded defaults**, not constants: they live in the rate set. Seeded defaults (effective 2026-01, **DRAFT**): personal deduction 15,500,000; dependent 6,200,000; social/health cap 46,800,000; unemployment cap 106,200,000; brackets 5 % to 10M, 10 % to 30M, 20 % to 60M, 30 % to 100M, 35 % above; annual leave 12 days. The project could not verify these against the regulation text; an accountant must confirm them.

**Worked example** (October 2026, 22 standard days, defaults): base 30,000,000 + lunch allowance 1,000,000 (exempt), no dependents.
Gross 31,000,000. Insurance 2,400,000 + 450,000 + 300,000 = 3,150,000. Taxable 31,000,000 − 1,000,000 − 3,150,000 − 15,500,000 = 11,350,000. PIT = 10,000,000 × 5 % + 1,350,000 × 10 % = 635,000. Net 27,215,000. Employer insurance 5,250,000 + 900,000 + 300,000 = 6,450,000.
Second case: base 60,000,000, two dependents: social base capped at 46,800,000 (3,744,000 / 702,000), unemployment 600,000, taxable 27,054,000, PIT 2,205,400, net 52,748,600, employer insurance 10,194,000.

## 5. Functional requirements
| ID | Requirement | Acceptance |
|---|---|---|
| FR-H1 | Salary terms per employee with an effective month; the latest row on or before the period end applies; saving the same month replaces it | `PUT employees/{id}/compensations`; base salary must be > 0, allowances ≥ 0; terminated employee refused |
| FR-H2 | Dependents with from / to month; only those covering the month count | `POST/GET employees/{id}/dependents`, `DELETE dependents/{id}`; name and relationship required, end not before start |
| FR-H3 | Rate sets by effective month: insurance rates, caps, deductions, annual leave days, tax brackets; the set of the month is used; a new set is a later date | `GET/PUT payroll-settings/rate-sets`; two sets cannot start the same month; rates in 0..1; exactly one open-ended last bracket, distinct positive limits |
| FR-H4 | A tenant with no rate set gets the default one, unconfirmed | first process / first look creates it; a month before every set is refused (400) |
| FR-H5 | Process payroll creates a calculated slip per active employee without a slip; an employee without salary terms is skipped and returned in `skipped` | result `{created[], skipped[]}`; running the month again creates 0 |
| FR-H6 | Paid days come from attendance (Absent, HalfDay) and approved unpaid leave; no attendance records = full month | unit tests; live: 22/22 |
| FR-H7 | The slip stores the breakdown and the rate set id; `Deductions` = insurance + PIT so older readers still work | `GET payslips/{id}` fields |
| FR-H8 | `PaySlipGenerated` carries the insurance and PIT split (optional fields, 0 on old events) | Accounting posts the split (FR-160) |
| FR-H9 | Each employee has an annual leave balance per year (rate set days), created on first use | `GET leave-requests/balances?employeeId&year` |
| FR-H10 | Leave days are working days (Mon–Fri); a request without any is refused | "The request covers no working day." |
| FR-H11 | An annual leave request over the remaining balance is refused at submission and at approval; approval consumes the days | 400 "Not enough annual leave: 12 day(s) left in 2026, 15 requested." |
| FR-H12 | Paying a slip once; cancelling an unpaid slip with a reason (existing P1-1d rules) | pay twice → 400 |
| FR-H13 | Admin screens: Salary profiles, Payroll settings, pay slip detail, skipped list, leave balance (section 9) | tsc clean; live API |

## 6. Business rules
- BR-H1 Money is rounded to whole VND per line; the net pay is the sum of rounded lines.
- BR-H2 Insurance is paid on the insurance salary, capped, and does **not** shrink with absences. Employer insurance is a cost, never deducted from the employee.
- BR-H3 Exempt allowances do not enter taxable income; every other income does, including overtime pay.
- BR-H4 Tax is progressive by slice, never a flat rate on the whole income. Taxable income below zero is zero.
- BR-H5 A slip is calculated with the rate set and salary terms in force on the **last day** of the month, so a mid-month change applies from the next month unless it was effective on or before the first day.
- BR-H6 One non-cancelled slip per employee and month. A cancelled slip frees the month.
- BR-H7 A slip is never created for an employee without salary terms.
- BR-H8 Unpaid leave and Absent days reduce pay, annual and sick leave do not. HalfDay counts ½.
- BR-H9 Annual leave is limited by the balance of the year of the start date; a request is not split across years.
- BR-H10 Defaults are drafts: `IsConfirmed` records that an accountant reviewed a set; nothing blocks payroll on an unconfirmed set (the screen shows DRAFT).

## 7. Use cases
**UC-H1 Process the monthly payroll.** Actor: payroll officer. Pre: rate set exists (or default), employees have salary terms. Main: Payroll → Process payroll → year, month → result. Alternative: some employees skipped → the result lists them → add salary terms → process again (only the missing ones are created). Exception: month before every rate set → 400. Post: slips Pending, events published.
**UC-H2 Set up an employee's pay.** Actor: HR. Main: Salary profiles → *Salary* → fill the terms → save; *Dependents* → add. Alternative: a raise → new row from the raise month. Exception: base salary 0 → 400.
**UC-H3 Maintain the rate sets.** Actor: chief accountant. Main: Payroll settings → *+ New rate set* (copies the latest) → change figures and brackets → save; tick *Confirmed*. Exception: bracket table without one open end → 400; same start month twice → 400.
**UC-H4 Take annual leave.** Actor: employee's manager. Main: Leave requests → new → the form shows "Annual leave 2026: 7 of 12 days left" → save → approve (days consumed). Exception: more days than left → refused with the numbers; weekend only → refused.
**UC-H5 Pay or withdraw a slip.** Actor: payroll officer. Main: open the slip → *Mark as paid*. Alternative: wrong figures → *Cancel pay slip* with a reason → process the month again. Exception: paid slip cannot be cancelled.

## 8. Data model
Tables in HrmDB (migration 008): `EmployeeCompensations`, `Dependents`, `PayrollRateSets`, `PitBrackets`, `LeaveBalances` (existing table given the base columns), `PaySlips` (new columns WorkedDays, StandardDays, EmployeeHealth, EmployeeUnemployment, EmployerSocial/Health/Unemployment, TaxableIncome, DependentCount, RateSetId; `BasicSalary` = base salary, `Allowances`, `SocialInsurance` = employee social, `TaxAmount` = PIT reused). All tenant-owned and soft-deleted. Unique: rate set per tenant and month, compensation per employee and month, leave balance per employee, year and type.
State: slip Pending → Paid | Cancelled (unchanged). Leave: Pending → Approved | Rejected | Cancelled.

## 9. Screens (admin)
- **HR → Payroll** (existing): columns number, employee, period, gross (+ overtime), deductions, net, status, *Details*. *Process payroll* shows, when employees were skipped, a result box "N pay slips created / M employees skipped: name (code) — no salary profile". *Details* opens the calculation: base, pay for X of Y days, allowances, overtime, gross; withheld social / health / unemployment; taxable income and dependents; PIT; net; the employer's insurance (grey box); buttons *Mark as paid* / *Cancel pay slip* (reason) for a pending slip. A slip entered by hand shows only the lump figures.
- **HR → Salary profiles** (new, `/hrm/salary-profiles`): active employees with base salary (or "No salary profile" in amber), allowances, insurance salary, tax code, dependents; actions *Salary* (terms form) and *Dependents* (list + add form).
- **HR → Payroll settings** (new, `/hrm/payroll-settings`): one card per rate set — name, month, DRAFT / Confirmed badge, insurance table (employee / employer × social / health / unemployment), deductions, caps, leave days, bracket table; *Edit*; *+ New rate set* form with the bracket editor.
- **HR → Leave requests**: the form shows the annual balance; refusals show the server's message; approving over the balance shows the message above the table.

## 10. Integrations
| Event / call | From → to | Content |
|---|---|---|
| `PaySlipGenerated` | HRM → Accounting, Reporting (Kafka `hrm-topic`) | existing fields + EmployeeSocial/Health/Unemployment, EmployerSocial/Health/Unemployment, IncomeTax (0 when absent) |
| `PaySlipPaid`, `PaySlipVoided` | unchanged | |
Reporting still reads only the old fields (G13).

## 11. Reports
No new report. The payroll liabilities are read in the trial balance / ledger on 334, 3335, 3383, 3384, 3389 (VAS chart).

## 12. Assumptions
Rates in the seeded set are the project's understanding of the rules in force from 2026-01 and must be confirmed. Employees are residents with one contract. Standard days are Monday–Friday (no public holidays calendar). Salaries are monthly.

## 13. Open issues
DEFERRED-ISSUES §4q (PIT year-end settlement, overtime premium exemption, seniority leave, holiday calendar, regional minimum wage, several contracts, cost centres, payslip PDF, salary changes triggered by master data, Reporting net pay G13).

## 14. UAT scenarios
| ID | Scenario | Expected |
|---|---|---|
| UAT-H1 | Process October with no salary profile | 0 created, every employee listed as skipped |
| UAT-H2 | Employee A 30M + 1M exempt allowance, no dependents | gross 31,000,000; insurance 3,150,000; PIT 635,000; net 27,215,000 |
| UAT-H3 | Employee B 60M, two dependents | insurance capped; taxable 27,054,000; PIT 2,205,400; net 52,748,600 |
| UAT-H4 | Process the same month again | 0 created |
| UAT-H5 | Ledger entry of A | 6421 Dr 31,000,000 and 6,450,000; Cr 334 27,215,000, 3383 7,650,000, 3384 1,350,000, 3389 600,000, 3335 635,000; balanced |
| UAT-H6 | Pay A, then pay again | first 200; second 400; Dr 334 / Cr bank |
| UAT-H7 | Cancel B | entry reversed; month can be processed again |
| UAT-H8 | New rate set from 2027-01 with personal deduction 20M; process January 2027 | taxable 6,850,000, PIT 342,500 for A (set picked by date) |
| UAT-H9 | Bracket table with no open end | 400 |
| UAT-H10 | Annual leave 15 working days with 12 left | 400 with the numbers |
| UAT-H11 | Annual leave Mon–Sun week | 5 days; approval → balance 5 / 12 |
| UAT-H12 | 10 more days with 7 left; weekend only | both 400 |
| UAT-H13 | Absent, half days and unpaid leave | paid days = standard − absent − ½ half − unpaid (unit test: 17 of 22) |

Verified live on tenant `glcheck` 2026-10-26: UAT-H1…H12 (H13 by unit test).
