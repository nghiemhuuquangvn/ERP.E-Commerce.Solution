-- ============================================================
-- ProductDB Schema Catchup — ProductService (Port 5008, Code: PRD)
-- Migration 007: Rebuilds Products table (old EF schema had wrong columns),
-- creates all tables missing from the DB (ProductVariants, ProductImages,
-- PriceLists, PriceListItems, OutboxMessages, ProductTranslations,
-- CategoryTranslations, ProductAttributes, ProductAttributeOptions,
-- ProductAttributeValues, ProductExternalMappings, ProductBundleItems,
-- ProductRelations, ProductReviews), and adds missing BaseEntity+entity
-- columns (RowVersion, ExtendedDataJson, Slug, MetaTitle, MetaDescription,
-- StockQuantity, LastStockSyncAt, RowVersion on Categories).
-- Safe to run multiple times (all DDL is guarded with IF NOT EXISTS).
-- ============================================================

USE ProductDB;
GO

-- ============================================================
-- STEP 1: Rebuild Products table
-- The old EF-generated table had wrong columns (Price, missing TenantId/Sku/etc.)
-- Drop and recreate with the canonical schema from 001_CreateProductDB.sql.
-- ============================================================

-- Drop old Products table (no FK children reference it yet in the old DB)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
    AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'TenantId')
BEGIN
    DROP TABLE Products;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
CREATE TABLE Products (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Sku             NVARCHAR(50)     NOT NULL DEFAULT '',
    Name            NVARCHAR(255)    NOT NULL,
    Description     NVARCHAR(2000)   NULL,
    ProductType     INT              NOT NULL DEFAULT 0,
    Status          INT              NOT NULL DEFAULT 0,
    CategoryId      UNIQUEIDENTIFIER NULL,
    HasImage        BIT              NOT NULL DEFAULT 0,
    Slug            NVARCHAR(256)    NULL,
    MetaTitle       NVARCHAR(200)    NULL,
    MetaDescription NVARCHAR(320)    NULL,
    StockQuantity   INT              NOT NULL DEFAULT 0,
    LastStockSyncAt DATETIME2        NULL,
    ExtendedDataJson NVARCHAR(MAX)   NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_TenantId' AND object_id = OBJECT_ID('Products'))
    CREATE INDEX IX_Products_TenantId ON Products (TenantId, Status, CreatedAt DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Category' AND object_id = OBJECT_ID('Products'))
    CREATE INDEX IX_Products_Category ON Products (CategoryId, Status);
GO

-- ============================================================
-- STEP 2: Add RowVersion to Categories (BaseEntity requires it)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'RowVersion')
    ALTER TABLE Categories ADD RowVersion ROWVERSION;
GO

-- Category : AggregateRoot also inherits BaseEntity's Description column, but neither
-- 001_CreateProductDB.sql's original CREATE TABLE nor this step's RowVersion catch-up ever
-- added it — CategoryDbContext has no explicit Entity<Category>() config, so EF maps
-- Description by convention and every GET /api/v1/categories 500'd with
-- "Invalid column name 'Description'".
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'Description')
    ALTER TABLE Categories ADD Description NVARCHAR(MAX) NULL;
GO

-- ============================================================
-- STEP 3: ProductVariants (from 001)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductVariants')
CREATE TABLE ProductVariants (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    Sku             NVARCHAR(50)     NOT NULL,
    Barcode         NVARCHAR(100)    NULL,
    Cost            DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    Weight          DECIMAL(18, 3)   NULL,
    Dimensions      NVARCHAR(100)    NULL,
    Length          DECIMAL(18, 3)   NULL,
    Width           DECIMAL(18, 3)   NULL,
    Height          DECIMAL(18, 3)   NULL,
    DimensionUnit   NVARCHAR(10)     NOT NULL DEFAULT 'cm',
    WeightUnit      NVARCHAR(10)     NOT NULL DEFAULT 'kg',
    ExtendedDataJson NVARCHAR(MAX)   NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Variant_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductVariants_Product' AND object_id = OBJECT_ID('ProductVariants'))
    CREATE INDEX IX_ProductVariants_Product ON ProductVariants (ProductId);
GO

-- ============================================================
-- STEP 4: ProductImages (from 001)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductImages')
CREATE TABLE ProductImages (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    ImageUrl    NVARCHAR(1000)   NOT NULL,
    AltText     NVARCHAR(255)    NULL,
    SortOrder   INT              NOT NULL DEFAULT 0,
    IsPrimary   BIT              NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Image_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductImages_Product' AND object_id = OBJECT_ID('ProductImages'))
    CREATE INDEX IX_ProductImages_Product ON ProductImages (ProductId, IsPrimary DESC, SortOrder);
GO

-- ============================================================
-- STEP 5: PriceLists + PriceListItems (from 001)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PriceLists')
CREATE TABLE PriceLists (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Name            NVARCHAR(200)    NOT NULL,
    Description     NVARCHAR(2000)   NULL,
    EffectiveDate   DATETIME2        NOT NULL,
    ExpiryDate      DATETIME2        NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PriceListItems')
CREATE TABLE PriceListItems (
    Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    PriceListId      UNIQUEIDENTIFIER NOT NULL,
    VariantId        UNIQUEIDENTIFIER NOT NULL,
    BasePrice        DECIMAL(18, 2)   NOT NULL,
    DiscountedPrice  DECIMAL(18, 2)   NULL,
    CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt        DATETIME2        NULL,
    CONSTRAINT FK_PriceListItem_PriceList FOREIGN KEY (PriceListId) REFERENCES PriceLists(Id),
    CONSTRAINT FK_PriceListItem_Variant   FOREIGN KEY (VariantId)   REFERENCES ProductVariants(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PriceListItems_Key' AND object_id = OBJECT_ID('PriceListItems'))
    CREATE UNIQUE INDEX IX_PriceListItems_Key ON PriceListItems (PriceListId, VariantId);
GO

-- ============================================================
-- STEP 6: OutboxMessages (from 001)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutboxMessages')
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
GO

-- ============================================================
-- STEP 7: ProductTranslations + CategoryTranslations (from 002)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductTranslations')
CREATE TABLE ProductTranslations (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    LanguageCode    NVARCHAR(10)     NOT NULL,
    Name            NVARCHAR(255)    NOT NULL,
    Description     NVARCHAR(2000)   NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProductTranslation_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    CONSTRAINT UQ_ProductTranslation UNIQUE (ProductId, LanguageCode)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CategoryTranslations')
CREATE TABLE CategoryTranslations (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    CategoryId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode    NVARCHAR(10)     NOT NULL,
    Name            NVARCHAR(100)    NOT NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CategoryTranslation_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
    CONSTRAINT UQ_CategoryTranslation UNIQUE (CategoryId, LanguageCode)
);
GO

-- ============================================================
-- STEP 8: ProductAttributes + Options + Values (from 003)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributes')
CREATE TABLE ProductAttributes (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(100)    NOT NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProductAttribute_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributeOptions')
CREATE TABLE ProductAttributeOptions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    AttributeId     UNIQUEIDENTIFIER NOT NULL,
    Value           NVARCHAR(200)    NOT NULL,
    SortOrder       INT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProductAttributeOption_Attribute FOREIGN KEY (AttributeId) REFERENCES ProductAttributes(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributeValues')
CREATE TABLE ProductAttributeValues (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    VariantId   UNIQUEIDENTIFIER NOT NULL,
    AttributeId UNIQUEIDENTIFIER NOT NULL,
    OptionId    UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT FK_ProductAttributeValue_Variant   FOREIGN KEY (VariantId)   REFERENCES ProductVariants(Id),
    CONSTRAINT FK_ProductAttributeValue_Attribute FOREIGN KEY (AttributeId) REFERENCES ProductAttributes(Id),
    CONSTRAINT FK_ProductAttributeValue_Option    FOREIGN KEY (OptionId)    REFERENCES ProductAttributeOptions(Id)
);
GO

-- ============================================================
-- STEP 9: ProductExternalMappings (from 004)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductExternalMappings')
CREATE TABLE ProductExternalMappings (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    Platform    NVARCHAR(50)     NOT NULL,
    ExternalId  NVARCHAR(200)    NOT NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProductExternalMapping_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    CONSTRAINT UQ_ProductExternalMapping UNIQUE (Platform, ExternalId)
);
GO

-- ============================================================
-- STEP 10: ProductBundleItems + ProductRelations + ProductReviews (from 006)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductBundleItems')
CREATE TABLE ProductBundleItems (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    BundleProductId     UNIQUEIDENTIFIER NOT NULL,
    ComponentProductId  UNIQUEIDENTIFIER NOT NULL,
    Quantity            INT              NOT NULL DEFAULT 1,
    CONSTRAINT FK_ProductBundleItem_Bundle    FOREIGN KEY (BundleProductId)    REFERENCES Products(Id),
    CONSTRAINT FK_ProductBundleItem_Component FOREIGN KEY (ComponentProductId) REFERENCES Products(Id),
    CONSTRAINT UQ_ProductBundleItem UNIQUE (BundleProductId, ComponentProductId)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductRelations')
CREATE TABLE ProductRelations (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    RelatedProductId UNIQUEIDENTIFIER NOT NULL,
    RelationType    NVARCHAR(50)     NOT NULL,
    CONSTRAINT FK_ProductRelation_Product        FOREIGN KEY (ProductId)        REFERENCES Products(Id),
    CONSTRAINT FK_ProductRelation_RelatedProduct FOREIGN KEY (RelatedProductId) REFERENCES Products(Id),
    CONSTRAINT UQ_ProductRelation UNIQUE (ProductId, RelatedProductId, RelationType)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductReviews')
CREATE TABLE ProductReviews (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    CustomerId  UNIQUEIDENTIFIER NOT NULL,
    Rating      INT              NOT NULL,
    Title       NVARCHAR(200)    NULL,
    Body        NVARCHAR(2000)   NULL,
    IsVerified  BIT              NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProductReview_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductReviews_Product' AND object_id = OBJECT_ID('ProductReviews'))
    CREATE INDEX IX_ProductReviews_Product ON ProductReviews (ProductId, Rating DESC);
GO
