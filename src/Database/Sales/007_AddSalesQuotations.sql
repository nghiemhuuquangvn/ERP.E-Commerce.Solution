-- ============================================================
-- SalesDB Migration 007 — SalesQuotation aggregate (Quotation -> customer
-- approval -> convert-to-order flow proposed by docs/business/Sales/module-05-sales.md).
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesQuotations')
CREATE TABLE SalesQuotations (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    CustomerId      UNIQUEIDENTIFIER NOT NULL,
    CustomerName    NVARCHAR(200)    NOT NULL,
    CustomerEmail   NVARCHAR(255)    NOT NULL,
    QuotationNo     NVARCHAR(50)     NOT NULL,
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Sent, 2=Accepted, 3=Rejected, 4=Expired, 5=Converted
    ValidUntil      DATETIME2        NOT NULL,
    TotalAmount     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    ConvertedOrderId UNIQUEIDENTIFIER NULL
);
CREATE INDEX IX_SalesQuotations_CustomerId ON SalesQuotations (CustomerId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SalesQuotations_QuotationNo')
    CREATE UNIQUE INDEX UX_SalesQuotations_QuotationNo ON SalesQuotations(QuotationNo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesQuotationItems')
CREATE TABLE SalesQuotationItems (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    QuotationId     UNIQUEIDENTIFIER NOT NULL,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    ProductName     NVARCHAR(500)    NOT NULL,
    Quantity        INT              NOT NULL,
    UnitPrice       DECIMAL(18, 2)   NOT NULL,
    CONSTRAINT FK_SalesQuotationItems_Quotation FOREIGN KEY (QuotationId) REFERENCES SalesQuotations(Id) ON DELETE CASCADE
);
CREATE INDEX IX_SalesQuotationItems_QuotationId ON SalesQuotationItems (QuotationId);
GO
