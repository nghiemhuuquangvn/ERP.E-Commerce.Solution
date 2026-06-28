-- ============================================================
-- PurchasingDB Schema — PurchaseOrderService (Port 5003, Code: PO)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PurchasingDB')
    CREATE DATABASE PurchasingDB;
GO

USE PurchasingDB;
GO

-- ============================================================
-- Suppliers
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Suppliers')
CREATE TABLE Suppliers (
    Id              UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Code            NVARCHAR(50)     NOT NULL,
    Name            NVARCHAR(255)    NOT NULL,
    Email           NVARCHAR(255)    NULL,
    Phone           NVARCHAR(20)     NULL,
    ContactPerson   NVARCHAR(200)    NULL,
    TaxCode         NVARCHAR(50)     NULL,
    -- Address (owned)
    Address_Street      NVARCHAR(255)    NULL,
    Address_City        NVARCHAR(100)    NULL,
    Address_State       NVARCHAR(50)     NULL,
    Address_PostalCode  NVARCHAR(20)     NULL,
    Address_Country     NVARCHAR(100)    NULL,
    PaymentTermDays INT              NOT NULL DEFAULT 30,
    CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    IsActive        BIT              NOT NULL DEFAULT 1,
    Rating          DECIMAL(3, 2)    NULL, -- 0.00 - 5.00
    Notes           NVARCHAR(1000)   NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    PRIMARY KEY (Id, TenantId)
);
CREATE UNIQUE INDEX IX_Suppliers_Code ON Suppliers (Code, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Suppliers_TenantId    ON Suppliers (TenantId, IsActive);
GO

-- ============================================================
-- Purchase Orders
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseOrders')
CREATE TABLE PurchaseOrders (
    Id                      UNIQUEIDENTIFIER NOT NULL,
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    PONumber                NVARCHAR(50)     NOT NULL,
    SupplierId              UNIQUEIDENTIFIER NOT NULL,
    Status                  INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Submitted, 2=Approved, 3=Received, 4=Cancelled
    OrderDate               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ExpectedDeliveryDate    DATETIME2        NULL,
    ActualDeliveryDate      DATETIME2        NULL,
    TotalAmount_Amount      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    TotalAmount_Currency    NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    Notes                   NVARCHAR(1000)   NULL,
    ApprovedBy              NVARCHAR(256)    NULL,
    ApprovedAt              DATETIME2        NULL,
    CancelledBy             NVARCHAR(256)    NULL,
    CancelledAt             DATETIME2        NULL,
    CancellationReason      NVARCHAR(500)    NULL,
    RowVersion              ROWVERSION       NOT NULL,
    CreatedBy               NVARCHAR(256)    NULL,
    CreatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy               NVARCHAR(256)    NULL,
    UpdatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted               BIT              NOT NULL DEFAULT 0,
    PRIMARY KEY (Id, TenantId),
    CONSTRAINT FK_PO_Supplier FOREIGN KEY (SupplierId, TenantId) REFERENCES Suppliers(Id, TenantId)
);
CREATE UNIQUE INDEX IX_PO_Number     ON PurchaseOrders (PONumber, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_PO_SupplierId        ON PurchaseOrders (SupplierId, TenantId);
CREATE INDEX IX_PO_Status            ON PurchaseOrders (Status, OrderDate DESC);
CREATE INDEX IX_PO_TenantId          ON PurchaseOrders (TenantId, OrderDate DESC);
GO

-- ============================================================
-- Purchase Order Items
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseOrderItems')
CREATE TABLE PurchaseOrderItems (
    Id                  UNIQUEIDENTIFIER NOT NULL,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    PurchaseOrderId     UNIQUEIDENTIFIER NOT NULL,
    ProductId           UNIQUEIDENTIFIER NULL,
    ProductCode         NVARCHAR(50)     NOT NULL,
    ProductName         NVARCHAR(255)    NOT NULL,
    Quantity            DECIMAL(18, 2)   NOT NULL,
    ReceivedQuantity    DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    Unit                NVARCHAR(20)     NOT NULL DEFAULT 'pcs',
    UnitPrice_Amount    DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    UnitPrice_Currency  NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    TaxRate             DECIMAL(5, 2)    NOT NULL DEFAULT 0,
    TaxAmount           DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    DiscountPercent     DECIMAL(5, 2)    NOT NULL DEFAULT 0,
    LineTotal           DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    Status              INT              NOT NULL DEFAULT 0, -- 0=Pending, 1=PartiallyReceived, 2=FullyReceived
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    PRIMARY KEY (Id, TenantId),
    CONSTRAINT FK_POItems_PO FOREIGN KEY (PurchaseOrderId, TenantId) REFERENCES PurchaseOrders(Id, TenantId) ON DELETE CASCADE
);
CREATE INDEX IX_POItems_OrderId ON PurchaseOrderItems (PurchaseOrderId, TenantId);
GO

-- ============================================================
-- Goods Receipts (when PO is received)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GoodsReceipts')
CREATE TABLE GoodsReceipts (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    POId            UNIQUEIDENTIFIER NOT NULL,
    ReceiptNumber   NVARCHAR(50)     NOT NULL,
    ReceivedDate    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ReceivedBy      NVARCHAR(256)    NOT NULL,
    Notes           NVARCHAR(1000)   NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_GoodsReceipts_POId     ON GoodsReceipts (POId, TenantId);
CREATE INDEX IX_GoodsReceipts_TenantId ON GoodsReceipts (TenantId, ReceivedDate DESC);
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
