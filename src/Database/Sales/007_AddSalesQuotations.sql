-- ============================================================
-- SalesDB Migration 007 — SalesQuotation aggregate (Quotation -> customer
-- approval -> convert-to-order flow proposed by docs/business/Sales/module-05-sales.md).
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesQuotations')
CREATE TABLE SalesQuotations (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    CustomerId      UNIQUEIDENTIFIER NOT NULL,
    CustomerName    NVARCHAR(200)    NOT NULL,
    CustomerEmail   NVARCHAR(255)    NOT NULL,
    QuotationNo     NVARCHAR(50)     NOT NULL,
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Sent, 2=Accepted, 3=Rejected, 4=Expired, 5=Converted
    ValidUntil      DATETIME2        NOT NULL,
    TotalAmount     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    ConvertedOrderId UNIQUEIDENTIFIER NULL,
    Description     NVARCHAR(MAX)    NULL,
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion      ROWVERSION
);
CREATE INDEX IX_SalesQuotations_CustomerId ON SalesQuotations (CustomerId);
GO

-- SalesQuotation : AggregateRoot inherits BaseEntity's TenantId/Description/IsDeleted/
-- CreatedBy/At/UpdatedBy/At/RowVersion columns, but the CREATE TABLE above (as originally
-- written) omitted all of them — every read (ListQuotationsQueryHandler -> GetAllAsync)
-- 500'd with "Invalid column name" against any DB where this migration already ran before
-- the fix. Guarded so it's a no-op on a fresh install (columns already exist from the
-- CREATE TABLE above) and a real patch on any DB that ran the old version of this file.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'TenantId')
    ALTER TABLE SalesQuotations ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'Description')
    ALTER TABLE SalesQuotations ADD Description NVARCHAR(MAX) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'IsDeleted')
    ALTER TABLE SalesQuotations ADD IsDeleted BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'CreatedBy')
    ALTER TABLE SalesQuotations ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'CreatedAt')
    ALTER TABLE SalesQuotations ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'UpdatedBy')
    ALTER TABLE SalesQuotations ADD UpdatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'UpdatedAt')
    ALTER TABLE SalesQuotations ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'RowVersion')
    ALTER TABLE SalesQuotations ADD RowVersion ROWVERSION;
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
