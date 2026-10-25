# HRM — Business Analysis: pay slip lifecycle and the link to the ledger

| | |
|---|---|
| **Domain** | Human Resources (HRMService, port 5007, code HRM) — payroll processing |
| **Covers** | ROADMAP P1-1d (pay a pay slip, cancel an unpaid one, payroll → ledger). The wider HR analysis (profiles, contracts, insurance, PIT …) is the existing set in [BA-Analysis/](BA-Analysis/00-domain-overview.md); real payroll calculation is ROADMAP P1-7. |
| **Status** | Implemented and verified on the local Docker stack, 2026-10-24 (unit tests HRM 8). Written afterwards, 2026-10-25, to the standard of [BA-Accounting.md](../Accounting/BA-Accounting.md). |
| **Audience** | Product owner, HR manager, payroll accountant, chief accountant, QA, developers |
| **Companion documents** | [BA-Accounting.md](../Accounting/BA-Accounting.md) (FR-10, UAT-10), [BA-Analysis/06-payroll.md](BA-Analysis/06-payroll.md), [AutoPosting.md](../Accounting/AutoPosting.md) |

> **Important limit.** The pay slip **figures are still a stand-in**: gross salary 5,000 and a flat 30 % deduction by default. Insurance, personal income tax and employer cost come with ROADMAP P1-7. This document describes the **lifecycle** (generate → pay | cancel) and how it reaches the books, which is independent of how the figures are calculated.

---

## 1. Business context and objectives

### 1.1 Problem
HRM could generate pay slips but they were dead ends:
* a slip could not be **paid** or **cancelled** — it stayed "pending" forever, so the ledger never learned that salaries were paid;
* a wrong slip could not be withdrawn and **blocked its month** (a unique index covered every slip, including cancelled ones), so the month could never be generated again;
* the generated event had only the net amount, so Accounting could not split the salary expense from the withholdings;
* events used random Kafka keys, so a **cancel could overtake its generation**.

### 1.2 Objectives
| # | Objective | How we know it is met |
|---|---|---|
| O1 | A pay slip has a controlled life: Pending → Paid, or Pending → Cancelled. | HRM-FR-01 … 03 |
| O2 | Paying out net pay and withdrawing a wrong slip reach the ledger. | HRM-FR-05, 06; UAT-HRM-01 … 03 |
| O3 | A cancelled slip frees its month for a corrected one. | HRM-FR-04; UAT-HRM-04 |
| O4 | The ledger can split gross pay, overtime and deductions. | HRM-FR-05; UAT-HRM-01 |

### 1.3 Scope
**In scope:** generating pay slips (all employees of a month, or one), paying and cancelling a slip, the events sent to Accounting.
**Out of scope:** the real calculation (social insurance, PIT, employer contributions, allowances — P1-7); payroll bank files; payslip delivery to employees; correcting a **paid** slip (needs a payroll correction process, not a cancel).

---

## 2. Stakeholders and actors

| Actor | Who | What they do |
|---|---|---|
| Payroll officer | `hrm:payroll:manage` | Generates the month's pay slips, pays them, cancels a wrong one. |
| HR manager | HRM roles | Reviews slips before payment. |
| Employee | via the employee portal (later) | Reads their own slip. |
| Chief accountant | Accounting roles | Reviews the salary entries and the payroll liability. |
| Accounting (system) | AccountingService | Consumes `hrm-topic` and posts. |
| Master data (system) | SharedMasterDataService | Provides the employee list HRM keeps as a read-only snapshot. |

---

## 3. Glossary

| Term | Meaning |
|---|---|
| Pay slip (phiếu lương) | One employee's pay for one month: gross, overtime, deductions, net. |
| Net salary | Gross + overtime pay − deductions. What the employee receives. |
| Deductions | Amounts withheld from pay (stand-in flat rate today; later insurance and PIT). |
| Pay period | The month; the slip is dated its last day. |
| Pending / Paid / Cancelled | The three states of a slip. |
| Salaries payable | The liability to employees until the net pay is paid. |

---

## 4. End-to-end business process

1. **Generate.** The payroll officer processes a month (all active employees) or one employee. For each, a pay slip is created Pending, dated the **last day of the month**, with figures. One slip per employee per month (unless an earlier one is cancelled).
2. **Book.** HRM publishes `PaySlipGenerated` (gross, overtime pay, deductions, net, period end). Accounting books *Dr Salary expense (gross + overtime) / Cr Salaries payable (net) and Cr Payroll withholdings payable (deductions)*, dated the end of the pay period but **not after today**.
3. **Pay.** The officer records that net pay was paid out → slip Paid, `PaySlipPaid` → Accounting books *Dr Salaries payable / Cr Bank* for the net pay (only if the slip's own salary entry exists).
4. **Or withdraw.** A wrong **pending** slip is cancelled with a reason → `PaySlipVoided` → Accounting **reverses** the salary entry; the month can then be generated again.

---

## 5. Functional requirements

| ID | Requirement | Pri | Source |
|---|---|---|---|
| HRM-FR-01 | A pay slip is created Pending with gross, overtime hours / pay, deductions, net = gross + overtime − deductions, dated the last day of its month. | M | existing |
| HRM-FR-02 | A pending slip can be **paid** (`POST hrm/payslips/{id}/pay`) once; a paid, cancelled or already-paid slip cannot. | M | P1-1d |
| HRM-FR-03 | A pending slip can be **cancelled** with a mandatory reason (`POST hrm/payslips/{id}/cancel`); a paid slip cannot. | M | P1-1d |
| HRM-FR-04 | A cancelled slip does not block its month: a new slip for the same employee and month can be generated. | M | P1-1d |
| HRM-FR-05 | `PaySlipGenerated` carries gross, overtime pay, deductions and the pay-period end date. | M | P1-1d |
| HRM-FR-06 | `PaySlipPaid` and `PaySlipVoided` events are published, keyed by the pay slip id. | M | P1-1d |
| HRM-FR-07 | Payroll can be processed for a whole month or for one employee; inactive employees are skipped. | S | existing |

### 5.1 Acceptance criteria
| ID | Acceptance criteria | UAT |
|---|---|---|
| HRM-FR-01 | Given gross 5,000, overtime 100 and deductions 1,500, then net = 3,600 (5,000 + 100 − 1,500) and the date is the month's last day. | UAT-HRM-01 |
| HRM-FR-02 | Given a pending slip, when it is paid, then its status is Paid and the payment event is published; when it is paid again, then it is refused. | UAT-HRM-02 |
| HRM-FR-03 | Given a pending slip, when it is cancelled with a reason, then status Cancelled and the voided event; without a reason → refused; cancel after payment → refused. | UAT-HRM-03 |
| HRM-FR-04 | Given a cancelled slip for April, when April is generated again for the same employee, then a new Pending slip is created. | UAT-HRM-04 |
| HRM-FR-05, 06 | Given the first slip, then the generated event shows 5,000 / 100 / 1,500 / 3,600 and the period end; paid and voided events have the pay slip id as the Kafka key. | UAT-HRM-01, 05 |

---

## 6. Business rules

| ID | Rule |
|---|---|
| HRM-BR-01 | **Net = gross + overtime pay − deductions.** |
| HRM-BR-02 | **One live slip per employee and month.** A cancelled slip does not count (filtered unique index, migration HRM 007). |
| HRM-BR-03 | Only a **pending** slip can be paid or cancelled; each happens at most once. |
| HRM-BR-04 | Cancelling needs a **reason** (kept in the event). A paid slip is corrected by a payroll correction, not by cancelling. |
| HRM-BR-05 | The slip is dated the **last day of the pay month**; the ledger entry is dated the same day but never in the future (capped at today). |
| HRM-BR-06 | Only active employees (per the master-data snapshot) get a slip. |
| HRM-BR-07 | **Stand-in figures:** until P1-7 the default gross is 5,000 and deductions are 30 % of gross; these are placeholders and must not be used for a real payroll. |
| HRM-BR-08 | Paying and cancelling are tenant-scoped like every HRM record; events carry the tenant. |

---

## 7. Use cases

### UC-HRM-01 Generate a month's pay slips
*Actor:* payroll officer (`hrm:payroll:manage`). *Main flow:* choose year and month → process → a Pending slip per active employee → events published → Accounting books each. *Alternatives:* generate for one employee. *Exceptions:* a live slip already exists for that employee and month → skipped / refused; employee not active → skipped.

### UC-HRM-02 Pay a pay slip
*Actor:* payroll officer. *Main flow:* open the pending slip → record payment (date, method) → status Paid → event → Accounting books *Dr Salaries payable / Cr Bank*. *Exception:* not pending → refused with the current status.

### UC-HRM-03 Cancel a wrong pay slip
*Actor:* payroll officer. *Main flow:* cancel with a reason → status Cancelled → event → Accounting reverses the salary entry → generate the month again (UC-HRM-01). *Exceptions:* no reason → refused; slip paid → refused ("needs a payroll correction").

### UC-HRM-04 List and read pay slips
*Actor:* payroll officer / HR manager. *Main flow:* list by period or employee, open one.

---

## 8. Data requirements

| Entity | Key attributes |
|---|---|
| PaySlip | number, employee, year, month, gross, overtime hours, overtime pay, deductions, net, payment date, payment status (0 Pending, 1 Paid, 2 Cancelled) |
| EmployeeSnapshot | read-only copy of the master-data employee (status) used to decide who gets a slip |

**State model:** Pending → Paid (end) | Pending → Cancelled (end; the month can be generated again).

---

## 9. Integrations

| Direction | Event | Transport | Content / effect |
|---|---|---|---|
| HRM → Accounting | `PaySlipGenerated` | Kafka `hrm-topic`, key = pay slip id | gross, overtime pay, deductions, net, period end → salary entry |
| HRM → Accounting | `PaySlipPaid` | same | net pay, pay date → *Dr Salaries payable / Cr Bank* |
| HRM → Accounting | `PaySlipVoided` | same | reason → reversal of the salary entry |
| MasterData → HRM | EmployeeHired / Terminated / Transferred | Kafka | keeps the snapshot current |

Accounting's consumer refreshes the topic metadata every 30 seconds, so a topic created after the service started is picked up (it used to sit idle for 5 minutes). Event names must not contain `PaySlipGenerated` (the Reporting service matches on it).

---

## 10. Reports

Pay slip list and detail; Accounting's ledger and trial balance show salary expense, salaries payable and withholdings. A payroll register and a payroll-to-ledger reconciliation are not built.

---

## 11. Security, audit and non-functional requirements

| Area | Requirement |
|---|---|
| Permissions | `hrm:payroll:manage` for generate, pay and cancel; read access under the HRM view permissions. |
| Tenant isolation | Pay slips are tenant-owned; the unique index is per employee and period among live slips. |
| Audit | The cancel reason and the payment date / method are kept in the events; the slip keeps its status and timestamps. |
| Consistency | Status change, domain event and outbox row commit together. |
| Ordering | Keying every pay-slip event by the slip id keeps *generated → paid | voided* in order. |

---

## 12. UAT scenarios

Verified on the local Docker stack on 2026-10-24 (SYSTEM tenant, April 2025) unless marked unit test.

| # | Given / When / Then | Requirement | Result |
|---|---|---|---|
| UAT-HRM-01 | Three slips for 2025-04 are generated; each books *Dr 5200 5,100 / Cr 2100 3,600 and Cr 2100 1,500* (gross 5,000 + overtime 100). | HRM-FR-01, 05 | Passed |
| UAT-HRM-02 | Paying a slip books *Dr 2100 / Cr 1000 3,600*; a second payment is refused (400). | HRM-FR-02 | Passed |
| UAT-HRM-03 | Cancelling a paid slip is refused; cancelling without a reason is refused. | HRM-FR-03 | Passed (paid) / unit test (reason) |
| UAT-HRM-04 | Cancel an unpaid slip, generate the month again: the first entry is Reversed with its reversal entry, and a new entry exists for the new slip. | HRM-FR-04 | Passed |
| UAT-HRM-05 | A cancel produced right after the generation does not overtake it (same key). | HRM-FR-06 | Passed (found with random keys, fixed) |

---

## 13. Roles and permissions matrix

| Action | Permission |
|---|---|
| Process payroll / generate a slip | `hrm:payroll:manage` |
| Pay a slip | `hrm:payroll:manage` |
| Cancel a slip | `hrm:payroll:manage` |
| List / read slips | HRM view permissions |

---

## 14. Assumptions and constraints

1. One currency (VND); one pay slip per employee per month; no off-cycle or bonus runs.
2. Pay is recorded as paid in full on one date; part payments and bank batches are not modelled.
3. The employee list comes from master data; an employee added after the slip was generated does not get one retroactively.
4. Figures are placeholders until P1-7 (HRM-BR-07).

---

## 15. Out of scope and open issues

Real payroll (insurance, PIT, employer cost) — ROADMAP P1-7; correcting a paid slip; payroll bank file and employee payslip delivery; Accounting posting of the employer's contributions; per-department cost allocation. See [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md). Note: HRM scripts 005–007 had also been missing from `RunAllScripts.ps1`; they are added.

---

## 16. Traceability and change log

| Task | Date | Requirements | Technical documents | Main tests |
|---|---|---|---|---|
| P1-1d payroll to the ledger | 2026-10-24 | HRM-FR-01 … 07; HRM-BR-01 … 08 | AutoPosting.md, HRM migration 007 | `PaySlipLifecycleTests` (HRM 8), `PayrollPostingTests` (ACC) |
| BA baseline (this document) | 2026-10-25 | All | — | — |
