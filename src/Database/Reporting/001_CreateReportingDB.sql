-- ============================================================
-- ReportingDB Schema (OLAP / Data Warehouse)
-- Service: ReportingService (Port 5014, Code: RPT)
-- Read-only star schema: Fact tables + Dimension tables
-- No Outbox (consumer-only, no events published)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ReportingDB')
    CREATE DATABASE ReportingDB;
GO

USE ReportingDB;
GO

-- ============================================================
-- Dimension Tables
-- ============================================================

CREATE TABLE DimDate (
    DateKey     INT              NOT NULL PRIMARY KEY, -- YYYYMMDD
    FullDate    DATE             NOT NULL,
    Year        INT              NOT NULL,
    Quarter     INT              NOT NULL,
    Month       INT              NOT NULL,
    MonthName   NVARCHAR(20)     NOT NULL,
    Week        INT              NOT NULL,
    DayOfWeek   INT              NOT NULL,
    DayName     NVARCHAR(15)     NOT NULL,
    IsWeekend   BIT              NOT NULL DEFAULT 0,
    IsHoliday   BIT              NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX IX_DimDate_FullDate ON DimDate (FullDate);
GO

CREATE TABLE DimCustomers (
    CustomerId  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Email       NVARCHAR(300)    NULL,
    FirstName   NVARCHAR(100)    NULL,
    LastName    NVARCHAR(100)    NULL,
    Country     NVARCHAR(100)    NULL,
    Tier        NVARCHAR(50)     NULL,
    FirstOrderDate DATE          NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_DimCustomers_TenantId ON DimCustomers (TenantId);
GO

CREATE TABLE DimProducts (
    ProductId   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Sku         NVARCHAR(100)    NULL,
    Name        NVARCHAR(300)    NOT NULL,
    Category    NVARCHAR(200)    NULL,
    BasePrice   DECIMAL(18,2)    NULL,
    Status      NVARCHAR(50)     NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_DimProducts_TenantId ON DimProducts (TenantId);
GO

-- ============================================================
-- Fact Tables
-- ============================================================

CREATE TABLE FactSalesOrders (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    OrderId         UNIQUEIDENTIFIER NOT NULL,
    OrderDateKey    INT              NOT NULL,
    CustomerId      UNIQUEIDENTIFIER NULL,
    TotalAmount     DECIMAL(18,2)    NOT NULL,
    LineItemCount   INT              NOT NULL DEFAULT 0,
    Status          NVARCHAR(50)     NOT NULL,
    Channel         NVARCHAR(50)     NULL DEFAULT 'Online',
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (OrderDateKey) REFERENCES DimDate(DateKey)
);

CREATE UNIQUE INDEX IX_FactSalesOrders_OrderId  ON FactSalesOrders (OrderId);
CREATE INDEX IX_FactSalesOrders_DateKey         ON FactSalesOrders (OrderDateKey);
CREATE INDEX IX_FactSalesOrders_TenantId        ON FactSalesOrders (TenantId);
GO

CREATE TABLE FactPayments (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    PaymentId       UNIQUEIDENTIFIER NOT NULL,
    OrderId         UNIQUEIDENTIFIER NOT NULL,
    PaymentDateKey  INT              NOT NULL,
    Amount          DECIMAL(18,2)    NOT NULL,
    Currency        NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    Method          NVARCHAR(50)     NOT NULL,
    Status          NVARCHAR(50)     NOT NULL,
    RefundedAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (PaymentDateKey) REFERENCES DimDate(DateKey)
);

CREATE UNIQUE INDEX IX_FactPayments_PaymentId ON FactPayments (PaymentId);
CREATE INDEX IX_FactPayments_DateKey          ON FactPayments (PaymentDateKey);
CREATE INDEX IX_FactPayments_TenantId         ON FactPayments (TenantId);
GO

CREATE TABLE FactInventoryMovements (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    MovementId      UNIQUEIDENTIFIER NOT NULL,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    MovementDateKey INT              NOT NULL,
    MovementType    NVARCHAR(50)     NOT NULL, -- Receipt, Issue, Transfer, Adjustment
    Quantity        INT              NOT NULL,
    ReferenceId     UNIQUEIDENTIFIER NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (MovementDateKey) REFERENCES DimDate(DateKey)
);

CREATE UNIQUE INDEX IX_FactInventory_MovementId ON FactInventoryMovements (MovementId);
CREATE INDEX IX_FactInventory_ProductId         ON FactInventoryMovements (ProductId);
CREATE INDEX IX_FactInventory_DateKey           ON FactInventoryMovements (MovementDateKey);
GO

CREATE TABLE FactPOSTransactions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    TransactionId   UNIQUEIDENTIFIER NOT NULL,
    TxnDateKey      INT              NOT NULL,
    TerminalId      UNIQUEIDENTIFIER NOT NULL,
    CashierId       NVARCHAR(100)    NOT NULL,
    TotalAmount     DECIMAL(18,2)    NOT NULL,
    ItemCount       INT              NOT NULL DEFAULT 0,
    PaymentMethod   NVARCHAR(50)     NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (TxnDateKey) REFERENCES DimDate(DateKey)
);

CREATE UNIQUE INDEX IX_FactPOS_TransactionId ON FactPOSTransactions (TransactionId);
CREATE INDEX IX_FactPOS_DateKey              ON FactPOSTransactions (TxnDateKey);
GO

-- ============================================================
-- Populate DimDate for 5 years (2020-01-01 to 2024-12-31)
-- Run once at initial setup
-- ============================================================
DECLARE @StartDate DATE = '2020-01-01';
DECLARE @EndDate   DATE = '2029-12-31';
DECLARE @Date      DATE = @StartDate;

WHILE @Date <= @EndDate
BEGIN
    INSERT INTO DimDate (DateKey, FullDate, Year, Quarter, Month, MonthName, Week, DayOfWeek, DayName, IsWeekend)
    VALUES (
        CAST(CONVERT(VARCHAR(8), @Date, 112) AS INT),
        @Date,
        YEAR(@Date),
        DATEPART(QUARTER, @Date),
        MONTH(@Date),
        DATENAME(MONTH, @Date),
        DATEPART(WEEK, @Date),
        DATEPART(WEEKDAY, @Date),
        DATENAME(WEEKDAY, @Date),
        CASE WHEN DATEPART(WEEKDAY, @Date) IN (1, 7) THEN 1 ELSE 0 END
    );
    SET @Date = DATEADD(DAY, 1, @Date);
END;
GO
