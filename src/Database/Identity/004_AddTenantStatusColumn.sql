-- ============================================================
-- Migration 004: Add Status column to Tenants table
-- Reason: AuthTenant entity uses byte Status (1=Active, 0=Inactive)
--         but original schema used IsActive BIT
-- ============================================================

USE AuthDB;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Tenants') AND name = 'Status'
)
BEGIN
    ALTER TABLE Tenants ADD Status TINYINT NOT NULL DEFAULT 1;
    -- sp_executesql, not a bare UPDATE: SQL Server compiles an entire GO-delimited
    -- batch up front, so a column added by ALTER TABLE two lines above isn't yet
    -- visible to deferred name resolution for a statement later in the SAME batch
    -- ("Invalid column name 'Status'") — confirmed by running this migration against
    -- a genuinely fresh database (CommonServiceRoadmap.md §7 Part B export
    -- verification; every environment that had already run this migration before
    -- that fix never hit it, since the column already existed). sp_executesql defers
    -- compilation of the UPDATE to execution time, after the ALTER has committed.
    EXEC sp_executesql N'UPDATE Tenants SET Status = CAST(IsActive AS TINYINT);';
    PRINT 'Added Status column to Tenants and migrated values from IsActive.';
END
ELSE
BEGIN
    PRINT 'Status column already exists on Tenants — skipped.';
END
GO
