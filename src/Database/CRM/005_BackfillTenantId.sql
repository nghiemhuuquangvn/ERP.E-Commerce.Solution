-- ============================================================
-- CrmDB Migration 005 — Backfill TenantId before tenant isolation
--
-- Until 2026-10-20 CRMService never set TenantId on rows created through its API, so
-- they were saved with the empty GUID. CrmDbContext now applies a per-tenant query
-- filter and stamps TenantId on every insert; rows left at the empty GUID would become
-- invisible to every tenant. Decision (2026-10-20): assign them to the SYSTEM tenant
-- (00000000-0000-0000-0000-000000000001 — same tenant as the 004 demo data).
-- Idempotent: only touches rows still at the empty GUID.
-- ============================================================

USE CrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @SystemTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @Empty UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

UPDATE Contacts      SET TenantId = @SystemTenantId WHERE TenantId = @Empty;
UPDATE Leads         SET TenantId = @SystemTenantId WHERE TenantId = @Empty;
UPDATE Opportunities SET TenantId = @SystemTenantId WHERE TenantId = @Empty;
UPDATE Cases         SET TenantId = @SystemTenantId WHERE TenantId = @Empty;
UPDATE Activities    SET TenantId = @SystemTenantId WHERE TenantId = @Empty;
UPDATE ContactTags   SET TenantId = @SystemTenantId WHERE TenantId = @Empty;
GO
