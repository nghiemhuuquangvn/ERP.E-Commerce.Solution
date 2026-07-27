-- ============================================================
-- SalesDB Migration 008 — CustomerSalesProfile: Sales-owned credit-limit concept.
-- No other service in this system owns a CreditLimit field (CRMService has no
-- Customer entity, IdentityService's User has none), so Sales becomes the
-- source of truth here instead of syncing it from elsewhere.
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerSalesProfiles')
CREATE TABLE CustomerSalesProfiles (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    CustomerId      UNIQUEIDENTIFIER NOT NULL,
    CreditLimit     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    CurrentBalance  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CustomerSalesProfiles_CustomerId')
    CREATE UNIQUE INDEX UX_CustomerSalesProfiles_CustomerId ON CustomerSalesProfiles(CustomerId);
GO
