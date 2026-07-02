-- ============================================================
-- ProductDB Schema Update — ProductService (Port 5008, Code: PRD)
-- Migration 004: External system mappings for Products/ProductVariants
-- (e.g. marketplace/channel SKUs, legacy-system IDs) used by sync jobs to
-- correlate this service's records with the corresponding external records.
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ProductDB')
    CREATE DATABASE ProductDB;
GO

USE ProductDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductExternalMappings')
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
CREATE UNIQUE INDEX IX_ExternalMappings_System_ExternalId ON ProductExternalMappings (ExternalSystem, ExternalId) WHERE IsDeleted = 0;
CREATE INDEX        IX_ExternalMappings_Product            ON ProductExternalMappings (ProductId);
GO
