-- ============================================================
-- SalesDB Schema Update — SaleOrderService (Port 5002, Code: SO)
-- Migration 002: order-update/notes/partial-fulfillment/return-flow columns +
-- two new tables (OrderNotes, OrderStatusHistory) added 2026-07-03 while
-- processing section 3.3's ❌ list (UpdateOrderCommand, order notes, partial
-- fulfillment, order timeline/audit trail, Return/Refund flow, SalesOrderDelivered
-- event). New OrderStatus enum values: 8=PartiallyShipped, 9=ReturnRequested,
-- 10=Returned (see OrderStatus.cs).
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'TrackingNumber')
    ALTER TABLE SalesOrders ADD TrackingNumber NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'Carrier')
    ALTER TABLE SalesOrders ADD Carrier NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'HasPriceChangeWarning')
    ALTER TABLE SalesOrders ADD HasPriceChangeWarning BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'ReturnRequestedAt')
    ALTER TABLE SalesOrders ADD ReturnRequestedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'ReturnedAt')
    ALTER TABLE SalesOrders ADD ReturnedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'ReturnReason')
    ALTER TABLE SalesOrders ADD ReturnReason NVARCHAR(1000) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'RefundAmount')
    ALTER TABLE SalesOrders ADD RefundAmount DECIMAL(18, 2) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrderItems') AND name = 'ShippedQuantity')
    ALTER TABLE SalesOrderItems ADD ShippedQuantity INT NOT NULL DEFAULT 0;
GO

-- ============================================================
-- Order internal notes (append-only, multiple per order)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderNotes')
CREATE TABLE OrderNotes (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OrderId     UNIQUEIDENTIFIER NOT NULL,
    Note        NVARCHAR(2000)   NOT NULL,
    AddedBy     NVARCHAR(200)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_OrderNotes_Order FOREIGN KEY (OrderId) REFERENCES SalesOrders(Id) ON DELETE CASCADE
);
CREATE INDEX IX_OrderNotes_OrderId ON OrderNotes (OrderId, CreatedAt DESC);
GO

-- ============================================================
-- Order status history (audit trail — one row per transition)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderStatusHistory')
CREATE TABLE OrderStatusHistory (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OrderId     UNIQUEIDENTIFIER NOT NULL,
    FromStatus  INT              NOT NULL,
    ToStatus    INT              NOT NULL,
    Reason      NVARCHAR(1000)   NULL,
    ChangedBy   NVARCHAR(200)    NULL,
    ChangedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_OrderStatusHistory_Order FOREIGN KEY (OrderId) REFERENCES SalesOrders(Id) ON DELETE CASCADE
);
CREATE INDEX IX_OrderStatusHistory_OrderId ON OrderStatusHistory (OrderId, ChangedAt DESC);
GO
