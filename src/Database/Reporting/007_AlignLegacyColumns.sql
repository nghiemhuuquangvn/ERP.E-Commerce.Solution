-- ============================================================
-- ReportingDB Migration 007 — legacy columns the fact/dimension models do not write (ROADMAP P0-9)
-- The Fact* entities are keyed by their business id (OrderId, PaymentId, ...) and carry DateKey; the
-- original tables also have a NOT NULL surrogate Id primary key with no default and legacy *DateKey /
-- TerminalId columns, so every consumer insert failed. DimDate keeps its DateKey key; FullDate / DayOfWeek are
-- not written by the model.
--   * Fact*.Id gets DEFAULT NEWID() (it is the primary key, so it cannot become NULL).
--   * The legacy date-key / terminal columns and DimDate.DayOfWeek / FullDate may be NULL (data preserved);
--     the unique FullDate index becomes filtered so several NULLs do not collide.
-- Idempotent.
-- ============================================================

USE ReportingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('FactSalesOrders') AND col_name(parent_object_id, parent_column_id) = 'Id')
    ALTER TABLE FactSalesOrders ADD CONSTRAINT DF_FactSalesOrders_Id DEFAULT NEWID() FOR Id;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('FactPayments') AND col_name(parent_object_id, parent_column_id) = 'Id')
    ALTER TABLE FactPayments ADD CONSTRAINT DF_FactPayments_Id DEFAULT NEWID() FOR Id;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('FactPOSTransactions') AND col_name(parent_object_id, parent_column_id) = 'Id')
    ALTER TABLE FactPOSTransactions ADD CONSTRAINT DF_FactPOSTransactions_Id DEFAULT NEWID() FOR Id;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('FactInventoryMovements') AND col_name(parent_object_id, parent_column_id) = 'Id')
    ALTER TABLE FactInventoryMovements ADD CONSTRAINT DF_FactInventoryMovements_Id DEFAULT NEWID() FOR Id;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactSalesOrders') AND name = 'OrderDateKey' AND is_nullable = 0)
    ALTER TABLE FactSalesOrders ALTER COLUMN OrderDateKey INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPayments') AND name = 'PaymentDateKey' AND is_nullable = 0)
    ALTER TABLE FactPayments ALTER COLUMN PaymentDateKey INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPOSTransactions') AND name = 'TxnDateKey' AND is_nullable = 0)
    ALTER TABLE FactPOSTransactions ALTER COLUMN TxnDateKey INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPOSTransactions') AND name = 'TerminalId' AND is_nullable = 0)
    ALTER TABLE FactPOSTransactions ALTER COLUMN TerminalId UNIQUEIDENTIFIER NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactInventoryMovements') AND name = 'MovementDateKey' AND is_nullable = 0)
    ALTER TABLE FactInventoryMovements ALTER COLUMN MovementDateKey INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DimDate') AND name = 'DayOfWeek' AND is_nullable = 0)
    ALTER TABLE DimDate ALTER COLUMN [DayOfWeek] INT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DimDate') AND name = 'FullDate' AND is_nullable = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DimDate_FullDate' AND object_id = OBJECT_ID('DimDate'))
        DROP INDEX IX_DimDate_FullDate ON DimDate;
    ALTER TABLE DimDate ALTER COLUMN FullDate DATE NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DimDate_FullDate' AND object_id = OBJECT_ID('DimDate'))
    CREATE UNIQUE INDEX IX_DimDate_FullDate ON DimDate (FullDate) WHERE FullDate IS NOT NULL;
GO
