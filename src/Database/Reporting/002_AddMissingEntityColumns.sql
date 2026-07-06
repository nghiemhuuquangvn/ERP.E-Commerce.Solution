-- ============================================================
-- ReportingDB Schema Fix — ReportingService (Port 5014, Code: RPT)
-- Migration 002: Adds columns that C# entity models reference but the
-- original CREATE TABLE scripts omitted or named differently.
-- All DDL is guarded with IF NOT EXISTS — safe to run multiple times.
-- ============================================================

USE ReportingDB;
GO

-- ============================================================
-- FactSalesOrders — entity: CustomerName, ItemCount, OrderDate,
--                           ApprovedAt, ShippedAt, DeliveredAt, DateKey
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactSalesOrders') AND name = 'CustomerName')
    ALTER TABLE FactSalesOrders ADD CustomerName NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactSalesOrders') AND name = 'ItemCount')
    ALTER TABLE FactSalesOrders ADD ItemCount INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactSalesOrders') AND name = 'OrderDate')
    ALTER TABLE FactSalesOrders ADD OrderDate DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactSalesOrders') AND name = 'ApprovedAt')
    ALTER TABLE FactSalesOrders ADD ApprovedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactSalesOrders') AND name = 'ShippedAt')
    ALTER TABLE FactSalesOrders ADD ShippedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactSalesOrders') AND name = 'DeliveredAt')
    ALTER TABLE FactSalesOrders ADD DeliveredAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactSalesOrders') AND name = 'DateKey')
    ALTER TABLE FactSalesOrders ADD DateKey INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactSalesOrders_DateKey' AND object_id = OBJECT_ID('FactSalesOrders'))
    CREATE INDEX IX_FactSalesOrders_DateKey ON FactSalesOrders (DateKey);
GO

-- ============================================================
-- FactPayments — entity: ProcessedAt, DateKey
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPayments') AND name = 'ProcessedAt')
    ALTER TABLE FactPayments ADD ProcessedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPayments') AND name = 'DateKey')
    ALTER TABLE FactPayments ADD DateKey INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactPayments_DateKey' AND object_id = OBJECT_ID('FactPayments'))
    CREATE INDEX IX_FactPayments_DateKey ON FactPayments (DateKey);
GO

-- ============================================================
-- DimDate — entity uses 'Date' (DB has 'FullDate') and 'Day' (DB has 'DayOfWeek')
-- Add aliased columns so EF convention-based mapping works
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DimDate') AND name = 'Date')
    ALTER TABLE DimDate ADD Date DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DimDate') AND name = 'Day')
    ALTER TABLE DimDate ADD Day INT NOT NULL DEFAULT 0;
GO

-- ============================================================
-- FactInventoryMovements — entity: OccurredAt, DateKey
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactInventoryMovements') AND name = 'OccurredAt')
    ALTER TABLE FactInventoryMovements ADD OccurredAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactInventoryMovements') AND name = 'DateKey')
    ALTER TABLE FactInventoryMovements ADD DateKey INT NOT NULL DEFAULT 0;
GO

-- ============================================================
-- FactPOSTransactions — entity: StoreId, TaxAmount, OccurredAt, DateKey
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPOSTransactions') AND name = 'StoreId')
    ALTER TABLE FactPOSTransactions ADD StoreId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPOSTransactions') AND name = 'TaxAmount')
    ALTER TABLE FactPOSTransactions ADD TaxAmount DECIMAL(18, 2) NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPOSTransactions') AND name = 'OccurredAt')
    ALTER TABLE FactPOSTransactions ADD OccurredAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FactPOSTransactions') AND name = 'DateKey')
    ALTER TABLE FactPOSTransactions ADD DateKey INT NOT NULL DEFAULT 0;
GO

-- ============================================================
-- DimProducts — entity: Price (DB has 'BasePrice'), IsActive
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DimProducts') AND name = 'Price')
    ALTER TABLE DimProducts ADD Price DECIMAL(18, 2) NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DimProducts') AND name = 'IsActive')
    ALTER TABLE DimProducts ADD IsActive BIT NOT NULL DEFAULT 1;
GO

-- ============================================================
-- FactHRPayslips — missing from DB entirely; create it
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FactHRPayslips')
CREATE TABLE FactHRPayslips (
    PayslipId       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeId      UNIQUEIDENTIFIER NOT NULL,
    EmployeeName    NVARCHAR(256)    NOT NULL DEFAULT '',
    Period          NVARCHAR(20)     NOT NULL DEFAULT '',
    BasicSalary     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    OvertimePay     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    TotalAmount     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    PaidAt          DATETIME2        NULL,
    DateKey         INT              NOT NULL DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactHRPayslips_EmployeeId' AND object_id = OBJECT_ID('FactHRPayslips'))
    CREATE INDEX IX_FactHRPayslips_EmployeeId ON FactHRPayslips (EmployeeId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactHRPayslips_Period' AND object_id = OBJECT_ID('FactHRPayslips'))
    CREATE INDEX IX_FactHRPayslips_Period ON FactHRPayslips (Period);
GO
