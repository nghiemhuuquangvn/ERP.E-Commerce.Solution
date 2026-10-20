-- ============================================================
-- PromotionDB Migration 003 — Tenant isolation (2026-10-20, service authz phase 2)
--
-- PromotionDbContext now applies a per-tenant query filter and stamps/validates TenantId on every
-- save (CLAUDE.md "Multi-Tenant Isolation"). Rows still at the empty GUID would become
-- invisible to every tenant, so — decision 2026-10-20, same as CRM 005 — they are
-- assigned to the SYSTEM tenant. System*Settings keep TenantId = empty: they are
-- platform-wide (IPlatformWideEntity).
-- Idempotent. Run BEFORE deploying the new PromotionService.
-- ============================================================

USE PromotionDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- PromotionProductCache (product mirror fed by ProductCreated) had no tenant column.
-- Existing rows -> SYSTEM. (Coupons are already unique per (Code, TenantId) since 001.)
IF OBJECT_ID('PromotionProductCache') IS NOT NULL AND COL_LENGTH('PromotionProductCache', 'TenantId') IS NULL
    ALTER TABLE PromotionProductCache ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_PromotionProductCache_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
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
