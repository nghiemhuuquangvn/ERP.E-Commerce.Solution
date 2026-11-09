-- ============================================================
-- ProductDB Migration 019 - Products.StockQuantity INT -> DECIMAL(18,6) (Warehouse upgrade W1, docs/architecture/adr-warehouse-upgrade.md)
-- Warehouse quantities become decimal; Product keeps a read-optimised copy. Deploy this BEFORE the Warehouse change.
-- Idempotent: only a column that is still INT is altered; its (possibly unnamed) default is replaced by a named one.
-- ============================================================

USE ProductDB;
GO

-- Products has filtered / computed-column indexes: altering it needs these options (sqlcmd defaults them OFF).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Products') AND name = 'StockQuantity' AND TYPE_NAME(user_type_id) = 'int')
BEGIN
    DECLARE @df NVARCHAR(256) = (SELECT dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Products') AND c.name = 'StockQuantity');
    IF @df IS NOT NULL EXEC('ALTER TABLE dbo.Products DROP CONSTRAINT ' + @df);
    ALTER TABLE dbo.Products ALTER COLUMN StockQuantity DECIMAL(18,6) NOT NULL;
    ALTER TABLE dbo.Products ADD CONSTRAINT DF_Products_StockQuantity DEFAULT 0 FOR StockQuantity;
END
GO
