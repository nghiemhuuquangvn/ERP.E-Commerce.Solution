-- ============================================================
-- ProductDB Schema Fix — ProductService (Port 5008, Code: PRD)
-- Migration 008: Adds missing BaseEntity columns to ProductVariants,
-- rebuilds ProductTranslations with the OwnerId + ProductId (shadow FK)
-- columns EF Core convention generates for TranslationEntity descendants,
-- and adds BaseEntity columns to CategoryTranslations.
-- ============================================================

USE ProductDB;
GO

-- ============================================================
-- ProductVariants — add missing BaseEntity columns
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'CreatedBy')
    ALTER TABLE ProductVariants ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'Description')
    ALTER TABLE ProductVariants ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'UpdatedBy')
    ALTER TABLE ProductVariants ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'RowVersion')
    ALTER TABLE ProductVariants ADD RowVersion ROWVERSION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'TenantId')
    ALTER TABLE ProductVariants ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'IsDeleted')
    ALTER TABLE ProductVariants ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

-- ============================================================
-- ProductTranslations — rebuild with correct schema
-- EF Convention: the Product.Translations navigation creates a shadow FK
-- column named 'ProductId', while OwnerId is the explicit TranslationEntity
-- property. Both must exist as columns.
-- ============================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductTranslations')
    DROP TABLE ProductTranslations;
GO

CREATE TABLE ProductTranslations (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OwnerId         UNIQUEIDENTIFIER NOT NULL,     -- TranslationEntity.OwnerId (= ProductId value)
    ProductId       UNIQUEIDENTIFIER NOT NULL,     -- EF shadow FK for Product.Translations navigation
    LanguageCode    NVARCHAR(10)     NOT NULL,
    Name            NVARCHAR(255)    NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion      ROWVERSION,
    CONSTRAINT FK_ProductTranslation_Product  FOREIGN KEY (ProductId)  REFERENCES Products(Id),
    CONSTRAINT FK_ProductTranslation_Owner    FOREIGN KEY (OwnerId)    REFERENCES Products(Id),
    CONSTRAINT UQ_ProductTranslation UNIQUE (ProductId, LanguageCode)
);
GO

CREATE INDEX IX_ProductTranslations_OwnerId ON ProductTranslations (OwnerId);
GO

-- ============================================================
-- CategoryTranslations — rebuild with correct schema (same pattern)
-- ============================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CategoryTranslations')
    DROP TABLE CategoryTranslations;
GO

CREATE TABLE CategoryTranslations (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OwnerId         UNIQUEIDENTIFIER NOT NULL,     -- TranslationEntity.OwnerId (= CategoryId value)
    CategoryId      UNIQUEIDENTIFIER NOT NULL,     -- EF shadow FK for Category.Translations navigation
    LanguageCode    NVARCHAR(10)     NOT NULL,
    Name            NVARCHAR(100)    NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion      ROWVERSION,
    CONSTRAINT FK_CategoryTranslation_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
    CONSTRAINT FK_CategoryTranslation_Owner    FOREIGN KEY (OwnerId)    REFERENCES Categories(Id),
    CONSTRAINT UQ_CategoryTranslation UNIQUE (CategoryId, LanguageCode)
);
GO
