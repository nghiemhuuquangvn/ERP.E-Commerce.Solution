-- ============================================================
-- PurchasingDB Migration 012 - RFQ, supplier quotations, supplier invoices with three-way match, returns to vendor (ROADMAP P1-8)
--   RequestForQuotations / RfqLines / RfqInvitations / SupplierQuotations / QuotationLines
--   SupplierInvoices / SupplierInvoiceLines / SupplierInvoiceMatchLines
--   VendorReturns / VendorReturnLines
--   PurchasingSettings (per-tenant match tolerances)
-- All tables are tenant-owned and carry the BaseEntity columns. Idempotent.
-- ============================================================

USE PurchasingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RequestForQuotations')
CREATE TABLE RequestForQuotations (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    RfqNumber     NVARCHAR(50)     NOT NULL,
    Title         NVARCHAR(255)    NOT NULL,
    NeededBy      DATETIME2        NULL,
    Notes         NVARCHAR(1000)   NULL,
    Status        INT              NOT NULL DEFAULT 0,
    SentAt        DATETIME2        NULL,
    AwardedSupplierId UNIQUEIDENTIFIER NULL,
    AwardedPurchaseOrderId UNIQUEIDENTIFIER NULL,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_RequestForQuotations_Number')
    CREATE UNIQUE INDEX UX_RequestForQuotations_Number ON RequestForQuotations (TenantId, RfqNumber) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RfqLines')
CREATE TABLE RfqLines (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    RfqId         UNIQUEIDENTIFIER NOT NULL,
    ProductCode   NVARCHAR(50)     NOT NULL,
    ProductName   NVARCHAR(255)    NOT NULL,
    Quantity      DECIMAL(18, 2)   NOT NULL,
    Unit          NVARCHAR(20)     NULL,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RfqLines_Rfq')
    CREATE INDEX IX_RfqLines_Rfq ON RfqLines (TenantId, RfqId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RfqInvitations')
CREATE TABLE RfqInvitations (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    RfqId         UNIQUEIDENTIFIER NOT NULL,
    SupplierId    UNIQUEIDENTIFIER NOT NULL,
    State         INT              NOT NULL DEFAULT 0,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RfqInvitations_Rfq')
    CREATE INDEX IX_RfqInvitations_Rfq ON RfqInvitations (TenantId, RfqId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupplierQuotations')
CREATE TABLE SupplierQuotations (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    RfqId         UNIQUEIDENTIFIER NOT NULL,
    SupplierId    UNIQUEIDENTIFIER NOT NULL,
    QuoteNumber   NVARCHAR(50)     NULL,
    ValidUntil    DATETIME2        NOT NULL,
    CurrencyCode  NVARCHAR(3)      NOT NULL,
    LeadTimeDays  INT              NOT NULL DEFAULT 0,
    Notes         NVARCHAR(1000)   NULL,
    IsWinner      BIT              NOT NULL DEFAULT 0,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupplierQuotations_Rfq')
    CREATE INDEX IX_SupplierQuotations_Rfq ON SupplierQuotations (TenantId, RfqId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QuotationLines')
CREATE TABLE QuotationLines (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    QuotationId   UNIQUEIDENTIFIER NOT NULL,
    RfqLineId     UNIQUEIDENTIFIER NOT NULL,
    UnitPrice     DECIMAL(18, 4)   NOT NULL,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QuotationLines_Quotation')
    CREATE INDEX IX_QuotationLines_Quotation ON QuotationLines (TenantId, QuotationId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupplierInvoices')
CREATE TABLE SupplierInvoices (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    InvoiceNumber NVARCHAR(50)     NOT NULL,
    SupplierId    UNIQUEIDENTIFIER NOT NULL,
    PurchaseOrderId UNIQUEIDENTIFIER NOT NULL,
    PONumber      NVARCHAR(50)     NULL,
    InvoiceDate   DATETIME2        NOT NULL,
    DueDate       DATETIME2        NOT NULL,
    CurrencyCode  NVARCHAR(3)      NOT NULL,
    NetAmount     DECIMAL(18, 2)   NOT NULL,
    TaxAmount     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    Status        INT              NOT NULL DEFAULT 0,
    HoldReasons   NVARCHAR(2000)   NULL,
    ApprovalReason NVARCHAR(1000)  NULL,
    RejectionReason NVARCHAR(1000) NULL,
    ApprovedBy    NVARCHAR(256)    NULL,
    ApprovedAt    DATETIME2        NULL,
    ReceivedValue DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SupplierInvoices_Number')
    CREATE UNIQUE INDEX UX_SupplierInvoices_Number ON SupplierInvoices (TenantId, SupplierId, InvoiceNumber) WHERE IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupplierInvoices_PO')
    CREATE INDEX IX_SupplierInvoices_PO ON SupplierInvoices (TenantId, PurchaseOrderId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupplierInvoiceLines')
CREATE TABLE SupplierInvoiceLines (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    InvoiceId     UNIQUEIDENTIFIER NOT NULL,
    ProductCode   NVARCHAR(50)     NOT NULL,
    Quantity      DECIMAL(18, 2)   NOT NULL,
    UnitPrice     DECIMAL(18, 4)   NOT NULL,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupplierInvoiceLines_Invoice')
    CREATE INDEX IX_SupplierInvoiceLines_Invoice ON SupplierInvoiceLines (TenantId, InvoiceId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupplierInvoiceMatchLines')
CREATE TABLE SupplierInvoiceMatchLines (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    InvoiceId     UNIQUEIDENTIFIER NOT NULL,
    ProductCode   NVARCHAR(50)     NOT NULL,
    Ordered       DECIMAL(18, 2)   NOT NULL,
    Received      DECIMAL(18, 2)   NOT NULL,
    Returned      DECIMAL(18, 2)   NOT NULL,
    PreviouslyInvoiced DECIMAL(18, 2) NOT NULL,
    Invoiced      DECIMAL(18, 2)   NOT NULL,
    OrderPrice    DECIMAL(18, 4)   NOT NULL,
    InvoicedPrice DECIMAL(18, 4)   NOT NULL,
    PriceVariancePercent DECIMAL(9, 2) NOT NULL,
    QuantityVariance DECIMAL(18, 2) NOT NULL,
    Verdict       NVARCHAR(50)     NOT NULL,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupplierInvoiceMatchLines_Invoice')
    CREATE INDEX IX_SupplierInvoiceMatchLines_Invoice ON SupplierInvoiceMatchLines (TenantId, InvoiceId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'VendorReturns')
CREATE TABLE VendorReturns (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    ReturnNumber  NVARCHAR(50)     NOT NULL,
    PurchaseOrderId UNIQUEIDENTIFIER NOT NULL,
    PONumber      NVARCHAR(50)     NULL,
    SupplierId    UNIQUEIDENTIFIER NOT NULL,
    CurrencyCode  NVARCHAR(3)      NOT NULL,
    Reason        NVARCHAR(500)    NOT NULL,
    Status        INT              NOT NULL DEFAULT 0,
    ShippedAt     DATETIME2        NULL,
    CreditNoteNumber NVARCHAR(50)  NULL,
    CreditAmount  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    CreditedAt    DATETIME2        NULL,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_VendorReturns_Number')
    CREATE UNIQUE INDEX UX_VendorReturns_Number ON VendorReturns (TenantId, ReturnNumber) WHERE IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VendorReturns_PO')
    CREATE INDEX IX_VendorReturns_PO ON VendorReturns (TenantId, PurchaseOrderId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'VendorReturnLines')
CREATE TABLE VendorReturnLines (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    ReturnId      UNIQUEIDENTIFIER NOT NULL,
    ProductCode   NVARCHAR(50)     NOT NULL,
    ProductName   NVARCHAR(255)    NULL,
    Quantity      DECIMAL(18, 2)   NOT NULL,
    UnitPrice     DECIMAL(18, 4)   NOT NULL,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VendorReturnLines_Return')
    CREATE INDEX IX_VendorReturnLines_Return ON VendorReturnLines (TenantId, ReturnId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchasingSettings')
CREATE TABLE PurchasingSettings (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    QuantityTolerancePercent DECIMAL(9, 2) NOT NULL DEFAULT 0,
    PriceTolerancePercent    DECIMAL(9, 2) NOT NULL DEFAULT 2,
    AutoApproveMatched       BIT           NOT NULL DEFAULT 0,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(256)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion    ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PurchasingSettings_Tenant')
    CREATE UNIQUE INDEX UX_PurchasingSettings_Tenant ON PurchasingSettings (TenantId);
GO
