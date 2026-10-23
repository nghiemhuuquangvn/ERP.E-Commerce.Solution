-- ============================================================
-- ProductDB Schema Repair — Migration 017
-- ProductBundleItems, ProductRelations, ProductReviews and ProductUomConversions derive from
-- BaseEntity in code (TenantId, Description, IsDeleted, audit columns, RowVersion) but databases
-- provisioned from the first scripts have their legacy shape without those columns; 006/010/015
-- skip them (the table exists) so the tenant query filter and every read/write fails.
--
-- Rebuilds a table to the entity's shape when TenantId is missing. A table that holds rows is
-- never dropped: the script stops so the rows can be migrated by hand. Idempotent.
-- ============================================================

USE ProductDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ── ProductBundleItems ──────────────────────────────────────
IF OBJECT_ID('ProductBundleItems') IS NOT NULL AND COL_LENGTH('ProductBundleItems', 'TenantId') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM ProductBundleItems) THROW 50017, 'ProductBundleItems has the legacy shape and contains rows; migrate them manually before running 017.', 1;
    DROP TABLE ProductBundleItems;
END
GO
IF OBJECT_ID('ProductBundleItems') IS NULL
BEGIN
    CREATE TABLE ProductBundleItems (
        Id                 UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId           UNIQUEIDENTIFIER NOT NULL,
        Description        NVARCHAR(MAX)    NULL,
        BundleProductId    UNIQUEIDENTIFIER NOT NULL,
        ComponentProductId UNIQUEIDENTIFIER NOT NULL,
        Quantity           INT              NOT NULL DEFAULT 1,
        CreatedBy          NVARCHAR(256)    NULL,
        CreatedAt          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedBy          NVARCHAR(256)    NULL,
        UpdatedAt          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted          BIT              NOT NULL DEFAULT 0,
        RowVersion         ROWVERSION       NOT NULL,
        CONSTRAINT FK_ProductBundleItems_Bundle    FOREIGN KEY (BundleProductId)    REFERENCES Products(Id),
        CONSTRAINT FK_ProductBundleItems_Component FOREIGN KEY (ComponentProductId) REFERENCES Products(Id)
    );
    CREATE INDEX IX_ProductBundleItems_Bundle ON ProductBundleItems (BundleProductId);
    CREATE UNIQUE INDEX UX_ProductBundleItems_Bundle_Component ON ProductBundleItems (BundleProductId, ComponentProductId);
END
GO

-- ── ProductRelations ────────────────────────────────────────
IF OBJECT_ID('ProductRelations') IS NOT NULL AND COL_LENGTH('ProductRelations', 'TenantId') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM ProductRelations) THROW 50017, 'ProductRelations has the legacy shape and contains rows; migrate them manually before running 017.', 1;
    DROP TABLE ProductRelations;
END
GO
IF OBJECT_ID('ProductRelations') IS NULL
BEGIN
    CREATE TABLE ProductRelations (
        Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId         UNIQUEIDENTIFIER NOT NULL,
        Description      NVARCHAR(MAX)    NULL,
        ProductId        UNIQUEIDENTIFIER NOT NULL,
        RelatedProductId UNIQUEIDENTIFIER NOT NULL,
        RelationType     INT              NOT NULL,
        SortOrder        INT              NOT NULL DEFAULT 0,
        CreatedBy        NVARCHAR(256)    NULL,
        CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedBy        NVARCHAR(256)    NULL,
        UpdatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted        BIT              NOT NULL DEFAULT 0,
        RowVersion       ROWVERSION       NOT NULL,
        CONSTRAINT FK_ProductRelations_Product FOREIGN KEY (ProductId)        REFERENCES Products(Id),
        CONSTRAINT FK_ProductRelations_Related FOREIGN KEY (RelatedProductId) REFERENCES Products(Id)
    );
    CREATE INDEX IX_ProductRelations_Product_Type ON ProductRelations (ProductId, RelationType);
    CREATE UNIQUE INDEX UX_ProductRelations_Product_Related_Type ON ProductRelations (ProductId, RelatedProductId, RelationType);
END
GO

-- ── ProductReviews ──────────────────────────────────────────
IF OBJECT_ID('ProductReviews') IS NOT NULL AND COL_LENGTH('ProductReviews', 'TenantId') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM ProductReviews) THROW 50017, 'ProductReviews has the legacy shape and contains rows; migrate them manually before running 017.', 1;
    DROP TABLE ProductReviews;
END
GO
IF OBJECT_ID('ProductReviews') IS NULL
BEGIN
    CREATE TABLE ProductReviews (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        Description NVARCHAR(MAX)    NULL,
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
        RowVersion  ROWVERSION       NOT NULL,
        CONSTRAINT FK_ProductReviews_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT CK_ProductReviews_Rating CHECK (Rating BETWEEN 1 AND 5)
    );
    CREATE INDEX IX_ProductReviews_Product_Approved ON ProductReviews (ProductId, IsApproved);
END
GO

-- ── ProductUomConversions ───────────────────────────────────
IF OBJECT_ID('ProductUomConversions') IS NOT NULL AND COL_LENGTH('ProductUomConversions', 'TenantId') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM ProductUomConversions) THROW 50017, 'ProductUomConversions has the legacy shape and contains rows; migrate them manually before running 017.', 1;
    DROP TABLE ProductUomConversions;
END
GO
IF OBJECT_ID('ProductUomConversions') IS NULL
BEGIN
    CREATE TABLE ProductUomConversions (
        Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId         UNIQUEIDENTIFIER NOT NULL,
        Description      NVARCHAR(MAX)    NULL,
        ProductId        UNIQUEIDENTIFIER NOT NULL,
        FromUomId        UNIQUEIDENTIFIER NOT NULL,
        ToUomId          UNIQUEIDENTIFIER NOT NULL,
        ConversionFactor DECIMAL(18, 6)   NOT NULL,
        CreatedBy        NVARCHAR(256)    NULL,
        CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedBy        NVARCHAR(256)    NULL,
        UpdatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted        BIT              NOT NULL DEFAULT 0,
        RowVersion       ROWVERSION       NOT NULL,
        CONSTRAINT FK_ProductUomConversion_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
    );
    CREATE INDEX IX_ProductUomConversions_Product ON ProductUomConversions (ProductId);
    CREATE UNIQUE INDEX UX_ProductUomConversions_Product_From_To ON ProductUomConversions (ProductId, FromUomId, ToUomId);
END
GO
