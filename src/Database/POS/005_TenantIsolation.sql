-- ============================================================
-- PosDB Migration 005 — Tenant isolation (2026-10-20, service authz phase 2)
--
-- POSDbContext now applies a per-tenant query filter and stamps/validates TenantId on every
-- save (CLAUDE.md "Multi-Tenant Isolation"). Rows still at the empty GUID would become
-- invisible to every tenant, so — decision 2026-10-20, same as CRM 005 — they are
-- assigned to the SYSTEM tenant. System*Settings keep TenantId = empty: they are
-- platform-wide (IPlatformWideEntity).
-- Idempotent. Run BEFORE deploying the new POSService.
-- ============================================================

USE PosDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Product/promotion mirrors fed by Kafka had no tenant column. Existing rows -> SYSTEM.
IF OBJECT_ID('PosProductCache') IS NOT NULL AND COL_LENGTH('PosProductCache', 'TenantId') IS NULL
    ALTER TABLE PosProductCache ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_PosProductCache_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('PosPromotionCache') IS NOT NULL AND COL_LENGTH('PosPromotionCache', 'TenantId') IS NULL
    ALTER TABLE PosPromotionCache ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_PosPromotionCache_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
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
