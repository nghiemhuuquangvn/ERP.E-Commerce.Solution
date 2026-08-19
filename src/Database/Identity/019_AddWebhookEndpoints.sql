-- ============================================================
-- AuthDB Schema - IdentityService (Port 5001)
-- Migration 019: Webhook endpoints (external HTTP callback delivery for domain
-- events, alongside the existing internal Kafka Outbox) + the two new
-- security:webhooks:{view,manage} permissions that manage them — see
-- docs/business/Identity/CommonServiceRoadmap.md §3.
--
-- Delivery is fire-and-forget from OutboxProcessor (mirrors NotificationService's
-- WebhookEndpoint/WebhookSender pattern — no separate delivery/retry table here
-- either, by design, for consistency with that existing precedent).
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- WebhookEndpoints
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WebhookEndpoints')
CREATE TABLE WebhookEndpoints (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    Url             NVARCHAR(2000)   NOT NULL,
    Secret          NVARCHAR(200)    NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    EventTypesRaw   NVARCHAR(2000)   NOT NULL DEFAULT '',
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NULL,
    CONSTRAINT FK_WebhookEndpoints_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
CREATE INDEX IX_WebhookEndpoints_TenantId ON WebhookEndpoints (TenantId);
GO

-- ============================================================
-- New permissions: security:webhooks:view / security:webhooks:manage
-- ============================================================
INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, v.Module
FROM (VALUES
    ('security:webhooks:view', 'View Webhook Endpoints', 'Identity'),
    ('security:webhooks:manage', 'Manage Webhook Endpoints', 'Identity')
) AS v(Code, Name, Module)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.PermissionCode = v.Code);
GO

-- Grant to every tenant's SuperAdmin/Admin role (matched by RoleCode, not a fixed
-- GUID — a Role row is per-tenant, so this must cover every tenant that already
-- exists, not just SYSTEM; mirrors 013_SeedRoleDefinitionsIntoRolePermissions.sql's
-- cross-tenant grant pattern). Any tenant created AFTER this migration gets these
-- automatically via AuthService.SignupTenantAsync's Module='Identity' role seed.
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE r.RoleCode IN ('SuperAdmin', 'Admin')
AND p.PermissionCode IN ('security:webhooks:view', 'security:webhooks:manage')
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
