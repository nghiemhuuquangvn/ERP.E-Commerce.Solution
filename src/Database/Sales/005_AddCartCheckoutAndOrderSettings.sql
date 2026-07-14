-- ============================================================
-- SalesDB Migration 005 — Cart/Checkout/Customer + Order Settings (System/Tenant two-tier)
-- Mirrors AuthDB's SystemAuthSettings/TenantAuthSettings pattern.
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemCartCheckoutSettings')
BEGIN
    CREATE TABLE SystemCartCheckoutSettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        GuestCheckoutAllowed    BIT NOT NULL DEFAULT 1,
        Description             NVARCHAR(MAX) NULL,
        IsDeleted               BIT NOT NULL DEFAULT 0,
        CreatedBy               NVARCHAR(200) NULL,
        CreatedAt               DATETIME2 NOT NULL,
        UpdatedBy               NVARCHAR(200) NULL,
        UpdatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion              ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantCartCheckoutSettings')
BEGIN
    CREATE TABLE TenantCartCheckoutSettings
    (
        Id                              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                        UNIQUEIDENTIFIER NOT NULL,
        CartHoldMinutes                 INT NOT NULL DEFAULT 60,
        RequireRegistrationAtCheckout   BIT NOT NULL DEFAULT 0,
        CheckoutSteps                   INT NOT NULL DEFAULT 3,
        Description                     NVARCHAR(MAX) NULL,
        IsDeleted                       BIT NOT NULL DEFAULT 0,
        CreatedBy                       NVARCHAR(200) NULL,
        CreatedAt                       DATETIME2 NOT NULL,
        UpdatedBy                       NVARCHAR(200) NULL,
        UpdatedAt                       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion                      ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemOrderSettings')
BEGIN
    CREATE TABLE SystemOrderSettings
    (
        Id                              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        CustomOrderNumberFormatAllowed  BIT NOT NULL DEFAULT 1,
        Description                     NVARCHAR(MAX) NULL,
        IsDeleted                       BIT NOT NULL DEFAULT 0,
        CreatedBy                       NVARCHAR(200) NULL,
        CreatedAt                       DATETIME2 NOT NULL,
        UpdatedBy                       NVARCHAR(200) NULL,
        UpdatedAt                       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion                      ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantOrderSettings')
BEGIN
    CREATE TABLE TenantOrderSettings
    (
        Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER NOT NULL,
        OrderNumberPrefix           NVARCHAR(20) NOT NULL DEFAULT 'SO-',
        AutoCancelUnpaidAfterHours  INT NOT NULL DEFAULT 24,
        AllowEditAfterSubmit        BIT NOT NULL DEFAULT 0,
        Description                 NVARCHAR(MAX) NULL,
        IsDeleted                   BIT NOT NULL DEFAULT 0,
        CreatedBy                   NVARCHAR(200) NULL,
        CreatedAt                   DATETIME2 NOT NULL,
        UpdatedBy                   NVARCHAR(200) NULL,
        UpdatedAt                   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion                  ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantCartCheckoutSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantCartCheckoutSettings_TenantId ON TenantCartCheckoutSettings(TenantId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantOrderSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantOrderSettings_TenantId ON TenantOrderSettings(TenantId);
GO
