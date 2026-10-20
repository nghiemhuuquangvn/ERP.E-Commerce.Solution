-- ============================================================
-- HrmDB Migration 005 — Tenant isolation (2026-10-20, service authz phase 2)
--
-- HrmDbContext now applies a per-tenant query filter and stamps/validates TenantId on every
-- save (CLAUDE.md "Multi-Tenant Isolation"). Rows still at the empty GUID would become
-- invisible to every tenant, so — decision 2026-10-20, same as CRM 005 — they are
-- assigned to the SYSTEM tenant. System*Settings keep TenantId = empty: they are
-- platform-wide (IPlatformWideEntity).
-- Idempotent. Run BEFORE deploying the new HRMService.
-- ============================================================

USE HrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Work shift names are chosen per tenant (two tenants may both have "Morning").
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkShifts_Name' AND object_id = OBJECT_ID('WorkShifts'))
    DROP INDEX IX_WorkShifts_Name ON WorkShifts;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_WorkShifts_Tenant_Name')
    CREATE UNIQUE INDEX UX_WorkShifts_Tenant_Name ON WorkShifts (TenantId, Name) WHERE IsDeleted = 0;
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
