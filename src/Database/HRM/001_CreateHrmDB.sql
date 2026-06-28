-- ============================================================
-- HrmDB Schema — HRMService (Port 5007, Code: HRM)
-- Employees, Departments, Attendance, Leave, Payroll
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'HrmDB')
    CREATE DATABASE HrmDB;
GO

USE HrmDB;
GO

-- ============================================================
-- Departments
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Departments')
CREATE TABLE Departments (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Code                NVARCHAR(50)     NOT NULL,
    Name                NVARCHAR(200)    NOT NULL,
    ParentDepartmentId  UNIQUEIDENTIFIER NULL,
    ManagerId           UNIQUEIDENTIFIER NULL, -- Employee.Id
    CostCenter          NVARCHAR(50)     NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Dept_Parent FOREIGN KEY (ParentDepartmentId) REFERENCES Departments(Id)
);
CREATE UNIQUE INDEX IX_Departments_Code ON Departments (Code, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Departments_TenantId    ON Departments (TenantId, IsActive);
GO

-- ============================================================
-- Employees
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Employees')
CREATE TABLE Employees (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    EmployeeNumber      NVARCHAR(50)     NOT NULL,
    FirstName           NVARCHAR(100)    NOT NULL,
    LastName            NVARCHAR(100)    NOT NULL,
    FullName            AS (FirstName + ' ' + LastName) PERSISTED,
    Email               NVARCHAR(256)    NULL,
    Phone               NVARCHAR(30)     NULL,
    DateOfBirth         DATE             NULL,
    Gender              INT              NULL, -- 0=Male, 1=Female, 2=Other
    NationalId          NVARCHAR(50)     NULL, -- passport/national ID
    DepartmentId        UNIQUEIDENTIFIER NOT NULL,
    ManagerId           UNIQUEIDENTIFIER NULL, -- Employee.Id (direct manager)
    Position            NVARCHAR(200)    NULL,
    JobTitle            NVARCHAR(200)    NULL,
    DateOfHire          DATE             NOT NULL,
    DateOfTermination   DATE             NULL,
    EmploymentStatus    INT              NOT NULL DEFAULT 0, -- 0=Active, 1=OnLeave, 2=Suspended, 3=Terminated
    EmploymentType      INT              NOT NULL DEFAULT 0, -- 0=FullTime, 1=PartTime, 2=Contract, 3=Intern
    BasicSalary         DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    CurrencyCode        NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    UserId              UNIQUEIDENTIFIER NULL, -- linked Identity user
    -- Address
    Street              NVARCHAR(255)    NULL,
    City                NVARCHAR(100)    NULL,
    State               NVARCHAR(100)    NULL,
    PostalCode          NVARCHAR(20)     NULL,
    Country             NVARCHAR(100)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Employee_Dept    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id),
    CONSTRAINT FK_Employee_Manager FOREIGN KEY (ManagerId)    REFERENCES Employees(Id)
);
CREATE UNIQUE INDEX IX_Employees_Number  ON Employees (EmployeeNumber, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Employees_Email          ON Employees (Email, TenantId) WHERE Email IS NOT NULL;
CREATE INDEX IX_Employees_Dept           ON Employees (DepartmentId, EmploymentStatus);
CREATE INDEX IX_Employees_TenantId       ON Employees (TenantId, EmploymentStatus, DateOfHire DESC);
CREATE INDEX IX_Employees_UserId         ON Employees (UserId) WHERE UserId IS NOT NULL;
GO

-- ============================================================
-- Attendance Logs
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttendanceLogs')
CREATE TABLE AttendanceLogs (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    EmployeeId      UNIQUEIDENTIFIER NOT NULL,
    CheckInAt       DATETIME2        NOT NULL,
    CheckOutAt      DATETIME2        NULL,
    WorkHours       AS (CASE WHEN CheckOutAt IS NOT NULL
                        THEN CAST(DATEDIFF(MINUTE, CheckInAt, CheckOutAt) / 60.0 AS DECIMAL(5,2))
                        ELSE NULL END) PERSISTED,
    OvertimeHours   DECIMAL(5, 2)    NULL,
    Source          INT              NOT NULL DEFAULT 0, -- 0=Manual, 1=Biometric, 2=App, 3=Web
    Notes           NVARCHAR(500)    NULL,
    ApprovedBy      NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Attendance_Employee FOREIGN KEY (EmployeeId) REFERENCES Employees(Id)
);
CREATE INDEX IX_Attendance_Employee ON AttendanceLogs (EmployeeId, CheckInAt DESC);
CREATE INDEX IX_Attendance_TenantId ON AttendanceLogs (TenantId, CheckInAt DESC);
GO

-- ============================================================
-- Leave Requests
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeaveRequests')
CREATE TABLE LeaveRequests (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    EmployeeId          UNIQUEIDENTIFIER NOT NULL,
    LeaveType           INT              NOT NULL, -- 0=Annual, 1=Sick, 2=Maternity, 3=Paternity, 4=Unpaid, 5=Other
    StartDate           DATE             NOT NULL,
    EndDate             DATE             NOT NULL,
    Days                DECIMAL(5, 1)    NOT NULL,
    HalfDay             BIT              NOT NULL DEFAULT 0,
    Reason              NVARCHAR(1000)   NULL,
    Status              INT              NOT NULL DEFAULT 0, -- 0=Pending, 1=Approved, 2=Rejected, 3=Cancelled
    ApprovedByUserId    NVARCHAR(100)    NULL,
    ApprovedAt          DATETIME2        NULL,
    RejectionReason     NVARCHAR(500)    NULL,
    Attachments         NVARCHAR(MAX)    NULL, -- JSON array of file URLs
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Leave_Employee FOREIGN KEY (EmployeeId) REFERENCES Employees(Id)
);
CREATE INDEX IX_Leave_EmployeeId ON LeaveRequests (EmployeeId, Status, StartDate DESC);
CREATE INDEX IX_Leave_TenantId   ON LeaveRequests (TenantId, Status, StartDate DESC);
CREATE INDEX IX_Leave_Pending    ON LeaveRequests (Status, CreatedAt DESC) WHERE Status = 0;
GO

-- ============================================================
-- Leave Balances (per employee per year)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeaveBalances')
CREATE TABLE LeaveBalances (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    EmployeeId  UNIQUEIDENTIFIER NOT NULL,
    Year        INT              NOT NULL,
    LeaveType   INT              NOT NULL,
    TotalDays   DECIMAL(5, 1)   NOT NULL DEFAULT 0,
    UsedDays    DECIMAL(5, 1)   NOT NULL DEFAULT 0,
    RemainingDays AS (TotalDays - UsedDays) PERSISTED,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_LeaveBalance_Employee FOREIGN KEY (EmployeeId) REFERENCES Employees(Id)
);
CREATE UNIQUE INDEX IX_LeaveBalance_Key ON LeaveBalances (EmployeeId, Year, LeaveType);
GO

-- ============================================================
-- Pay Slips
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaySlips')
CREATE TABLE PaySlips (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    EmployeeId      UNIQUEIDENTIFIER NOT NULL,
    PeriodStart     DATE             NOT NULL,
    PeriodEnd       DATE             NOT NULL,
    BasicSalary     DECIMAL(18, 2)   NOT NULL,
    Allowances      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    AllowanceDetails NVARCHAR(MAX)   NULL, -- JSON: [{name, amount}]
    Deductions      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    DeductionDetails NVARCHAR(MAX)   NULL, -- JSON: [{name, amount}]
    TaxAmount       DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    SocialInsurance DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    NetSalary       AS (BasicSalary + Allowances - Deductions - TaxAmount - SocialInsurance) PERSISTED,
    CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Issued, 2=PaidOut
    IssuedAt        DATETIME2        NULL,
    IssuedBy        NVARCHAR(256)    NULL,
    PaidAt          DATETIME2        NULL,
    PaymentRef      NVARCHAR(100)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PaySlip_Employee FOREIGN KEY (EmployeeId) REFERENCES Employees(Id)
);
CREATE UNIQUE INDEX IX_PaySlips_Period ON PaySlips (EmployeeId, PeriodStart, PeriodEnd);
CREATE INDEX IX_PaySlips_TenantId      ON PaySlips (TenantId, PeriodStart DESC, Status);
GO

-- ============================================================
-- Outbox Messages
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutboxMessages')
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
CREATE INDEX IX_OutboxMessages_Unprocessed ON OutboxMessages (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL;
GO
