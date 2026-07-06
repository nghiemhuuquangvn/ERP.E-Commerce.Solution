-- ============================================================
-- HrmDB — Fix AttendanceRecords column name mismatches.
-- The original AttendanceLogs table had:
--   CheckInAt  (index-covered)
--   CheckOutAt (index-covered, referenced by computed column WorkHours)
--   WorkHours  (computed: DATEDIFF minutes CheckInAt/CheckOutAt)
-- Entity expects: CheckInTime, CheckOutTime, TotalHours
-- Strategy: drop blocking indexes/computed column, rename, recreate.
-- ============================================================

USE HrmDB;
GO

-- Drop the two indexes that reference CheckInAt
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendance_Employee' AND object_id = OBJECT_ID('AttendanceRecords'))
    DROP INDEX IX_Attendance_Employee ON AttendanceRecords;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendance_TenantId' AND object_id = OBJECT_ID('AttendanceRecords'))
    DROP INDEX IX_Attendance_TenantId ON AttendanceRecords;
GO

-- Drop computed column WorkHours (references CheckInAt and CheckOutAt)
IF EXISTS (SELECT 1 FROM sys.computed_columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'WorkHours')
    ALTER TABLE AttendanceRecords DROP COLUMN WorkHours;
GO

-- Now rename CheckInAt → CheckInTime
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'CheckInAt')
    EXEC sp_rename 'AttendanceRecords.CheckInAt', 'CheckInTime', 'COLUMN';
GO

-- Rename CheckOutAt → CheckOutTime
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'CheckOutAt')
    EXEC sp_rename 'AttendanceRecords.CheckOutAt', 'CheckOutTime', 'COLUMN';
GO

-- Add TotalHours as a regular decimal column (replaces the dropped computed WorkHours)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'TotalHours')
    ALTER TABLE AttendanceRecords ADD TotalHours DECIMAL(6, 2) NOT NULL DEFAULT 0;
GO

-- Recreate indexes using the new column name CheckInTime
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AttendanceRecords_EmployeeId' AND object_id = OBJECT_ID('AttendanceRecords'))
    CREATE INDEX IX_AttendanceRecords_EmployeeId ON AttendanceRecords (EmployeeId, CheckInTime);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AttendanceRecords_TenantId' AND object_id = OBJECT_ID('AttendanceRecords'))
    CREATE INDEX IX_AttendanceRecords_TenantId ON AttendanceRecords (TenantId, CheckInTime);
GO

-- Unique index on (EmployeeId, Date) as required by the DbContext configuration
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AttendanceRecords_EmployeeId_Date' AND object_id = OBJECT_ID('AttendanceRecords'))
    CREATE UNIQUE INDEX IX_AttendanceRecords_EmployeeId_Date ON AttendanceRecords (EmployeeId, [Date]) WHERE IsDeleted = 0;
GO
