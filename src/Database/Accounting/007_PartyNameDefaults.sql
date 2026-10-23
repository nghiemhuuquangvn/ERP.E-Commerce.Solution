-- ============================================================
-- AccountingDB Schema Repair — Migration 007
-- Bills.SupplierName and Invoices.CustomerName are NOT NULL without a default, but the Bill and
-- Invoice entities do not map them (the integration events that create documents carry only
-- the party's Id). Every insert from the application failed with "Cannot insert the value NULL
-- into column 'SupplierName'", so no bill/invoice was ever booked from an event.
--
-- Gives both columns an empty-string default so the insert succeeds. KNOWN LIMITATION: the name
-- stays empty until the party's name is carried on the event and mapped on the entity
-- (docs/ROADMAP.md P1-1). Idempotent.
-- ============================================================

USE AccountingDB;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('Bills') AND c.name = 'SupplierName')
    ALTER TABLE Bills ADD CONSTRAINT DF_Bills_SupplierName DEFAULT N'' FOR SupplierName;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('Invoices') AND c.name = 'CustomerName')
    ALTER TABLE Invoices ADD CONSTRAINT DF_Invoices_CustomerName DEFAULT N'' FOR CustomerName;
GO
