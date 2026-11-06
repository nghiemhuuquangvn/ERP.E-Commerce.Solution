# Employee import (CSV, streaming) — business analysis

Version 1.0, 2026-11-06 (ROADMAP M6). Technical pattern: `docs/architecture/streaming-import-pattern.md`. Employee master data: `Employee.md`.

## 1. Purpose and actors

HR loads the existing employee list of a company at once from a CSV file. The page is in the **HRM menu** ("Import employees"); the data is written in **master data**
(SharedMasterDataService owns the Employee aggregate; HRM only keeps a read-only snapshot that follows through events).

| Actor | Role |
|---|---|
| HR administrator | Holds `masterdata:employees:manage`; prepares the CSV, checks, imports, reads the result. |
| SharedMasterDataService (MST) | Validates the rows, creates the employees, raises `EmployeeHired` (source `import`) per created row. |
| HRMService | Updates its `EmployeeSnapshot` from `EmployeeHired` (Kafka `mst-topic`); the HR pickers show the new people a moment later. |
| NotificationService | Sends the welcome e-mail for a **hired** employee; for source `import` it sends nothing. |

## 2. Process (main flow)

Same dialog as the product import: template, optional **Check only**, progress bar and counters, skipped / invalid rows listed with line and reason, summary, problem rows downloadable.
Creating a person here does **not** create a login (Identity is not touched).

## 3. Requirements and acceptance criteria

| ID | Requirement | Acceptance criterion |
|---|---|---|
| EI1 | Import up to 20 000 rows / 25 MB streamed, committed in batches of 200 (the rows and their outbox events commit together). | 150 rows are created in about 1.4 s; each appears in MST and, after the Kafka consumer, in HRM. |
| EI2 | Required: first name, last name (up to 100 characters), department code, hire date (`yyyy-MM-dd` or `dd/MM/yyyy`, not before 1950, at most 30 days ahead). | A missing name, an unreadable date (`15-01-2025`) or a date too far ahead is an `error` row with the reason. |
| EI3 | The department is given by **code** and must exist; the position code is optional but must exist when given. | "Unknown department code 'NO-SUCH'." / "Unknown position code 'X'." |
| EI4 | An e-mail (valid, up to 256) already used by an employee of the shop, or repeated in the file, is skipped; the e-mail is compared without case and stored in lower case. | "An employee with this e-mail already exists." / "The e-mail repeats an earlier row of this file." |
| EI5 | The employee number may be given (letters, digits, `-`, up to 50) or is **generated**: `EMP-{hire year}-{5 digits}`, continuing above the highest generated number of the shop and never repeating inside the file. A given number that exists or repeats is skipped. | Rows without a number get `EMP-2025-00001`, `...00002`; re-running the same file skips the rows whose number or e-mail exists. |
| EI6 | `employmentType` FullTime (default), PartTime or Contract; phone up to 20 characters. | An unknown type or a numeric type is an `error` row. |
| EI7 | An imported employee gets **no welcome e-mail**. | `EmployeeHired.Source = "import"`; the Notification handler skips it (verified: 0 welcome messages after 150 imported rows). |
| EI8 | Check only writes nothing; a wrong header is HTTP 400; one import per user at a time. | As the product import. |

## 4. Business rules

* **Columns** (matched without case, spaces or underscores): `employeeNumber`, `firstName`*, `lastName`*, `email`, `phone`, `departmentCode`*, `positionCode`, `dateOfHire`*, `employmentType`.
* New employees are **Active** (status 0). Skipped, not updated: existing employees are never changed by an import.
* Generated numbers use five digits so they cannot collide with the random four-digit numbers of the hire form (`EMP-2026-1234`, which has no uniqueness check: R149).
* There is no manager, branch or salary data in this import (the Employee aggregate has no manager field; salary terms belong to HRM payroll).

## 5. Use cases

UC1 clean import; UC2 import with problems (skip / fail rows, fix, import again); UC3 header refused (400); UC4 cancel; UC5 server stops after a failed batch; UC6 re-import of the same file (all skipped). Details as in `Product/ProductImport.md` section 5.

## 6. Data and integration

* Written in MasterDataDB: `Employees` and `OutboxMessages` (`EmployeeHired` with `Source`). No migration.
* `EmployeeHiredEvent` has a new `Source` property (`hire` default, `import`); consumers that do not know it ignore it (Newtonsoft). Notification: `EmployeeHiredIntegrationEvent.Source`; `Source = import` -> no welcome message. HRM and Reporting are unaffected.
* Endpoints (Gateway paths `/mst/api/v1/masterdata/employees/...`): `POST import`, `GET import/{importId}/events`, `GET import/template`; permission `masterdata:employees:manage`.

## 7. Assumptions and open issues

HR has the department codes (a department without a code cannot be referenced). No unique index on `Employees.Email` / `EmployeeNumber` (a concurrent hire could still duplicate). The HRM list lags by the Kafka consumer. See `docs/DEFERRED-ISSUES.md` R146-R153.

## 8. UAT

1. Import the template with the department codes of the shop. 2. Import it again: all skipped. 3. A row with an unknown department code, a bad date and a repeated e-mail: three reasons. 4. Check only: nothing saved. 5. After the import the people appear in HRM pickers (Leave, Payroll). 6. No welcome e-mail is sent to the imported addresses.

## 9. Verification record (2026-11-06, Docker, glcheck, throw-away data)

Unit tests: MST 21 (new project `ERP.MST.UnitTests`), Notification 85 (3 new for the welcome skip). Live: department `ZZ-DEPT` created; 150 employees imported with generated numbers `EMP-2025-00001...`; HRM `employees?search=ZZ` returned them; a second file reported an existing e-mail and an existing number as skipped, an unknown department and a bad date as errors, created the one good row (`EMP-2025-00151`); wrong header -> 400. **Not verified:** the Admin page in a real browser; a user without the permission; a 20 000-row file. Throw-away rows left on glcheck: 151 employees `ZZ*` and the department `ZZ-DEPT` (no delete endpoint for employees).
