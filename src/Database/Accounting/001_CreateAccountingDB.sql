-- ============================================================
-- AccountingDB Schema — AccountingService (Port 5005, Code: ACC)
-- General Ledger + AR/AP + Fiscal Periods
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AccountingDB')
    CREATE DATABASE AccountingDB;
GO

USE AccountingDB;
GO

-- ============================================================
-- Chart of Accounts
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Accounts')
CREATE TABLE Accounts (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    AccountNumber   NVARCHAR(20)     NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    AccountType     INT              NOT NULL, -- 0=Asset, 1=Liability, 2=Equity, 3=Revenue, 4=Expense
    NormalBalance   INT              NOT NULL, -- 0=Debit, 1=Credit
    ParentAccountId UNIQUEIDENTIFIER NULL,
    Level           INT              NOT NULL DEFAULT 1,
    IsControl       BIT              NOT NULL DEFAULT 0,
    CurrentBalance  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_Accounts_Number ON Accounts (AccountNumber, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Accounts_TenantId      ON Accounts (TenantId, AccountType, IsActive);
CREATE INDEX IX_Accounts_Parent        ON Accounts (ParentAccountId) WHERE ParentAccountId IS NOT NULL;
GO

-- ============================================================
-- Fiscal Periods
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FiscalPeriods')
CREATE TABLE FiscalPeriods (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(100)    NOT NULL,
    StartDate   DATE             NOT NULL,
    EndDate     DATE             NOT NULL,
    FiscalYear  INT              NOT NULL,
    PeriodMonth INT              NOT NULL, -- 1-12
    Status      INT              NOT NULL DEFAULT 0, -- 0=Open, 1=Closed, 2=Locked
    ClosedAt    DATETIME2        NULL,
    ClosedBy    NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE UNIQUE INDEX IX_FiscalPeriods_Period ON FiscalPeriods (TenantId, FiscalYear, PeriodMonth);
CREATE INDEX IX_FiscalPeriods_Status       ON FiscalPeriods (TenantId, Status);
GO

-- ============================================================
-- Journal Entries (double-entry bookkeeping)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JournalEntries')
CREATE TABLE JournalEntries (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    EntryNumber     NVARCHAR(50)     NOT NULL,
    FiscalPeriodId  UNIQUEIDENTIFIER NOT NULL,
    Description     NVARCHAR(500)    NOT NULL,
    EntryDate       DATE             NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
    TotalDebit      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    TotalCredit     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Posted, 2=Voided
    ReferenceType   NVARCHAR(100)    NULL, -- Invoice, Bill, SalesOrder, PurchaseOrder, Manual
    ReferenceId     UNIQUEIDENTIFIER NULL,
    PostedAt        DATETIME2        NULL,
    PostedBy        NVARCHAR(256)    NULL,
    VoidedAt        DATETIME2        NULL,
    VoidedBy        NVARCHAR(256)    NULL,
    VoidReason      NVARCHAR(500)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_JE_FiscalPeriod FOREIGN KEY (FiscalPeriodId) REFERENCES FiscalPeriods(Id)
);
CREATE UNIQUE INDEX IX_JE_Number ON JournalEntries (EntryNumber, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_JE_FiscalPeriod  ON JournalEntries (FiscalPeriodId, Status);
CREATE INDEX IX_JE_TenantId      ON JournalEntries (TenantId, EntryDate DESC);
CREATE INDEX IX_JE_Reference     ON JournalEntries (ReferenceId) WHERE ReferenceId IS NOT NULL;
GO

-- ============================================================
-- Journal Entry Lines (debit/credit line items)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JournalEntryLines')
CREATE TABLE JournalEntryLines (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    JournalEntryId  UNIQUEIDENTIFIER NOT NULL,
    AccountId       UNIQUEIDENTIFIER NOT NULL,
    Description     NVARCHAR(500)    NULL,
    DebitAmount     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    CreditAmount    DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    LineOrder       INT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_JELine_Entry   FOREIGN KEY (JournalEntryId) REFERENCES JournalEntries(Id) ON DELETE CASCADE,
    CONSTRAINT FK_JELine_Account FOREIGN KEY (AccountId) REFERENCES Accounts(Id),
    CONSTRAINT CHK_JELine_OneAmount CHECK (
        (DebitAmount > 0 AND CreditAmount = 0) OR
        (CreditAmount > 0 AND DebitAmount = 0)
    )
);
CREATE INDEX IX_JELines_EntryId   ON JournalEntryLines (JournalEntryId);
CREATE INDEX IX_JELines_AccountId ON JournalEntryLines (AccountId, TenantId);
GO

-- ============================================================
-- Invoices (Accounts Receivable)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Invoices')
CREATE TABLE Invoices (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    InvoiceNumber   NVARCHAR(50)     NOT NULL,
    CustomerId      UNIQUEIDENTIFIER NOT NULL,
    CustomerName    NVARCHAR(300)    NOT NULL,
    CustomerEmail   NVARCHAR(256)    NULL,
    OrderId         UNIQUEIDENTIFIER NULL,
    InvoiceDate     DATE             NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
    DueDate         DATE             NOT NULL,
    SubTotal        DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    TaxAmount       DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    TotalAmount     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    AmountPaid      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    AmountDue       AS (TotalAmount - AmountPaid) PERSISTED,
    CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Issued, 2=Partial, 3=Paid, 4=Overdue, 5=Void
    Notes           NVARCHAR(1000)   NULL,
    SentAt          DATETIME2        NULL,
    PaidAt          DATETIME2        NULL,
    VoidedAt        DATETIME2        NULL,
    VoidReason      NVARCHAR(500)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_Invoices_Number   ON Invoices (InvoiceNumber, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Invoices_CustomerId      ON Invoices (CustomerId, Status);
CREATE INDEX IX_Invoices_TenantId        ON Invoices (TenantId, InvoiceDate DESC);
CREATE INDEX IX_Invoices_Due             ON Invoices (DueDate, Status) WHERE Status IN (1, 2);
GO

-- ============================================================
-- Invoice Lines
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InvoiceLines')
CREATE TABLE InvoiceLines (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    InvoiceId   UNIQUEIDENTIFIER NOT NULL,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Description NVARCHAR(500)    NOT NULL,
    Quantity    DECIMAL(18, 2)   NOT NULL DEFAULT 1,
    UnitPrice   DECIMAL(18, 2)   NOT NULL,
    TaxRate     DECIMAL(5, 2)    NOT NULL DEFAULT 0,
    TaxAmount   DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    LineTotal   DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    AccountId   UNIQUEIDENTIFIER NULL,
    LineOrder   INT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_InvoiceLine_Invoice FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE
);
CREATE INDEX IX_InvoiceLines_InvoiceId ON InvoiceLines (InvoiceId);
GO

-- ============================================================
-- Bills (Accounts Payable)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Bills')
CREATE TABLE Bills (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    BillNumber      NVARCHAR(50)     NOT NULL,
    SupplierId      UNIQUEIDENTIFIER NOT NULL,
    SupplierName    NVARCHAR(300)    NOT NULL,
    POId            UNIQUEIDENTIFIER NULL,
    SupplierInvoiceRef NVARCHAR(100) NULL,
    BillDate        DATE             NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
    DueDate         DATE             NOT NULL,
    SubTotal        DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    TaxAmount       DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    TotalAmount     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    AmountPaid      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    AmountDue       AS (TotalAmount - AmountPaid) PERSISTED,
    CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Approved, 2=Partial, 3=Paid, 4=Overdue
    Notes           NVARCHAR(1000)   NULL,
    ApprovedAt      DATETIME2        NULL,
    ApprovedBy      NVARCHAR(256)    NULL,
    PaidAt          DATETIME2        NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_Bills_Number ON Bills (BillNumber, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Bills_SupplierId   ON Bills (SupplierId, Status);
CREATE INDEX IX_Bills_TenantId     ON Bills (TenantId, BillDate DESC);
CREATE INDEX IX_Bills_Due          ON Bills (DueDate, Status) WHERE Status IN (1, 2);
GO

-- ============================================================
-- Bill Lines
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BillLines')
CREATE TABLE BillLines (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    BillId      UNIQUEIDENTIFIER NOT NULL,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Description NVARCHAR(500)    NOT NULL,
    Quantity    DECIMAL(18, 2)   NOT NULL DEFAULT 1,
    UnitPrice   DECIMAL(18, 2)   NOT NULL,
    TaxRate     DECIMAL(5, 2)    NOT NULL DEFAULT 0,
    TaxAmount   DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    LineTotal   DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    AccountId   UNIQUEIDENTIFIER NULL,
    LineOrder   INT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_BillLine_Bill FOREIGN KEY (BillId) REFERENCES Bills(Id) ON DELETE CASCADE
);
CREATE INDEX IX_BillLines_BillId ON BillLines (BillId);
GO

-- ============================================================
-- Outbox Messages
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutboxMessages')
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
CREATE INDEX IX_OutboxMessages_Unprocessed ON OutboxMessages (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL;
GO

-- ============================================================
-- Seed default Chart of Accounts (basic GL structure)
-- ============================================================
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

-- Assets (1xxx)
IF NOT EXISTS (SELECT 1 FROM Accounts WHERE AccountNumber = '1000' AND TenantId = @TenantId)
BEGIN
    INSERT INTO Accounts (Id, TenantId, AccountNumber, Name, AccountType, NormalBalance, Level, IsControl)
    VALUES
        (NEWID(), @TenantId, '1000', 'Cash & Cash Equivalents',   0, 0, 1, 1),
        (NEWID(), @TenantId, '1100', 'Accounts Receivable',        0, 0, 1, 1),
        (NEWID(), @TenantId, '1200', 'Inventory',                  0, 0, 1, 0),
        (NEWID(), @TenantId, '1500', 'Fixed Assets',               0, 0, 1, 0),
        -- Liabilities (2xxx)
        (NEWID(), @TenantId, '2000', 'Accounts Payable',           1, 1, 1, 1),
        (NEWID(), @TenantId, '2100', 'Accrued Liabilities',        1, 1, 1, 0),
        (NEWID(), @TenantId, '2200', 'Tax Payable',                1, 1, 1, 0),
        -- Equity (3xxx)
        (NEWID(), @TenantId, '3000', 'Owner Equity',               2, 1, 1, 0),
        (NEWID(), @TenantId, '3100', 'Retained Earnings',          2, 1, 1, 0),
        -- Revenue (4xxx)
        (NEWID(), @TenantId, '4000', 'Sales Revenue',              3, 1, 1, 0),
        (NEWID(), @TenantId, '4100', 'Service Revenue',            3, 1, 1, 0),
        -- Expenses (5xxx)
        (NEWID(), @TenantId, '5000', 'Cost of Goods Sold',         4, 0, 1, 0),
        (NEWID(), @TenantId, '5100', 'Operating Expenses',         4, 0, 1, 0),
        (NEWID(), @TenantId, '5200', 'Salaries & Benefits',        4, 0, 1, 0);
END
GO
