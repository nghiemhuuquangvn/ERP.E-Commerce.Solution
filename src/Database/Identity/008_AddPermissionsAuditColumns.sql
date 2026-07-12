-- ============================================================
-- Migration 008: Add missing BaseEntity audit columns to Permissions
-- Permission : BaseEntity requires CreatedBy/UpdatedBy, but the
-- Permissions table was left out of migration 005's sweep — any
-- EF Core query that materializes a Permission (e.g. Users ->
-- UserRoles -> Role -> RolePermissions -> Permission) fails with
-- "Invalid column name 'CreatedBy'. Invalid column name 'UpdatedBy'."
-- ============================================================

USE AuthDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Permissions') AND name = 'CreatedBy')
    ALTER TABLE Permissions ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Permissions') AND name = 'UpdatedBy')
    ALTER TABLE Permissions ADD UpdatedBy NVARCHAR(256) NULL;
GO

PRINT 'Migration 008 complete: Permissions audit columns added.';
GO
