# Employee

**Service:** SharedMasterDataService (Port 5015, Code: MST)
**Database:** MasterDataDB

## Purpose

Employee master record: identity, department/position assignment, and employment
status/type. HR-transactional processes (attendance, leave, payroll) stay in
HRMService and reference `EmployeeId` only — they do not own or duplicate this
record.

## Fields

| Field | Type | Notes |
|-------|------|-------|
| EmployeeNumber | string | Format `EMP-{year}-{random4}` |
| FirstName / LastName | string | |
| Email / Phone | string? | |
| DateOfHire | DateTime | |
| DepartmentId | Guid | |
| PositionId | Guid? | |
| EmploymentStatus | int | 0=Active, 1=OnLeave, 2=Terminated |
| EmploymentType | int | 0=FullTime, 1=PartTime, 2=Contract |

## Workflow

1. `UserCreated` (IdentityService) with `IsEmployee=true` → SharedMasterDataService
   auto-creates an Employee record (`UserCreatedEventHandler`).
2. Hire / terminate / transfer / update-profile via
   `POST/PUT /api/v1/masterdata/employees/...`.
3. HRMService consumes `EmployeeHiredEvent`/`EmployeeTerminatedEvent`/
   `EmployeeTransferredEvent` (topic `mst-topic`) to keep a local, read-only
   `EmployeeSnapshot` projection — used by leave/payroll handlers so they don't need a
   synchronous cross-service call to check employee status.
4. When HRMService approves a leave request (`LeaveApprovedEvent`, topic `hrm-topic`),
   SharedMasterDataService consumes it and sets the canonical Employee to `OnLeave` —
   closing the loop back to the owning aggregate.

## Events

Published: `EmployeeHiredEvent`, `EmployeeTerminatedEvent`, `EmployeeTransferredEvent`
(topic `mst-topic`).
Consumed: `UserCreated` (IdentityService), `LeaveApprovedEvent` (HRMService, topic
`hrm-topic`).

## Why this moved out of HRMService

HRMService originally owned Employee/Department directly. Employee/Department master
data is needed by more than just HR workflows (e.g. any service that needs to know
"which department owns this," "who manages this," or basic employee identity), so it
was extracted into SharedMasterDataService. See CLAUDE.md's Service Boundaries section
and `docs/architecture/domain-map.md`'s User Provisioning Cycle for the updated event
flow.
