-- ============================================================
-- ProductDB Schema Update — ProductService (Port 5008, Code: PRD)
-- Migration 006: SEO fields + stock-quantity sync columns on Products, plus
-- three new tables: ProductBundleItems (bundle/combo composition),
-- ProductRelations (cross-sell/upsell), ProductReviews (ratings/comments).
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ProductDB')
    CREATE DATABASE ProductDB;
GO

USE ProductDB;
GO

-- ============================================================
-- Products: SEO fields + denormalized stock-quantity projection
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'Slug')
    ALTER TABLE Products ADD Slug NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'MetaTitle')
    ALTER TABLE Products ADD MetaTitle NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'MetaDescription')
    ALTER TABLE Products ADD MetaDescription NVARCHAR(320) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'StockQuantity')
    ALTER TABLE Products ADD StockQuantity INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'LastStockSyncAt')
    ALTER TABLE Products ADD LastStockSyncAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Slug' AND object_id = OBJECT_ID('Products'))
    CREATE UNIQUE INDEX IX_Products_Slug ON Products (Slug) WHERE Slug IS NOT NULL;
GO

-- ============================================================
-- Product Bundle Items (bundle/combo composition)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductBundleItems')
CREATE TABLE ProductBundleItems (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    BundleProductId     UNIQUEIDENTIFIER NOT NULL,
    ComponentProductId  UNIQUEIDENTIFIER NOT NULL,
    Quantity            INT              NOT NULL DEFAULT 1,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProductBundleItems_Bundle FOREIGN KEY (BundleProductId) REFERENCES Products(Id),
    CONSTRAINT FK_ProductBundleItems_Component FOREIGN KEY (ComponentProductId) REFERENCES Products(Id)
);
CREATE INDEX IX_ProductBundleItems_BundleProductId ON ProductBundleItems (BundleProductId);
CREATE UNIQUE INDEX IX_ProductBundleItems_Bundle_Component ON ProductBundleItems (BundleProductId, ComponentProductId);
GO

-- ============================================================
-- Product Relations (cross-sell/upsell — directed: A -> B doesn't imply B -> A)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductRelations')
CREATE TABLE ProductRelations (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId         UNIQUEIDENTIFIER NOT NULL,
    RelatedProductId  UNIQUEIDENTIFIER NOT NULL,
    RelationType      INT              NOT NULL DEFAULT 0, -- 0=CrossSell, 1=UpSell
    SortOrder         INT              NOT NULL DEFAULT 0,
    CreatedBy         NVARCHAR(256)    NULL,
    CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy         NVARCHAR(256)    NULL,
    UpdatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted         BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProductRelations_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    CONSTRAINT FK_ProductRelations_Related FOREIGN KEY (RelatedProductId) REFERENCES Products(Id)
);
CREATE INDEX IX_ProductRelations_Product_Type ON ProductRelations (ProductId, RelationType);
CREATE UNIQUE INDEX IX_ProductRelations_Product_Related_Type ON ProductRelations (ProductId, RelatedProductId, RelationType);
GO

-- ============================================================
-- Product Reviews (ratings/comments — no moderation queue today, IsApproved
-- defaults to 1; a future moderation workflow can filter on it without a
-- schema change)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductReviews')
CREATE TABLE ProductReviews (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    CustomerId  UNIQUEIDENTIFIER NOT NULL,
    Rating      INT              NOT NULL,
    Comment     NVARCHAR(2000)   NULL,
    IsApproved  BIT              NOT NULL DEFAULT 1,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProductReviews_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
    CONSTRAINT CK_ProductReviews_Rating CHECK (Rating BETWEEN 1 AND 5)
);
CREATE INDEX IX_ProductReviews_Product_Approved ON ProductReviews (ProductId, IsApproved);
GO
