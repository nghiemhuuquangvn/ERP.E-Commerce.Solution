-- ============================================================
-- ReportingDB Migration 010 - FactInventoryMovements.Quantity and DimProducts.StockQuantity INT -> DECIMAL(18,6) (Warehouse upgrade W1)
-- Deploy this BEFORE the Warehouse change. Idempotent: only a column that is still INT is altered.
-- ============================================================

USE ReportingDB;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FactInventoryMovements') AND name = 'Quantity' AND TYPE_NAME(user_type_id) = 'int')
BEGIN
    DECLARE @df1 NVARCHAR(256) = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns k ON k.object_id = dc.parent_object_id AND k.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.FactInventoryMovements') AND k.name = 'Quantity');
    IF @df1 IS NOT NULL EXEC('ALTER TABLE dbo.FactInventoryMovements DROP CONSTRAINT ' + @df1);
    ALTER TABLE dbo.FactInventoryMovements ALTER COLUMN Quantity DECIMAL(18,6) NOT NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DimProducts') AND name = 'StockQuantity' AND TYPE_NAME(user_type_id) = 'int')
BEGIN
    DECLARE @df2 NVARCHAR(256) = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns k ON k.object_id = dc.parent_object_id AND k.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.DimProducts') AND k.name = 'StockQuantity');
    IF @df2 IS NOT NULL EXEC('ALTER TABLE dbo.DimProducts DROP CONSTRAINT ' + @df2);
    ALTER TABLE dbo.DimProducts ALTER COLUMN StockQuantity DECIMAL(18,6) NOT NULL;
    ALTER TABLE dbo.DimProducts ADD CONSTRAINT DF_DimProducts_StockQuantity DEFAULT 0 FOR StockQuantity;
END
GO
