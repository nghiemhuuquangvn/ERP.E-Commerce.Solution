-- ============================================================
-- PosDB Migration 003 — PosProductCache / PosPromotionCache
-- Local read cache POSDbContext has queried since the service was scaffolded
-- (ProductLookupQueryHandler, POSIntegrationEventHandlers syncing from
-- ProductCreated/ProductUpdated/PromotionCreated events per the event catalog),
-- but no migration ever created either table — every product lookup on any POS
-- terminal 500'd with "Invalid object name 'PosProductCache'".
-- ============================================================

USE PosDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PosProductCache')
CREATE TABLE PosProductCache (
    ProductId   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name        NVARCHAR(300)    NOT NULL,
    Sku         NVARCHAR(100)    NOT NULL,
    Price       DECIMAL(18, 2)   NOT NULL,
    IsActive    BIT              NOT NULL DEFAULT 1,
    SyncedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_PosProductCache_Sku ON PosProductCache (Sku);
CREATE INDEX IX_PosProductCache_IsActive ON PosProductCache (IsActive);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PosPromotionCache')
CREATE TABLE PosPromotionCache (
    PromotionId     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name            NVARCHAR(300)    NOT NULL,
    DiscountType    INT              NOT NULL,
    DiscountValue   DECIMAL(18, 4)   NOT NULL,
    StartDate       DATETIME2        NOT NULL,
    EndDate         DATETIME2        NOT NULL
);
CREATE INDEX IX_PosPromotionCache_DateRange ON PosPromotionCache (StartDate, EndDate);
GO
