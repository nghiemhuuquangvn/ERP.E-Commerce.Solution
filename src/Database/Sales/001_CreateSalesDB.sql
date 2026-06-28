-- ============================================================
-- SalesDB Schema — SaleOrderService (Port 5002, Code: SO)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'SalesDB')
    CREATE DATABASE SalesDB;
GO

USE SalesDB;
GO

-- ============================================================
-- Sales Orders
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesOrders')
CREATE TABLE SalesOrders (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    CustomerId          UNIQUEIDENTIFIER NOT NULL,
    CustomerName        NVARCHAR(200)    NOT NULL,
    CustomerEmail       NVARCHAR(255)    NOT NULL,
    OrderDate           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    Status              INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Submitted, 2=Approved, 3=Picked, 4=Shipped, 5=Delivered, 6=Cancelled, 7=Rejected
    TotalAmount         DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    -- Shipping address (owned entity — flat columns)
    ShippingAddress_Street      NVARCHAR(255)    NULL,
    ShippingAddress_City        NVARCHAR(100)    NULL,
    ShippingAddress_State       NVARCHAR(100)    NULL,
    ShippingAddress_PostalCode  NVARCHAR(20)     NULL,
    ShippingAddress_Country     NVARCHAR(50)     NULL,
    Notes               NVARCHAR(1000)   NULL,
    PromotionId         UNIQUEIDENTIFIER NULL,
    DiscountAmount      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    ApprovedAt          DATETIME2        NULL,
    ApprovedBy          NVARCHAR(256)    NULL,
    ShippedAt           DATETIME2        NULL,
    DeliveredAt         DATETIME2        NULL,
    CancelledAt         DATETIME2        NULL,
    CancellationReason  NVARCHAR(500)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0
);
CREATE INDEX IX_SalesOrders_CustomerId  ON SalesOrders (CustomerId);
CREATE INDEX IX_SalesOrders_Status      ON SalesOrders (Status, OrderDate DESC);
CREATE INDEX IX_SalesOrders_TenantId    ON SalesOrders (TenantId, OrderDate DESC);
GO

-- ============================================================
-- Sales Order Line Items (owned collection)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesOrderItems')
CREATE TABLE SalesOrderItems (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OrderId         UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    ProductName     NVARCHAR(500)    NOT NULL,
    ProductSku      NVARCHAR(100)    NULL,
    Quantity        INT              NOT NULL,
    UnitPrice       DECIMAL(18, 2)   NOT NULL,
    DiscountAmount  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    TaxRate         DECIMAL(5, 2)    NOT NULL DEFAULT 0,
    TaxAmount       DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    LineTotal       AS (CAST((Quantity * UnitPrice - DiscountAmount + TaxAmount) AS DECIMAL(18,2))) PERSISTED,
    CONSTRAINT FK_OrderItems_Order FOREIGN KEY (OrderId) REFERENCES SalesOrders(Id) ON DELETE CASCADE
);
CREATE INDEX IX_SalesOrderItems_OrderId   ON SalesOrderItems (OrderId);
CREATE INDEX IX_SalesOrderItems_ProductId ON SalesOrderItems (ProductId);
GO

-- ============================================================
-- Saga States (Order Fulfillment Saga tracking)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SagaStates')
CREATE TABLE SagaStates (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    OrderId         UNIQUEIDENTIFIER NOT NULL UNIQUE,
    SagaType        NVARCHAR(100)    NOT NULL DEFAULT 'OrderFulfillment',
    CurrentStep     NVARCHAR(100)    NOT NULL, -- PaymentPending, InventoryPending, ShippingPending, Completed, Compensating, Failed
    Status          INT              NOT NULL DEFAULT 0, -- 0=Running, 1=Completed, 2=Compensating, 3=Failed
    PaymentId       UNIQUEIDENTIFIER NULL,
    ReservationId   UNIQUEIDENTIFIER NULL,
    ShipmentId      UNIQUEIDENTIFIER NULL,
    ErrorMessage    NVARCHAR(2000)   NULL,
    RetryCount      INT              NOT NULL DEFAULT 0,
    LastUpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt     DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_SagaStates_OrderId  ON SagaStates (OrderId);
CREATE INDEX IX_SagaStates_Status   ON SagaStates (Status, LastUpdatedAt DESC);
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
