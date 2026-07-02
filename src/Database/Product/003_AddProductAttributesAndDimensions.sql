-- ============================================================
-- ProductDB Schema Update — ProductService (Port 5008, Code: PRD)
-- Migration 003: Product attributes (EAV-lite) + variant physical dimensions
--
-- Three tiers for product/variant specs, from most to least structured:
--   1. First-class columns — dimensions/weight on ProductVariants (Length/
--      Width/Height/DimensionUnit/WeightUnit) for frequently-used, strongly
--      typed shipping/logistics data that is queried directly in SQL.
--   2. ProductAttribute* (EAV-lite) — category-varying specs (e.g. "Color",
--      "Screen Size") modeled as attribute/value rows so new specs can be
--      added without schema changes, while remaining relationally
--      queryable/filterable (ValueNumber/ValueText are indexed).
--   3. ExtendedDataJson — opaque escape hatch on Products/ProductVariants for
--      arbitrary structured data that is never queried via SQL JSON
--      functions, only read/written as a whole blob by the owning aggregate.
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ProductDB')
    CREATE DATABASE ProductDB;
GO

USE ProductDB;
GO

-- ============================================================
-- ProductVariants: physical dimension columns + extended data
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'Length')
    ALTER TABLE ProductVariants ADD Length DECIMAL(18, 3) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'Width')
    ALTER TABLE ProductVariants ADD Width DECIMAL(18, 3) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'Height')
    ALTER TABLE ProductVariants ADD Height DECIMAL(18, 3) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'DimensionUnit')
    ALTER TABLE ProductVariants ADD DimensionUnit NVARCHAR(10) NOT NULL CONSTRAINT DF_ProductVariants_DimensionUnit DEFAULT 'cm';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'WeightUnit')
    ALTER TABLE ProductVariants ADD WeightUnit NVARCHAR(10) NOT NULL CONSTRAINT DF_ProductVariants_WeightUnit DEFAULT 'kg';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductVariants') AND name = 'ExtendedDataJson')
    ALTER TABLE ProductVariants ADD ExtendedDataJson NVARCHAR(MAX) NULL;
GO

-- ============================================================
-- Products: extended data
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ExtendedDataJson')
    ALTER TABLE Products ADD ExtendedDataJson NVARCHAR(MAX) NULL;
GO

-- ============================================================
-- ProductAttributes (EAV-lite attribute definitions)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributes')
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
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProductAttribute_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);
CREATE UNIQUE INDEX IX_ProductAttributes_Code     ON ProductAttributes (TenantId, Code) WHERE IsDeleted = 0;
CREATE INDEX        IX_ProductAttributes_Category ON ProductAttributes (CategoryId);
GO

-- ============================================================
-- ProductAttributeOptions (values for SingleSelect/MultiSelect attributes)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributeOptions')
CREATE TABLE ProductAttributeOptions (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    ProductAttributeId  UNIQUEIDENTIFIER NOT NULL,
    Value               NVARCHAR(200)    NOT NULL,
    SortOrder           INT              NOT NULL DEFAULT 0,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_AttributeOption_Attribute FOREIGN KEY (ProductAttributeId) REFERENCES ProductAttributes(Id)
);
CREATE UNIQUE INDEX IX_AttributeOptions_Attribute_Value ON ProductAttributeOptions (ProductAttributeId, Value) WHERE IsDeleted = 0;
GO

-- ============================================================
-- ProductAttributeValues (EAV values for Products/ProductVariants)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductAttributeValues')
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
    CreatedBy                NVARCHAR(256)    NULL,
    CreatedAt                DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy                NVARCHAR(256)    NULL,
    UpdatedAt                DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted                BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_AttributeValue_Attribute FOREIGN KEY (ProductAttributeId) REFERENCES ProductAttributes(Id),
    CONSTRAINT FK_AttributeValue_Option    FOREIGN KEY (ProductAttributeOptionId) REFERENCES ProductAttributeOptions(Id),
    CONSTRAINT CK_AttributeValue_OwnerType CHECK (OwnerType IN (0, 1))
);
CREATE UNIQUE INDEX IX_AttributeValues_Owner_Attribute  ON ProductAttributeValues (OwnerType, OwnerId, ProductAttributeId) WHERE IsDeleted = 0;
CREATE INDEX        IX_AttributeValues_Owner            ON ProductAttributeValues (OwnerType, OwnerId);
CREATE INDEX        IX_AttributeValues_Attribute_Number ON ProductAttributeValues (ProductAttributeId, ValueNumber) WHERE IsDeleted = 0;
CREATE INDEX        IX_AttributeValues_Attribute_Text   ON ProductAttributeValues (ProductAttributeId, ValueText) WHERE IsDeleted = 0;
GO
