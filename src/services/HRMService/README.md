# HRM Service

Human Resource Management including employee records, attendance, leave management, and payroll processing for the ERP E-Commerce Solution.

## Overview

The HRM Service manages all employee lifecycle and compensation:
- **Employees** — Employee master records, contracts, employment history
- **Departments** — Organizational structure and hierarchy
- **Attendance** — Clock-in/out, attendance tracking
- **Leave Management** — Leave types, requests, approvals, accrual
- **Payroll** — Salary structure, payslips, tax deductions, benefits

## Core Domain Model

### Aggregates

**Employee** (Aggregate Root)
- EmployeeId, EmployeeNumber, FirstName, LastName
- Email, Phone, DateOfBirth, DateOfHire
- DepartmentId, ManagerId (parent employee)
- EmploymentStatus (Active, OnLeave, Terminated, Suspended)
- EmploymentType (FullTime, PartTime, Contract, Intern)
- List<Attendance> children
- List<LeaveRequest> children

**Department** (Aggregate Root)
- DepartmentId, DepartmentName
- ParentDepartmentId (for hierarchy)
- ManagerId (head of department)
- Budget allocation

**Attendance** (Entity, child of Employee)
- AttendanceId, AttendanceDate
- ClockInTime, ClockOutTime
- HoursWorked, Status (Present, Absent, Late, EarlyLeave)

**LeaveRequest** (Aggregate Root)
- LeaveRequestId, LeaveRequestNumber
- EmployeeId
- LeaveTypeId (Annual, Sick, Maternity, Bereavement, Unpaid)
- StartDate, EndDate, DaysRequested
- LeaveStatus (Pending, Approved, Rejected, Cancelled)
- ApprovedBy, ApprovalDate
- Reason

**PaySlip** (Aggregate Root)
- PaySlipId, PaySlipNumber
- EmployeeId, PayPeriodId
- GrossSalary, Deductions (Tax, SSF, Insurance)
- NetSalary, PaymentStatus (Draft, Generated, Paid, Disputed)
- PaymentDate

**SalaryStructure** (Entity)
- StructureId, EmployeeId
- BaseSalary, HourlyRate
- AllowanceList (housing, food, transport)
- DeductionList (tax, SSF)
- EffectiveDate

## Key Business Rules

1. Employee cannot submit leave request for past dates
2. Leave request cannot exceed available balance for that leave type
3. Manager approval required for leave requests
4. Payroll can only be processed for approved attendance
5. PaySlip is immutable once marked as Paid
6. Department head must be reassigned before deleting department

## Database

**Database Name:** `HrmDB`

**Key Tables:**
- Employees, Departments
- AttendanceRecords, LeaveRequests, LeaveTypes
- SalaryStructures, Allowances, Deductions
- PaySlips, PayrollPeriods
- OutboxMessages

## Integration Events

### Published Events

**EmployeeHiredIntegrationEvent**
- Published when new employee is added to system
- Subscribers: NotificationService, ReportingService, IdentityService

**EmployeeTerminatedIntegrationEvent**
- Published when employee is terminated
- Subscribers: NotificationService, ReportingService, IdentityService

**PaySlipGeneratedIntegrationEvent**
- Published when payroll is processed
- Subscribers: NotificationService, AccountingService, ReportingService

**LeaveApprovedIntegrationEvent**
- Published when leave request is approved
- Subscribers: NotificationService

### Consumed Events

**UserCreatedIntegrationEvent** (from Identity)
→ Link UserId to Employee record if UserType=Employee

## API Endpoints

```
GET    /api/v1/employees                   - List employees
POST   /api/v1/employees                   - Create employee
GET    /api/v1/employees/{id}              - Employee detail
PUT    /api/v1/employees/{id}              - Update employee
GET    /api/v1/employees/{id}/attendance   - Attendance history

GET    /api/v1/departments                 - List departments
POST   /api/v1/departments                 - Create department
GET    /api/v1/departments/{id}            - Department detail

POST   /api/v1/attendance/clock-in         - Clock in
POST   /api/v1/attendance/clock-out        - Clock out
GET    /api/v1/attendance/today            - Today's attendance

GET    /api/v1/leave-requests              - List requests
POST   /api/v1/leave-requests              - Create request
GET    /api/v1/leave-requests/{id}         - Request detail
POST   /api/v1/leave-requests/{id}/approve - Approve request
POST   /api/v1/leave-requests/{id}/reject  - Reject request

GET    /api/v1/salary-structures           - List structures
POST   /api/v1/salary-structures           - Create structure

GET    /api/v1/payroll/periods             - List payroll periods
POST   /api/v1/payroll/process             - Process payroll
GET    /api/v1/payslips                    - List payslips
GET    /api/v1/payslips/{id}               - Payslip detail
```

## Dependencies

**Services that depend on HRM Service:**
- NotificationService (consumes events)
- ReportingService (consumes events)

**Services HRM depends on:**
- IdentityService (user linking)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — HrmDB
- **Kafka** — Event streaming
- **MediatR** — CQRS

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
