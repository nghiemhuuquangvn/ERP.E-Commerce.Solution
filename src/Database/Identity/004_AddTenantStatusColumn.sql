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
    UPDATE Tenants SET Status = CAST(IsActive AS TINYINT);
    PRINT 'Added Status column to Tenants and migrated values from IsActive.';
END
ELSE
BEGIN
    PRINT 'Status column already exists on Tenants — skipped.';
END
GO
