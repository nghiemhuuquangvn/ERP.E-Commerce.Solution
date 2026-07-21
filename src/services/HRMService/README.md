# HRM Service

Attendance tracking, leave management, and payroll processing for the ERP
E-Commerce Solution.

## Overview

The HRM Service manages the employee-lifecycle transactions that sit on top of
employee master data:
- **Attendance** — Check-in/check-out, absence marking, monthly summaries.
- **Leave Management** — Leave requests, approval/rejection, cancellation.
- **Payroll** — Pay slip generation (single employee or a whole month's batch).
- **Work Shifts** — Named shift definitions (start/end time) attendance can be
  checked in against.

Employee and Department master data are **not** owned here — they live in
SharedMasterDataService (port 5015, Code: MST). HRM keeps a local, read-only
`EmployeeSnapshot` projection (`EmployeeNumber`, `FullName`, `Email`,
`EmploymentStatus`, `DepartmentId`), kept in sync via a Kafka consumer
(`MasterDataEventConsumer`) reacting to MST's `EmployeeHired` /
`EmployeeTerminated` / `EmployeeTransferred` events. This snapshot exists so
attendance/leave/payroll handlers — and their Admin UI pickers — don't need a
synchronous cross-service call just to resolve an employee's name. Do not add
Employee or Department write endpoints here; that logic belongs in MST.

## Core Domain Model

### Aggregates

**AttendanceRecord** (Aggregate Root)
- `EmployeeId`, `Date`, `CheckInTime`, `CheckOutTime`
- `TotalHours`, `OvertimeHours`, `Status` (`AttendanceStatus`)
- `WorkShiftId` (optional), `Notes`
- One record per employee per date, enforced by a unique DB index.

**LeaveRequest** (Aggregate Root)
- `LeaveRequestNumber`, `EmployeeId`, `LeaveTypeId` (`LeaveType`)
- `StartDate`, `EndDate`, `DaysRequested`, `LeaveStatus` (`LeaveStatus`), `Reason`

**PaySlip** (Aggregate Root)
- `PaySlipNumber`, `EmployeeId`, `Year`, `Month` (`PayPeriod` is derived as
  `YYYY-MM`)
- `GrossSalary`, `Deductions`, `OvertimeHours`, `OvertimePay`, `NetSalary`
  (`= GrossSalary + OvertimePay - Deductions`)
- `PaymentDate` (last day of the pay month), `PaymentStatus` (`PaymentStatus`)

**WorkShift** (Aggregate Root)
- `Name`, `StartTime`, `EndTime` (`HH:mm` strings), `ShiftHours`, `IsActive`

### Read-only projection

**EmployeeSnapshot** — `EmployeeNumber`, `FullName`, `Email`, `EmploymentStatus`,
`DepartmentId`. Synced from SharedMasterDataService; never written to directly by
a client of this service.

### Enums (`ERP.HRM.Domain.Enums.HrmEnums`)

| Enum | Values |
|---|---|
| `AttendanceStatus` | Present=0, Absent=1, Late=2, HalfDay=3, OnLeave=4, Holiday=5 |
| `LeaveType` | Annual=0, Sick=1, Unpaid=2, Maternity=3, Paternity=4 |
| `LeaveStatus` | Pending=0, Approved=1, Rejected=2, Cancelled=3 |
| `PaymentStatus` | Pending=0, Paid=1, Cancelled=2 |
| `EmploymentStatus` | Active=0, OnLeave=1, Terminated=2 (mirrors MST's enum, used to filter the local snapshot) |
| `EmploymentType` | FullTime=0, PartTime=1, Contract=2 (denormalized onto the snapshot; not owned here) |

## Key Business Rules

1. One attendance record per employee per date (DB-enforced uniqueness).
2. Checking in more than 15 minutes after the assigned shift's start time marks
   the record `Late` instead of `Present`.
3. Check-out time must be after check-in time; standard shift length is 8 hours
   — hours worked beyond that count as overtime, and fewer than 4 hours worked
   marks the record `HalfDay`.
4. A leave request can only be approved or rejected while `Pending`; an
   `Approved` or `Rejected` request can no longer be cancelled.
5. A pay slip's deductions must be less than its gross pay; net salary is always
   computed as `GrossSalary + OvertimePay - Deductions`.
6. Payroll processing (`POST /payroll/process`) generates one pay slip per call
   for a given year/month batch; generating a single pay slip
   (`POST /payroll/generate`) is a separate, explicit action.

## Database

**Database Name:** `HrmDB`

**Tables:** `EmployeeSnapshots`, `AttendanceRecords`, `LeaveRequests`, `PaySlips`,
`WorkShifts`, `OutboxMessages`.

## Integration Events

### Published Events

**LeaveApprovedIntegrationEvent** — Published when a leave request is approved.
Subscribers: NotificationService.

**PaySlipGeneratedIntegrationEvent** — Published when a pay slip is generated
(single or batch). Subscribers: NotificationService, AccountingService,
ReportingService.

### Consumed Events

**EmployeeHiredIntegrationEvent**, **EmployeeTerminatedIntegrationEvent**,
**EmployeeTransferredIntegrationEvent** (from SharedMasterDataService, via
`MasterDataEventConsumer`) → keep the local `EmployeeSnapshot` projection in sync.

## API Endpoints

```
GET    /api/v1/hrm/employees                          - List employee snapshots (search/department/status filters)
GET    /api/v1/hrm/employees/{id}                      - Get one employee snapshot

GET    /api/v1/hrm/attendance                          - List attendance records (employee/date range/status filters)
GET    /api/v1/hrm/attendance/{id}                      - Get one attendance record
GET    /api/v1/hrm/attendance/employee/{id}/date/{date} - Get an employee's record for a specific date
GET    /api/v1/hrm/attendance/employee/{id}/summary     - Monthly summary (present/absent/late days, hours, overtime)
POST   /api/v1/hrm/attendance/check-in                  - Check in
POST   /api/v1/hrm/attendance/{id}/check-out            - Check out
POST   /api/v1/hrm/attendance/mark-absent               - Mark an employee absent for a date

GET    /api/v1/hrm/leave-requests                       - List leave requests (employee/status filters)
POST   /api/v1/hrm/leave-requests                       - Submit a leave request
GET    /api/v1/hrm/leave-requests/{id}                  - Get a leave request
POST   /api/v1/hrm/leave-requests/{id}/approve          - Approve
POST   /api/v1/hrm/leave-requests/{id}/reject           - Reject (reason required)
POST   /api/v1/hrm/leave-requests/{id}/cancel           - Cancel

POST   /api/v1/hrm/payroll/process                      - Process payroll for all employees for a year/month
POST   /api/v1/hrm/payroll/generate                     - Generate a single pay slip
GET    /api/v1/hrm/payslips                             - List pay slips (employee/year/month filters)
GET    /api/v1/hrm/payslips/{id}                        - Get a pay slip

GET    /api/v1/hrm/work-shifts                          - List work shifts (optional activeOnly filter)
GET    /api/v1/hrm/work-shifts/{id}                     - Get a work shift
POST   /api/v1/hrm/work-shifts                          - Create a work shift
PUT    /api/v1/hrm/work-shifts/{id}                     - Update name/start/end time
POST   /api/v1/hrm/work-shifts/{id}/deactivate          - Deactivate (no reverse "activate" endpoint)
```

All endpoints are gated by `[AuthorizePermission]` — see the controllers under
`ERP.HRM.API/Controllers/` for the exact permission string per action (e.g.
`hrm:attendance:view` vs `hrm:attendance:manage`). Leave Requests and Payroll have
only a `:manage` permission each — there is no separate `:view` permission for
those two.

## Frontend

The Admin app's `/hrm/*` pages (Attendance, Leave Requests, Payroll & Pay Slips,
Work Shifts) are the UI for this service — see
`docs/frontend/hrm-admin-guide.md` in the repo root for the pattern used there.

## Dependencies

**Services that depend on HRM Service:**
- NotificationService (consumes `LeaveApproved`/`PaySlipGenerated`)
- AccountingService (consumes `PaySlipGenerated`)
- ReportingService (consumes both, for analytics)

**Services HRM depends on:**
- SharedMasterDataService (Employee lifecycle events, synced into the local
  `EmployeeSnapshot` projection)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — HrmDB
- **Kafka** — Event streaming (consumer + outbox-based publishing)
- **MediatR** — CQRS
- **FluentValidation** — Command validation
