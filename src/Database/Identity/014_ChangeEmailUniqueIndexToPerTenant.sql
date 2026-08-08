-- ============================================================
-- AuthDB Schema - IdentityService (Port 5001)
-- Migration 014: Email uniqueness moves from global to per-tenant.
--
-- Context: login/register no longer take a manually-typed Tenant Code — the tenant is
-- resolved from the request's subdomain (Gateway) before Email is looked up. Requiring
-- Email to be unique across the ENTIRE platform (IX_Users_Email, 001_CreateAuthDB.sql:62)
-- was never realistic for a multi-tenant ERP serving independent companies (two unrelated
-- customers can easily share a generic address like "info@..."), and it blocked the
-- already-implemented "invite an existing user into another tenant" feature
-- (UserService.InviteToTenantAsync) from ever working end-to-end.
--
-- Safe to run on existing data: a composite (Email, TenantId) unique constraint is
-- strictly LOOSER than the previous global-Email constraint, so no existing row can
-- violate it - no de-duplication needed first.
--
-- Idempotent: safe to re-run.
-- ============================================================

USE AuthDB;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('Users'))
    DROP INDEX IX_Users_Email ON Users;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email_TenantId' AND object_id = OBJECT_ID('Users'))
    CREATE UNIQUE INDEX IX_Users_Email_TenantId ON Users (Email, TenantId) WHERE IsDeleted = 0;
GO
