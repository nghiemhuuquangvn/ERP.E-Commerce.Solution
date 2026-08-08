-- ============================================================
-- ProductDB Migration 010 — Brand master data, per-product UoM conversions,
-- Category.Code, PriceList Channel/Currency + quantity-break pricing,
-- Product.BrandId/BaseUomId, ProductImage.VariantId.
-- Safe to run multiple times (all DDL is guarded with IF NOT EXISTS).
-- ============================================================

USE ProductDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- STEP 1: Brands (flat reference list, no hierarchy — proper noun, not localized)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Brands')
CREATE TABLE Brands (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Code        NVARCHAR(50)     NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    LogoUrl     NVARCHAR(500)    NULL,
    IsActive    BIT              NOT NULL DEFAULT 1,
    Description NVARCHAR(2000)   NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Brands_Code' AND object_id = OBJECT_ID('Brands'))
    CREATE UNIQUE INDEX UX_Brands_Code ON Brands (Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- STEP 2: ProductUomConversions (per-product UoM conversion; FromUomId/ToUomId
-- reference SharedMasterDataService's UnitOfMeasure by Id only — no DB FK,
-- see CLAUDE.md database ownership rules)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductUomConversions')
CREATE TABLE ProductUomConversions (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId         UNIQUEIDENTIFIER NOT NULL,
    FromUomId         UNIQUEIDENTIFIER NOT NULL,
    ToUomId           UNIQUEIDENTIFIER NOT NULL,
    ConversionFactor  DECIMAL(18, 6)   NOT NULL,
    CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt         DATETIME2        NULL,
    CONSTRAINT FK_ProductUomConversion_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductUomConversions_Product' AND object_id = OBJECT_ID('ProductUomConversions'))
    CREATE INDEX IX_ProductUomConversions_Product ON ProductUomConversions (ProductId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ProductUomConversions_Pair' AND object_id = OBJECT_ID('ProductUomConversions'))
    CREATE UNIQUE INDEX UX_ProductUomConversions_Pair ON ProductUomConversions (ProductId, FromUomId, ToUomId);
GO

-- ============================================================
-- STEP 3: Products.BrandId (local FK to Brands) + BaseUomId (cross-service, no FK)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'BrandId')
    ALTER TABLE Products ADD BrandId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Product_Brand')
    ALTER TABLE Products ADD CONSTRAINT FK_Product_Brand FOREIGN KEY (BrandId) REFERENCES Brands(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'BaseUomId')
    -- cross-service ref to SharedMasterDataService's UnitOfMeasure, no DB FK
    ALTER TABLE Products ADD BaseUomId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Brand' AND object_id = OBJECT_ID('Products'))
    CREATE INDEX IX_Products_Brand ON Products (BrandId);
GO

-- ============================================================
-- STEP 4: Categories.Code (nullable — existing rows won't have one; unique
-- when present)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'Code')
    ALTER TABLE Categories ADD Code NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Categories_Code' AND object_id = OBJECT_ID('Categories'))
    CREATE UNIQUE INDEX UX_Categories_Code ON Categories (Code) WHERE Code IS NOT NULL;
GO

-- ============================================================
-- STEP 5: PriceLists.Channel/CurrencyId + PriceListItems.MinQuantity
-- (quantity-break pricing; uniqueness widened from (PriceListId, VariantId)
-- to (PriceListId, VariantId, MinQuantity))
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PriceLists') AND name = 'Channel')
    ALTER TABLE PriceLists ADD Channel INT NOT NULL DEFAULT 0; -- PriceListChannel: Retail=0, Wholesale=1, Ecommerce=2
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PriceLists') AND name = 'CurrencyId')
    -- cross-service ref to SharedMasterDataService's Currency, no DB FK; NULL = tenant default currency
    ALTER TABLE PriceLists ADD CurrencyId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PriceListItems') AND name = 'MinQuantity')
    ALTER TABLE PriceListItems ADD MinQuantity INT NOT NULL DEFAULT 1;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PriceListItems_Key' AND object_id = OBJECT_ID('PriceListItems'))
    DROP INDEX IX_PriceListItems_Key ON PriceListItems;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PriceListItems_Key' AND object_id = OBJECT_ID('PriceListItems'))
    CREATE UNIQUE INDEX IX_PriceListItems_Key ON PriceListItems (PriceListId, VariantId, MinQuantity);
GO

-- ============================================================
-- STEP 6: ProductImages.VariantId (local FK to ProductVariants, nullable —
-- NULL means a product-level image)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductImages') AND name = 'VariantId')
    ALTER TABLE ProductImages ADD VariantId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProductImage_Variant')
    ALTER TABLE ProductImages ADD CONSTRAINT FK_ProductImage_Variant FOREIGN KEY (VariantId) REFERENCES ProductVariants(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductImages_Variant' AND object_id = OBJECT_ID('ProductImages'))
    CREATE INDEX IX_ProductImages_Variant ON ProductImages (VariantId);
GO
