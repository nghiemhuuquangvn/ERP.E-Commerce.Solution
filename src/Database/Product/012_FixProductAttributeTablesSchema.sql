-- ============================================================
-- ProductDB Schema Fix — ProductService (Port 5008, Code: PRD)
-- Migration 012: Rebuilds ProductAttributes / ProductAttributeOptions /
-- ProductAttributeValues with the schema the current EF Core entities
-- actually require.
--
-- Root cause: 007_RebuildProductsAndMissingTables.sql and
-- 003_AddProductAttributesAndDimensions.sql both define these three tables,
-- each guarded by `IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = ...)`.
-- On this database 007's older, incompatible shape (e.g. ProductAttributeValues
-- with just Id/VariantId/AttributeId/OptionId) won the race, so 003's
-- IF NOT EXISTS saw the tables already existed and silently no-opped. This
-- left the live schema out of sync with ProductAttribute/ProductAttributeOption/
-- ProductAttributeValue (ERP.PRD.Domain/Entities), causing
-- ElasticsearchProductIndexer.IndexAsync to fail with SQL error 207 (invalid
-- column name) for every product.
--
-- All three tables were verified empty (0 rows) on the live database before
-- writing this script, so a drop-and-recreate is safe — no data migration
-- needed. Also adds Description/RowVersion, which even 003's version of
-- these tables was missing (every BaseEntity descendant needs them; see the
-- same gap fixed for other tables in 008_FixMissingEntityColumns.sql).
-- ============================================================

USE ProductDB;
GO

-- Filtered indexes (WHERE IsDeleted = 0) below require this session option —
-- same fix already applied in 010/011 for the same reason.
SET QUOTED_IDENTIFIER ON;
GO

-- Drop child-to-parent (FK order), recreate parent-to-child.
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributeValues')
    DROP TABLE ProductAttributeValues;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributeOptions')
    DROP TABLE ProductAttributeOptions;
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributes')
    DROP TABLE ProductAttributes;
GO

-- ============================================================
-- ProductAttributes (EAV-lite attribute definitions)
-- ============================================================
CREATE TABLE ProductAttributes (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    CategoryId      UNIQUEIDENTIFIER NULL,
    Code            NVARCHAR(100)    NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    DataType        INT              NOT NULL, -- 0=Text,1=Number,2=Boolean,3=Date,4=SingleSelect,5=MultiSelect
    Unit            NVARCHAR(20)     NULL,
    IsVariantOption BIT              NOT NULL DEFAULT 0,
    IsRequired      BIT              NOT NULL DEFAULT 0,
    IsFilterable    BIT              NOT NULL DEFAULT 1,
    SortOrder       INT              NOT NULL DEFAULT 0,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_ProductAttribute_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);
CREATE UNIQUE INDEX IX_ProductAttributes_Code     ON ProductAttributes (TenantId, Code) WHERE IsDeleted = 0;
CREATE INDEX        IX_ProductAttributes_Category ON ProductAttributes (CategoryId);
GO

-- ============================================================
-- ProductAttributeOptions (values for SingleSelect/MultiSelect attributes)
-- ============================================================
CREATE TABLE ProductAttributeOptions (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    ProductAttributeId  UNIQUEIDENTIFIER NOT NULL,
    Value               NVARCHAR(200)    NOT NULL,
    SortOrder           INT              NOT NULL DEFAULT 0,
    Description         NVARCHAR(MAX)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_AttributeOption_Attribute FOREIGN KEY (ProductAttributeId) REFERENCES ProductAttributes(Id)
);
CREATE UNIQUE INDEX IX_AttributeOptions_Attribute_Value ON ProductAttributeOptions (ProductAttributeId, Value) WHERE IsDeleted = 0;
GO

-- ============================================================
-- ProductAttributeValues (EAV values for Products/ProductVariants)
-- ============================================================
CREATE TABLE ProductAttributeValues (
    Id                       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId                 UNIQUEIDENTIFIER NOT NULL,
    ProductAttributeId       UNIQUEIDENTIFIER NOT NULL,
    OwnerType                TINYINT          NOT NULL, -- 0=Product, 1=ProductVariant
    OwnerId                  UNIQUEIDENTIFIER NOT NULL,
    ValueText                NVARCHAR(500)    NULL,
    ValueNumber              DECIMAL(18, 4)   NULL,
    ValueBoolean             BIT              NULL,
    ValueDate                DATETIME2        NULL,
    ProductAttributeOptionId UNIQUEIDENTIFIER NULL,
    ValueOptionIdsJson       NVARCHAR(MAX)    NULL,
    Description              NVARCHAR(MAX)    NULL,
    CreatedBy                NVARCHAR(256)    NULL,
    CreatedAt                DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy                NVARCHAR(256)    NULL,
    UpdatedAt                DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted                BIT              NOT NULL DEFAULT 0,
    RowVersion               ROWVERSION,
    CONSTRAINT FK_AttributeValue_Attribute FOREIGN KEY (ProductAttributeId) REFERENCES ProductAttributes(Id),
    CONSTRAINT FK_AttributeValue_Option    FOREIGN KEY (ProductAttributeOptionId) REFERENCES ProductAttributeOptions(Id),
    CONSTRAINT CK_AttributeValue_OwnerType CHECK (OwnerType IN (0, 1))
);
CREATE UNIQUE INDEX IX_AttributeValues_Owner_Attribute  ON ProductAttributeValues (OwnerType, OwnerId, ProductAttributeId) WHERE IsDeleted = 0;
CREATE INDEX        IX_AttributeValues_Owner            ON ProductAttributeValues (OwnerType, OwnerId);
CREATE INDEX        IX_AttributeValues_Attribute_Number ON ProductAttributeValues (ProductAttributeId, ValueNumber) WHERE IsDeleted = 0;
CREATE INDEX        IX_AttributeValues_Attribute_Text   ON ProductAttributeValues (ProductAttributeId, ValueText) WHERE IsDeleted = 0;
GO
