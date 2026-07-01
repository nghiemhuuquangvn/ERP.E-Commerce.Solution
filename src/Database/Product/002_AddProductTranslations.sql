-- ============================================================
-- ProductDB Schema Update — ProductService (Port 5008, Code: PRD)
-- Adds content-translation overlay tables for Product (Name/Description)
-- and Category (Name), per the shared TranslationEntity pattern
-- (src/Shared/Core/Domain/TranslationEntity.cs). The owning Products/
-- Categories tables keep their own Name/Description columns as the
-- fallback/default-language value; these tables are an additive overlay.
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ProductDB')
    CREATE DATABASE ProductDB;
GO

USE ProductDB;
GO

-- ============================================================
-- Product Translations
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductTranslations')
CREATE TABLE ProductTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(255)    NOT NULL,
    Description  NVARCHAR(2000)   NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProductTranslation_Product FOREIGN KEY (OwnerId) REFERENCES Products(Id)
);
CREATE UNIQUE INDEX IX_ProductTranslations_Owner ON ProductTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Category Translations
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CategoryTranslations')
CREATE TABLE CategoryTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(100)    NOT NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_CategoryTranslation_Category FOREIGN KEY (OwnerId) REFERENCES Categories(Id)
);
CREATE UNIQUE INDEX IX_CategoryTranslations_Owner ON CategoryTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO
