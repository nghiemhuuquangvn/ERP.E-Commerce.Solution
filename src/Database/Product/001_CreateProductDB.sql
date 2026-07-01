-- ============================================================
-- ProductDB Schema — ProductService (Port 5008, Code: PRD)
-- Categories, Products, Product Variants, Product Images, Price Lists
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ProductDB')
    CREATE DATABASE ProductDB;
GO

USE ProductDB;
GO

-- ============================================================
-- Categories
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Categories')
CREATE TABLE Categories (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(100)    NOT NULL,
    ParentCategoryId    UNIQUEIDENTIFIER NULL,
    SortOrder           INT              NOT NULL DEFAULT 0,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Category_Parent FOREIGN KEY (ParentCategoryId) REFERENCES Categories(Id)
);
CREATE INDEX IX_Categories_Parent   ON Categories (ParentCategoryId, SortOrder);
CREATE INDEX IX_Categories_TenantId ON Categories (TenantId);
GO

-- ============================================================
-- Products
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
CREATE TABLE Products (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Sku             NVARCHAR(50)     NOT NULL,
    Name            NVARCHAR(255)    NOT NULL,
    Description     NVARCHAR(2000)   NULL,
    ProductType     INT              NOT NULL DEFAULT 0, -- 0=Physical, 1=Digital, 2=Service, 3=Bundle
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Active, 2=Discontinued
    CategoryId      UNIQUEIDENTIFIER NULL,
    HasImage        BIT              NOT NULL DEFAULT 0,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);
CREATE UNIQUE INDEX IX_Products_Sku      ON Products (Sku) WHERE IsDeleted = 0;
CREATE INDEX IX_Products_Category        ON Products (CategoryId, Status);
CREATE INDEX IX_Products_TenantId        ON Products (TenantId, Status, CreatedAt DESC);
GO

-- ============================================================
-- Product Variants
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductVariants')
CREATE TABLE ProductVariants (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    Sku         NVARCHAR(50)     NOT NULL,
    Barcode     NVARCHAR(100)    NULL,
    Cost        DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    Weight      DECIMAL(18, 3)   NULL,
    Dimensions  NVARCHAR(100)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Variant_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
CREATE UNIQUE INDEX IX_ProductVariants_Sku ON ProductVariants (Sku) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductVariants_Product    ON ProductVariants (ProductId);
GO

-- ============================================================
-- Product Images
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductImages')
CREATE TABLE ProductImages (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
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
CREATE INDEX IX_ProductImages_Product ON ProductImages (ProductId, IsPrimary DESC, SortOrder);
GO

-- ============================================================
-- Price Lists
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PriceLists')
CREATE TABLE PriceLists (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
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
CREATE INDEX IX_PriceLists_Active ON PriceLists (IsActive, EffectiveDate DESC);
GO

-- ============================================================
-- Price List Items
-- ============================================================
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
CREATE UNIQUE INDEX IX_PriceListItems_Key ON PriceListItems (PriceListId, VariantId);
GO

-- ============================================================
-- Outbox Messages
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
CREATE INDEX IX_OutboxMessages_Unprocessed ON OutboxMessages (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL;
GO
