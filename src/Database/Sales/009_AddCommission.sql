-- ============================================================
-- SalesDB Migration 009 — CommissionPlan/CommissionRecord. No other service
-- claims Commission; created off PaymentProcessedEvent (already consumed by
-- PaymentKafkaConsumer) since there is no InvoicePaid event in this system.
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CommissionPlans')
CREATE TABLE CommissionPlans (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name        NVARCHAR(200)    NOT NULL,
    RateType    INT              NOT NULL DEFAULT 0, -- 0=Percent
    RateValue   DECIMAL(9, 6)    NOT NULL,
    IsActive    BIT              NOT NULL DEFAULT 1
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CommissionRecords')
CREATE TABLE CommissionRecords (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    PlanId          UNIQUEIDENTIFIER NOT NULL,
    SalesRepId      UNIQUEIDENTIFIER NOT NULL,
    OrderId         UNIQUEIDENTIFIER NOT NULL,
    Amount          DECIMAL(18, 2)   NOT NULL,
    Status          INT              NOT NULL DEFAULT 0, -- 0=Pending, 1=Approved, 2=Paid
    CalculatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CommissionRecords_Plan FOREIGN KEY (PlanId) REFERENCES CommissionPlans(Id)
);
CREATE INDEX IX_CommissionRecords_SalesRepId ON CommissionRecords (SalesRepId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CommissionRecords_OrderId')
    CREATE UNIQUE INDEX UX_CommissionRecords_OrderId ON CommissionRecords(OrderId);
GO
