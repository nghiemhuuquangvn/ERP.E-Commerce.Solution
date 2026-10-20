-- ============================================================
-- ReportingDB Migration 006 — Tenant isolation (2026-10-20, service authz phase 2)
--
-- ReportingDbContext now applies a per-tenant query filter and stamps/validates TenantId on every
-- save (CLAUDE.md "Multi-Tenant Isolation"). Rows still at the empty GUID would become
-- invisible to every tenant, so — decision 2026-10-20, same as CRM 005 — they are
-- assigned to the SYSTEM tenant. System*Settings keep TenantId = empty: they are
-- platform-wide (IPlatformWideEntity).
-- Idempotent. Run BEFORE deploying the new ReportingService.
-- ============================================================

USE ReportingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Facts/dimensions had no tenant at all: every tenant's reports summed every tenant's
-- orders, payments, stock and payroll. Existing rows -> SYSTEM. DimDate stays global.
IF OBJECT_ID('FactSalesOrders') IS NOT NULL AND COL_LENGTH('FactSalesOrders', 'TenantId') IS NULL
    ALTER TABLE FactSalesOrders ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_FactSalesOrders_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('FactPayments') IS NOT NULL AND COL_LENGTH('FactPayments', 'TenantId') IS NULL
    ALTER TABLE FactPayments ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_FactPayments_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('FactInventoryMovements') IS NOT NULL AND COL_LENGTH('FactInventoryMovements', 'TenantId') IS NULL
    ALTER TABLE FactInventoryMovements ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_FactInventoryMovements_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('FactPOSTransactions') IS NOT NULL AND COL_LENGTH('FactPOSTransactions', 'TenantId') IS NULL
    ALTER TABLE FactPOSTransactions ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_FactPOSTransactions_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('FactHRPayslips') IS NOT NULL AND COL_LENGTH('FactHRPayslips', 'TenantId') IS NULL
    ALTER TABLE FactHRPayslips ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_FactHRPayslips_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('DimProducts') IS NOT NULL AND COL_LENGTH('DimProducts', 'TenantId') IS NULL
    ALTER TABLE DimProducts ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_DimProducts_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('DimCustomers') IS NOT NULL AND COL_LENGTH('DimCustomers', 'TenantId') IS NULL
    ALTER TABLE DimCustomers ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_DimCustomers_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
GO

IF OBJECT_ID('FactSalesOrders') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactSalesOrders_TenantId')
    CREATE INDEX IX_FactSalesOrders_TenantId ON FactSalesOrders (TenantId);
IF OBJECT_ID('FactPayments') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactPayments_TenantId')
    CREATE INDEX IX_FactPayments_TenantId ON FactPayments (TenantId);
IF OBJECT_ID('FactInventoryMovements') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactInventoryMovements_TenantId')
    CREATE INDEX IX_FactInventoryMovements_TenantId ON FactInventoryMovements (TenantId);
IF OBJECT_ID('FactPOSTransactions') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactPOSTransactions_TenantId')
    CREATE INDEX IX_FactPOSTransactions_TenantId ON FactPOSTransactions (TenantId);
IF OBJECT_ID('FactHRPayslips') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactHRPayslips_TenantId')
    CREATE INDEX IX_FactHRPayslips_TenantId ON FactHRPayslips (TenantId);
IF OBJECT_ID('DimProducts') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DimProducts_TenantId')
    CREATE INDEX IX_DimProducts_TenantId ON DimProducts (TenantId);
GO

-- Backfill: every tenant column still at the empty GUID -> SYSTEM.
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
             + N' SET TenantId = ''00000000-0000-0000-0000-000000000001'''
             + N' WHERE TenantId = ''00000000-0000-0000-0000-000000000000'';' + CHAR(10)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'TenantId'
WHERE t.name NOT LIKE 'System%Settings';
EXEC sp_executesql @sql;
GO
