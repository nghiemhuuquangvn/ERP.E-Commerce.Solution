-- ============================================================
-- ProductDB Schema Fix — ProductService (Port 5008, Code: PRD)
-- Migration 013: Two independent schema-drift bugs found while live-verifying
-- the product image upload / attribute value endpoints (2026-08-07).
--
-- Bug 1 — ProductImages is missing 4 BaseEntity columns.
-- CreatedBy/Description/UpdatedBy/RowVersion exist on every other BaseEntity
-- table in this DB (Products, ProductVariants, ProductAttributes, ...) but
-- were never added to ProductImages by 001_CreateProductDB.sql or any later
-- script. Any EF query that loads Product.Images (e.g.
-- ProductRepository.GetByIdWithImagesAsync) throws "Invalid column name
-- 'CreatedBy'" etc. — this breaks every image endpoint (list/upload/
-- remove/set-primary), not just the new file-upload one.
--
-- Bug 2 — ProductAttributeValues.OwnerType is TINYINT but EF expects INT.
-- 012_FixProductAttributeTablesSchema.sql explicitly declared OwnerType as
-- TINYINT (line 102 of that script) — a reasonable-looking storage choice
-- since AttributeOwnerType only has 2 members. But ERP.PRD.Domain.Entities.
-- ProductAttributeValue.OwnerType is typed as the C# enum AttributeOwnerType
-- (default underlying type `int`), and with zero IEntityTypeConfiguration
-- classes in this service (confirmed: EF logs "No instantiatable types
-- implementing IEntityTypeConfiguration were found" on every boot), EF maps
-- it via pure convention to SQL INT — not TINYINT. Every other enum-backed
-- column in this schema (DataType, ProductType, ProductStatus, ...) is
-- INT for the same reason, so TINYINT here was the actual outlier, not a
-- deliberate documented exception. Every read of this column throws
-- "InvalidCastException: Unable to cast object of type 'System.Byte' to
-- type 'System.Int32'" — breaks GetProductAttributeValuesHandler (reading
-- attribute values back) and DeleteProductAttributeCommand (which checks for
-- existing values first). SetProductAttributeValueCommand's create path
-- doesn't select the column back, so writing a *new* value silently
-- succeeded while reading it immediately failed.
--
-- Both bugs are additive/widening changes — safe to apply against live data.
-- ============================================================

USE ProductDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Bug 1: add the missing BaseEntity columns to ProductImages
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductImages') AND name = 'CreatedBy')
    ALTER TABLE ProductImages ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductImages') AND name = 'Description')
    ALTER TABLE ProductImages ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductImages') AND name = 'UpdatedBy')
    ALTER TABLE ProductImages ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductImages') AND name = 'RowVersion')
    ALTER TABLE ProductImages ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- Bug 2: widen ProductAttributeValues.OwnerType from TINYINT to INT.
-- The CHECK constraint and both indexes referencing OwnerType must be
-- dropped before ALTER COLUMN and recreated identically afterward — SQL
-- Server refuses to alter a column any object still depends on.
-- ============================================================
IF EXISTS (SELECT 1 FROM sys.columns c
           WHERE c.object_id = OBJECT_ID('ProductAttributeValues') AND c.name = 'OwnerType'
             AND TYPE_NAME(c.user_type_id) = 'tinyint')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE object_id = OBJECT_ID('CK_AttributeValue_OwnerType'))
        ALTER TABLE ProductAttributeValues DROP CONSTRAINT CK_AttributeValue_OwnerType;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ProductAttributeValues') AND name = 'IX_AttributeValues_Owner_Attribute')
        DROP INDEX IX_AttributeValues_Owner_Attribute ON ProductAttributeValues;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ProductAttributeValues') AND name = 'IX_AttributeValues_Owner')
        DROP INDEX IX_AttributeValues_Owner ON ProductAttributeValues;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns c
           WHERE c.object_id = OBJECT_ID('ProductAttributeValues') AND c.name = 'OwnerType'
             AND TYPE_NAME(c.user_type_id) = 'tinyint')
    ALTER TABLE ProductAttributeValues ALTER COLUMN OwnerType INT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE object_id = OBJECT_ID('CK_AttributeValue_OwnerType'))
    ALTER TABLE ProductAttributeValues ADD CONSTRAINT CK_AttributeValue_OwnerType CHECK (OwnerType IN (0, 1));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ProductAttributeValues') AND name = 'IX_AttributeValues_Owner_Attribute')
    CREATE UNIQUE INDEX IX_AttributeValues_Owner_Attribute ON ProductAttributeValues (OwnerType, OwnerId, ProductAttributeId) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ProductAttributeValues') AND name = 'IX_AttributeValues_Owner')
    CREATE INDEX IX_AttributeValues_Owner ON ProductAttributeValues (OwnerType, OwnerId);
GO
