-- ============================================================
-- HrmDB Migration — Remove Employee/Department ownership
-- Department and Employee master data moves to MasterDataDB
-- (SharedMasterDataService, port 5015, code MST). Run this AFTER
-- src/Database/MasterData/001_CreateMasterDataDB.sql has been applied
-- and executed.
--
-- IMPORTANT — manual data migration step (not automated by this script):
-- before running this script in an environment with real data, copy the
-- existing rows of HrmDB.Employees / HrmDB.Departments into
-- MasterDataDB.Employees / MasterDataDB.Departments (preserving Ids), so
-- HRM's EmployeeSnapshots projection and MasterDataService's own Employee/
-- Department aggregates start from the same data. This script only
-- changes schema — it does not move data.
-- ============================================================

USE HrmDB;
GO

-- Drop FKs from tables that stay in HRM but referenced the tables being removed
IF OBJECT_ID('FK_Attendance_Employee', 'F') IS NOT NULL
    ALTER TABLE AttendanceLogs DROP CONSTRAINT FK_Attendance_Employee;
GO
IF OBJECT_ID('FK_Leave_Employee', 'F') IS NOT NULL
    ALTER TABLE LeaveRequests DROP CONSTRAINT FK_Leave_Employee;
GO
IF OBJECT_ID('FK_LeaveBalance_Employee', 'F') IS NOT NULL
    ALTER TABLE LeaveBalances DROP CONSTRAINT FK_LeaveBalance_Employee;
GO
IF OBJECT_ID('FK_PaySlip_Employee', 'F') IS NOT NULL
    ALTER TABLE PaySlips DROP CONSTRAINT FK_PaySlip_Employee;
GO

-- ============================================================
-- Local read-only projection of MasterDataService's Employee data,
-- populated by consuming EmployeeHiredEvent/EmployeeTerminatedEvent/
-- EmployeeTransferredEvent from the "mst-topic" Kafka topic. Not an
-- aggregate root — never written to except by the Kafka consumer.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmployeeSnapshots')
CREATE TABLE EmployeeSnapshots (
    EmployeeId       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId         UNIQUEIDENTIFIER NOT NULL,
    FullName         NVARCHAR(200)    NOT NULL,
    EmploymentStatus INT              NOT NULL,
    DepartmentId     UNIQUEIDENTIFIER NOT NULL,
    UpdatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_EmployeeSnapshots_TenantId ON EmployeeSnapshots (TenantId, EmploymentStatus);
GO

-- Departments/Employees now owned by MasterDataDB — drop the local copies
-- (data must already be migrated per the note above).
DROP TABLE IF EXISTS Employees;
GO
DROP TABLE IF EXISTS Departments;
GO
