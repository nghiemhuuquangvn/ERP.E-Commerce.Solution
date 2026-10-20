-- ============================================================
-- SalesDB Migration 011 — Tenant isolation (2026-10-20, service authz phase 2)
--
-- SalesOrderDbContext now applies a per-tenant query filter and stamps/validates TenantId
-- on every save. Before that:
--   * CustomerSalesProfiles, CommissionPlans and CommissionRecords had no TenantId at all
--     (every tenant shared credit limits and commission plans);
--   * rows created through the API could be saved with the empty GUID.
-- Rows at the empty GUID would become invisible to every tenant, so — decision
-- 2026-10-20, same as CRM 005 — they are assigned to the SYSTEM tenant.
-- System*Settings keep TenantId = empty: they are platform-wide (IPlatformWideEntity).
-- Idempotent. Run BEFORE deploying the new SaleOrderService.
-- ============================================================

USE SalesDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1) Tables that had no tenant column. Existing rows -> SYSTEM.
IF COL_LENGTH('CustomerSalesProfiles', 'TenantId') IS NULL
    ALTER TABLE CustomerSalesProfiles ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_CustomerSalesProfiles_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF COL_LENGTH('CommissionPlans', 'TenantId') IS NULL
    ALTER TABLE CommissionPlans ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_CommissionPlans_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF COL_LENGTH('CommissionRecords', 'TenantId') IS NULL
    ALTER TABLE CommissionRecords ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_CommissionRecords_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
GO

-- A customer's credit profile is per tenant (the same user may buy from several tenants).
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CustomerSalesProfiles_CustomerId')
    DROP INDEX UX_CustomerSalesProfiles_CustomerId ON CustomerSalesProfiles;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CustomerSalesProfiles_Tenant_Customer')
    CREATE UNIQUE INDEX UX_CustomerSalesProfiles_Tenant_Customer ON CustomerSalesProfiles (TenantId, CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CommissionPlans_TenantId')
    CREATE INDEX IX_CommissionPlans_TenantId ON CommissionPlans (TenantId, IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CommissionRecords_TenantId')
    CREATE INDEX IX_CommissionRecords_TenantId ON CommissionRecords (TenantId, SalesRepId);
GO

-- Order/quotation numbers come from a per-tenant counter (OrderNumberSequences), so two
-- tenants legitimately produce the same number: uniqueness is per tenant.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SalesOrders_OrderNo')
    DROP INDEX UX_SalesOrders_OrderNo ON SalesOrders;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SalesOrders_Tenant_OrderNo')
    CREATE UNIQUE INDEX UX_SalesOrders_Tenant_OrderNo ON SalesOrders (TenantId, OrderNo) WHERE OrderNo <> '';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SalesQuotations_QuotationNo')
    DROP INDEX UX_SalesQuotations_QuotationNo ON SalesQuotations;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SalesQuotations_Tenant_QuotationNo')
    CREATE UNIQUE INDEX UX_SalesQuotations_Tenant_QuotationNo ON SalesQuotations (TenantId, QuotationNo);
GO

-- 2) Backfill: every remaining tenant column still at the empty GUID -> SYSTEM.
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
