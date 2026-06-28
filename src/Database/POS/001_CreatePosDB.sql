-- ============================================================
-- PosDB Schema
-- Service: POSService (Port 5011, Code: POS)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PosDB')
    CREATE DATABASE PosDB;
GO

USE PosDB;
GO

CREATE TABLE Terminals (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(100)    NOT NULL,
    LocationId  UNIQUEIDENTIFIER NULL,
    IsActive    BIT              NOT NULL DEFAULT 1,
    LastSyncAt  DATETIME2        NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_Terminals_TenantId ON Terminals (TenantId);
GO

CREATE TABLE CashShifts (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    TerminalId      UNIQUEIDENTIFIER NOT NULL,
    CashierId       NVARCHAR(100)    NOT NULL,
    OpenedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ClosedAt        DATETIME2        NULL,
    OpeningBalance  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    ClosingBalance  DECIMAL(18,2)    NULL,
    TotalSales      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TransactionCount INT             NOT NULL DEFAULT 0,
    Notes           NVARCHAR(1000)   NULL,
    Status          INT              NOT NULL DEFAULT 0 -- 0=Open, 1=Closed
);

CREATE INDEX IX_CashShifts_TerminalId ON CashShifts (TerminalId);
CREATE INDEX IX_CashShifts_TenantId   ON CashShifts (TenantId);
GO

CREATE TABLE Transactions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    TerminalId      UNIQUEIDENTIFIER NOT NULL,
    CashierId       NVARCHAR(100)    NOT NULL,
    CashShiftId     UNIQUEIDENTIFIER NULL,
    Status          INT              NOT NULL, -- 0=Draft, 1=Completed, 2=Voided
    SubTotal        DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TaxAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    DiscountAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TotalAmount     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    PaymentMethod   INT              NULL, -- 0=Cash, 1=CreditCard, 2=EWallet
    PromotionId     UNIQUEIDENTIFIER NULL,
    VoidReason      NVARCHAR(500)    NULL,
    CompletedAt     DATETIME2        NULL,
    VoidedAt        DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (CashShiftId) REFERENCES CashShifts(Id)
);

CREATE INDEX IX_Transactions_TerminalId ON Transactions (TerminalId, Status);
CREATE INDEX IX_Transactions_TenantId   ON Transactions (TenantId);
CREATE INDEX IX_Transactions_CashShiftId ON Transactions (CashShiftId);
GO

CREATE TABLE TransactionItems (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TransactionId   UNIQUEIDENTIFIER NOT NULL,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    ProductName     NVARCHAR(300)    NOT NULL,
    Quantity        INT              NOT NULL,
    UnitPrice       DECIMAL(18,2)    NOT NULL,
    TaxRate         DECIMAL(5,2)     NOT NULL DEFAULT 0,
    TaxAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    DiscountAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    LineTotal       DECIMAL(18,2)    NOT NULL,
    FOREIGN KEY (TransactionId) REFERENCES Transactions(Id) ON DELETE CASCADE
);

CREATE INDEX IX_TransactionItems_TransactionId ON TransactionItems (TransactionId);
GO

CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
GO
