-- ============================================================
-- AuthDB Migration 022 — TenantDomains (tenant is decided by the request host)
--
-- Custom host names per tenant (e.g. shop.acme.com). Subdomains of Tenancy:BaseDomain
-- (acme.erp.com) resolve by TenantCode without a row here, and the platform (SYSTEM)
-- tenant's host comes from Tenancy:PlatformHost — so no seed rows are required.
-- See ITenantDomainService / TenantDomainService.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantDomains')
BEGIN
    CREATE TABLE TenantDomains (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        Host        NVARCHAR(253)    NOT NULL,
        IsPrimary   BIT              NOT NULL DEFAULT 0,
        CreatedBy   NVARCHAR(256)    NULL,
        CreatedAt   DATETIME2        NULL DEFAULT GETUTCDATE(),
        UpdatedBy   NVARCHAR(256)    NULL,
        UpdatedAt   DATETIME2        NULL DEFAULT GETUTCDATE(),
        IsDeleted   BIT              NOT NULL DEFAULT 0,
        CONSTRAINT FK_TenantDomains_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_TenantDomains_Host ON TenantDomains (Host);
    CREATE INDEX IX_TenantDomains_TenantId ON TenantDomains (TenantId);
END
GO
