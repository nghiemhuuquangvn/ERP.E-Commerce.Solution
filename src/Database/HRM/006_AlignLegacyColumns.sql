-- ============================================================
-- HrmDB Migration 006 — columns the entities do not write (ROADMAP P0-9)
--  * LeaveRequests.LeaveType/Days: superseded by LeaveTypeId/DaysRequested (added by a later fix script).
--  * AttendanceRecords.CheckInTime: the entity allows a record without a check-in (absence, leave).
--  * PaySlips: NetSalary stops being a computed column (the entity writes it); BasicSalary is not written
--    (GrossSalary is); PeriodStart/PeriodEnd are derived from Year/Month,
--    so they become persisted computed columns (the per-employee unique index is rebuilt on them).
--  * EmployeeSnapshots: the primary key was EmployeeId but the entity's key is Id (equal in every row).
--    Id becomes the primary key and EmployeeId a persisted alias of it.
-- Idempotent.
-- ============================================================

USE HrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- PaySlips.NetSalary was a computed column (BasicSalary + Allowances - ...) but the entity computes and writes
-- it (gross + overtime - deductions): INSERT failed with "cannot be modified because it is a computed column".
-- It becomes a stored value; existing amounts are copied.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'NetSalary' AND is_computed = 1)
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'NetSalary_New')
    ALTER TABLE PaySlips ADD NetSalary_New DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PaySlips_NetSalary_New DEFAULT 0;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'NetSalary' AND is_computed = 1)
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'NetSalary_New')
BEGIN
    EXEC (N'UPDATE PaySlips SET NetSalary_New = NetSalary');
    ALTER TABLE PaySlips DROP COLUMN NetSalary;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'NetSalary_New')
    EXEC sp_rename 'PaySlips.NetSalary_New', 'NetSalary', 'COLUMN';
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'LeaveType' AND is_nullable = 0)
    ALTER TABLE LeaveRequests ALTER COLUMN LeaveType INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequests') AND name = 'Days' AND is_nullable = 0)
    ALTER TABLE LeaveRequests ALTER COLUMN Days DECIMAL(5, 1) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'CheckInTime' AND is_nullable = 0)
    ALTER TABLE AttendanceRecords ALTER COLUMN CheckInTime DATETIME2 NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'BasicSalary' AND is_nullable = 0)
    ALTER TABLE PaySlips ALTER COLUMN BasicSalary DECIMAL(18, 2) NULL;
GO

-- PaySlips.PeriodStart / PeriodEnd -> computed from Year / Month.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'PeriodStart' AND is_computed = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaySlips_Period' AND object_id = OBJECT_ID('PaySlips')) DROP INDEX IX_PaySlips_Period ON PaySlips;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaySlips_TenantId' AND object_id = OBJECT_ID('PaySlips')) DROP INDEX IX_PaySlips_TenantId ON PaySlips;
    ALTER TABLE PaySlips DROP COLUMN PeriodStart;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'PeriodEnd' AND is_computed = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaySlips_Period' AND object_id = OBJECT_ID('PaySlips')) DROP INDEX IX_PaySlips_Period ON PaySlips;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaySlips_TenantId' AND object_id = OBJECT_ID('PaySlips')) DROP INDEX IX_PaySlips_TenantId ON PaySlips;
    ALTER TABLE PaySlips DROP COLUMN PeriodEnd;
END
GO
IF COL_LENGTH('PaySlips', 'PeriodStart') IS NULL
    ALTER TABLE PaySlips ADD PeriodStart AS DATEFROMPARTS([Year], [Month], 1) PERSISTED;
GO
IF COL_LENGTH('PaySlips', 'PeriodEnd') IS NULL
    ALTER TABLE PaySlips ADD PeriodEnd AS EOMONTH(DATEFROMPARTS([Year], [Month], 1)) PERSISTED;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaySlips_Period' AND object_id = OBJECT_ID('PaySlips'))
    CREATE UNIQUE INDEX IX_PaySlips_Period ON PaySlips (EmployeeId, PeriodStart, PeriodEnd);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaySlips_TenantId' AND object_id = OBJECT_ID('PaySlips'))
    CREATE INDEX IX_PaySlips_TenantId ON PaySlips (TenantId, PeriodStart, Status);
GO

-- EmployeeSnapshots: Id is the key; EmployeeId an alias of it.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeSnapshots') AND name = 'EmployeeId' AND is_computed = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM EmployeeSnapshots WHERE Id IS NULL OR Id <> EmployeeId)
        THROW 50026, 'EmployeeSnapshots.Id differs from EmployeeId in some rows; fix the data before running HRM 006.', 1;

    DECLARE @pk SYSNAME = (SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('EmployeeSnapshots') AND type = 'PK');
    IF @pk IS NOT NULL EXEC (N'ALTER TABLE EmployeeSnapshots DROP CONSTRAINT ' + @pk);
    ALTER TABLE EmployeeSnapshots ALTER COLUMN Id UNIQUEIDENTIFIER NOT NULL;
    ALTER TABLE EmployeeSnapshots ADD CONSTRAINT PK_EmployeeSnapshots PRIMARY KEY (Id);
    ALTER TABLE EmployeeSnapshots DROP COLUMN EmployeeId;
END
GO
IF COL_LENGTH('EmployeeSnapshots', 'EmployeeId') IS NULL
    ALTER TABLE EmployeeSnapshots ADD EmployeeId AS Id PERSISTED;
GO
