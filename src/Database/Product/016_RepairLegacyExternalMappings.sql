-- ============================================================
-- ProductDB Schema Repair — Migration 016
-- Databases provisioned before migration 004 have a legacy ProductExternalMappings
-- (Id, ProductId, Platform, ExternalId, CreatedAt: no TenantId / audit columns / IsDeleted).
-- 004 skips its CREATE TABLE when the table exists and 015 then fails on `IsDeleted`, so
-- the per-tenant unique index never existed and the EF entity could not read the table.
--
-- Rebuilds the table to the 004 shape, plus 015's per-tenant unique index. Refuses to drop
-- a legacy table that holds rows (nothing writes the legacy shape any more) — migrate
-- those rows by hand instead. Idempotent.
-- ============================================================

USE ProductDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID('ProductExternalMappings') IS NOT NULL
   AND COL_LENGTH('ProductExternalMappings', 'TenantId') IS NULL
BEGIN
    DECLARE @rows INT;
    EXEC sp_executesql N'SELECT @n = COUNT(*) FROM ProductExternalMappings', N'@n INT OUTPUT', @n = @rows OUTPUT;
    IF @rows > 0
        THROW 50016, 'ProductExternalMappings has the legacy shape and contains rows; migrate them manually before running 016.', 1;
    DROP TABLE ProductExternalMappings;
END
GO

IF OBJECT_ID('ProductExternalMappings') IS NULL
BEGIN
    CREATE TABLE ProductExternalMappings (
        Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId          UNIQUEIDENTIFIER NOT NULL,
        ProductId         UNIQUEIDENTIFIER NOT NULL,
        VariantId         UNIQUEIDENTIFIER NULL,
        ExternalSystem    NVARCHAR(100)    NOT NULL,
        ExternalId        NVARCHAR(200)    NOT NULL,
        ExternalDataJson  NVARCHAR(MAX)    NULL,
        SyncedAt          DATETIME2        NULL,
        CreatedBy         NVARCHAR(256)    NULL,
        CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedBy         NVARCHAR(256)    NULL,
        UpdatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted         BIT              NOT NULL DEFAULT 0,
        CONSTRAINT FK_ExternalMapping_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT FK_ExternalMapping_Variant FOREIGN KEY (VariantId) REFERENCES ProductVariants(Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ExternalMappings_Tenant_System_ExternalId' AND object_id = OBJECT_ID('ProductExternalMappings'))
    CREATE UNIQUE INDEX UX_ExternalMappings_Tenant_System_ExternalId
        ON ProductExternalMappings (TenantId, ExternalSystem, ExternalId) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExternalMappings_Product' AND object_id = OBJECT_ID('ProductExternalMappings'))
    CREATE INDEX IX_ExternalMappings_Product ON ProductExternalMappings (ProductId);
GO

-- BaseEntity also maps Description and RowVersion (found by LiveSchemaDriftTests).
IF COL_LENGTH('ProductExternalMappings', 'Description') IS NULL
    ALTER TABLE ProductExternalMappings ADD Description NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH('ProductExternalMappings', 'RowVersion') IS NULL
    ALTER TABLE ProductExternalMappings ADD RowVersion ROWVERSION NOT NULL;
GO
