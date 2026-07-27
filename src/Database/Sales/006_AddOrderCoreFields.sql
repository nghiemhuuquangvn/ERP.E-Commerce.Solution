-- ============================================================
-- SalesDB Migration 006 — SalesOrder core fields (OrderNo, OrderSource,
-- DepartmentId, SalesRepId, QuotationId) + per-tenant/month OrderNo/QuotationNo
-- counter table. PromotionId/DiscountAmount already exist on SalesOrders since
-- 001_CreateSalesDB.sql — they were never mapped by EF until now, no new
-- columns needed for them.
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'OrderNo')
    ALTER TABLE SalesOrders ADD OrderNo NVARCHAR(50) NOT NULL DEFAULT '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'OrderSource')
    ALTER TABLE SalesOrders ADD OrderSource INT NOT NULL DEFAULT 0; -- 0=Manual, 1=Ecommerce, 2=POS
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'DepartmentId')
    ALTER TABLE SalesOrders ADD DepartmentId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'SalesRepId')
    ALTER TABLE SalesOrders ADD SalesRepId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'QuotationId')
    ALTER TABLE SalesOrders ADD QuotationId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SalesOrders_OrderNo')
    CREATE UNIQUE INDEX UX_SalesOrders_OrderNo ON SalesOrders(OrderNo) WHERE OrderNo <> '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderNumberSequences')
CREATE TABLE OrderNumberSequences (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Prefix      NVARCHAR(20)     NOT NULL,
    Year        INT              NOT NULL,
    Month       INT              NOT NULL,
    LastValue   INT              NOT NULL DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OrderNumberSequences_Tenant_Prefix_Year_Month')
    CREATE UNIQUE INDEX UX_OrderNumberSequences_Tenant_Prefix_Year_Month
        ON OrderNumberSequences(TenantId, Prefix, Year, Month);
GO
