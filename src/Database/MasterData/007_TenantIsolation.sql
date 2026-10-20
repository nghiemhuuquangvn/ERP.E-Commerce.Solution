-- ============================================================
-- MasterDataDB Migration 007 — Tenant isolation (2026-10-20, service authz phase 2)
--
-- MasterDataDbContext now applies a per-tenant query filter and stamps/validates TenantId on every
-- save (CLAUDE.md "Multi-Tenant Isolation"). Rows still at the empty GUID would become
-- invisible to every tenant, so — decision 2026-10-20, same as CRM 005 — they are
-- assigned to the SYSTEM tenant. System*Settings keep TenantId = empty: they are
-- platform-wide (IPlatformWideEntity).
-- Idempotent. Run BEFORE deploying the new SharedMasterDataService.
-- ============================================================

USE MasterDataDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Codes are unique per tenant. Shared reference data (Countries, SupportedLanguages, ...)
-- lives in the SYSTEM tenant and is visible to every tenant; a tenant may add its own
-- rows (application handlers already reject a code that is visible to the tenant).
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Countries_Code' AND object_id = OBJECT_ID('Countries'))
    DROP INDEX IX_Countries_Code ON Countries;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Countries_Tenant_Code')
    CREATE UNIQUE INDEX UX_Countries_Tenant_Code ON Countries (TenantId, Code) WHERE IsDeleted = 0;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupportedLanguages_Code' AND object_id = OBJECT_ID('SupportedLanguages'))
    DROP INDEX IX_SupportedLanguages_Code ON SupportedLanguages;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SupportedLanguages_Tenant_Code')
    CREATE UNIQUE INDEX UX_SupportedLanguages_Tenant_Code ON SupportedLanguages (TenantId, Code) WHERE IsDeleted = 0;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Companies_Code' AND object_id = OBJECT_ID('Companies'))
    DROP INDEX IX_Companies_Code ON Companies;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Companies_Tenant_Code')
    CREATE UNIQUE INDEX UX_Companies_Tenant_Code ON Companies (TenantId, Code) WHERE IsDeleted = 0;
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
