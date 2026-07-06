-- ============================================================
-- HrmDB Schema Fix — HRMService (Port 5007, Code: HRM)
-- Migration 003: Fixes column mismatches and creates missing tables.
-- All DDL is guarded with IF NOT EXISTS — safe to run multiple times.
-- ============================================================

USE HrmDB;
GO

-- ============================================================
-- LeaveRequests — entity uses LeaveRequestNumber, LeaveTypeId,
--                 DaysRequested, LeaveStatus; DB uses LeaveType, Days, Status
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'LeaveRequestNumber')
    ALTER TABLE LeaveRequests ADD LeaveRequestNumber NVARCHAR(50) NOT NULL DEFAULT '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'LeaveTypeId')
    ALTER TABLE LeaveRequests ADD LeaveTypeId INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'DaysRequested')
    ALTER TABLE LeaveRequests ADD DaysRequested INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'LeaveStatus')
    ALTER TABLE LeaveRequests ADD LeaveStatus INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'IsDeleted')
    ALTER TABLE LeaveRequests ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'Description')
    ALTER TABLE LeaveRequests ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'RowVersion')
    ALTER TABLE LeaveRequests ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- PaySlips — entity uses PaySlipNumber, Year, Month, GrossSalary,
--            OvertimeHours, OvertimePay, PaymentDate, PaymentStatus;
--            DB uses PeriodStart/PeriodEnd, BasicSalary, Status
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'PaySlipNumber')
    ALTER TABLE PaySlips ADD PaySlipNumber NVARCHAR(50) NOT NULL DEFAULT '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'Year')
    ALTER TABLE PaySlips ADD [Year] INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'Month')
    ALTER TABLE PaySlips ADD [Month] INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'GrossSalary')
    ALTER TABLE PaySlips ADD GrossSalary DECIMAL(18, 2) NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'OvertimeHours')
    ALTER TABLE PaySlips ADD OvertimeHours DECIMAL(6, 2) NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'OvertimePay')
    ALTER TABLE PaySlips ADD OvertimePay DECIMAL(18, 2) NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'PaymentDate')
    ALTER TABLE PaySlips ADD PaymentDate DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'PaymentStatus')
    ALTER TABLE PaySlips ADD PaymentStatus INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'IsDeleted')
    ALTER TABLE PaySlips ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'Description')
    ALTER TABLE PaySlips ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'RowVersion')
    ALTER TABLE PaySlips ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- AttendanceLogs → rename to AttendanceRecords (matches DbSet name)
-- Then fix mismatched column names and add missing columns.
-- Entity: Date, CheckInTime, CheckOutTime, TotalHours, OvertimeHours,
--         Status, WorkShiftId, Notes
-- DB had: CheckInAt (→CheckInTime), CheckOutAt (→CheckOutTime),
--         WorkHours (→TotalHours), OvertimeHours (matches)
-- ============================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttendanceLogs')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttendanceRecords')
        EXEC sp_rename 'AttendanceLogs', 'AttendanceRecords';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'CheckInAt')
    EXEC sp_rename 'AttendanceRecords.CheckInAt', 'CheckInTime', 'COLUMN';
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'CheckOutAt')
    EXEC sp_rename 'AttendanceRecords.CheckOutAt', 'CheckOutTime', 'COLUMN';
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'WorkHours')
    EXEC sp_rename 'AttendanceRecords.WorkHours', 'TotalHours', 'COLUMN';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'Date')
    ALTER TABLE AttendanceRecords ADD [Date] DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'Status')
    ALTER TABLE AttendanceRecords ADD [Status] INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'WorkShiftId')
    ALTER TABLE AttendanceRecords ADD WorkShiftId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'CreatedBy')
    ALTER TABLE AttendanceRecords ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'UpdatedBy')
    ALTER TABLE AttendanceRecords ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'UpdatedAt')
    ALTER TABLE AttendanceRecords ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'IsDeleted')
    ALTER TABLE AttendanceRecords ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'Description')
    ALTER TABLE AttendanceRecords ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'RowVersion')
    ALTER TABLE AttendanceRecords ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- EmployeeSnapshots — DB uses EmployeeId as PK column name;
--                     EF expects Id (from BaseEntity).
--                     Also missing: EmployeeNumber, Email, CreatedAt,
--                                   CreatedBy, UpdatedBy, IsDeleted,
--                                   Description, RowVersion
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'Id')
    ALTER TABLE EmployeeSnapshots ADD Id UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

-- Populate Id from EmployeeId for any existing rows
UPDATE EmployeeSnapshots SET Id = EmployeeId WHERE Id = '00000000-0000-0000-0000-000000000000';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'EmployeeNumber')
    ALTER TABLE EmployeeSnapshots ADD EmployeeNumber NVARCHAR(50) NOT NULL DEFAULT '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'Email')
    ALTER TABLE EmployeeSnapshots ADD Email NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'CreatedAt')
    ALTER TABLE EmployeeSnapshots ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'CreatedBy')
    ALTER TABLE EmployeeSnapshots ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'UpdatedBy')
    ALTER TABLE EmployeeSnapshots ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'IsDeleted')
    ALTER TABLE EmployeeSnapshots ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'Description')
    ALTER TABLE EmployeeSnapshots ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'RowVersion')
    ALTER TABLE EmployeeSnapshots ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- WorkShifts — table missing entirely; create it
-- Entity: Name, StartTime (TimeSpan→TIME), EndTime (TIME), IsActive
-- ShiftHours is a computed property (ignored by EF)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WorkShifts')
CREATE TABLE WorkShifts (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(100)    NOT NULL,
    StartTime   TIME             NOT NULL,
    EndTime     TIME             NOT NULL,
    IsActive    BIT              NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy   NVARCHAR(256)    NULL,
    UpdatedBy   NVARCHAR(256)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    Description NVARCHAR(MAX)    NULL,
    RowVersion  ROWVERSION
);
GO

SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkShifts_Name' AND object_id = OBJECT_ID('WorkShifts'))
    CREATE UNIQUE INDEX IX_WorkShifts_Name ON WorkShifts (Name) WHERE IsDeleted = 0;
GO
