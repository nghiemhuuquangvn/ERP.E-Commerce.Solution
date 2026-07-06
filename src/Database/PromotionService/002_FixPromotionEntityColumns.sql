-- ============================================================
-- PromotionDB Schema Fix (Port 5012, Code: PRM)
-- Migration 002: entity-specific column gaps + create missing tables.
-- Safe to re-run (all DDL is guarded).
-- ============================================================
USE PromotionDB;
GO

-- ============================================================
-- Promotions — missing entity-specific columns
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Promotions') AND name='Scope')
    ALTER TABLE Promotions ADD Scope INT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Promotions') AND name='MaxUsageTotal')
    ALTER TABLE Promotions ADD MaxUsageTotal INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Promotions') AND name='MaxUsagePerUser')
    ALTER TABLE Promotions ADD MaxUsagePerUser INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Promotions') AND name='BuyQuantity')
    ALTER TABLE Promotions ADD BuyQuantity INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Promotions') AND name='GetQuantity')
    ALTER TABLE Promotions ADD GetQuantity INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Promotions') AND name='ApplicableProductIds')
    ALTER TABLE Promotions ADD ApplicableProductIds NVARCHAR(MAX) NOT NULL DEFAULT '[]';
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Promotions') AND name='ApplicableCategoryIds')
    ALTER TABLE Promotions ADD ApplicableCategoryIds NVARCHAR(MAX) NOT NULL DEFAULT '[]';
GO

-- ============================================================
-- LoyaltyAccounts — missing entity-specific columns
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('LoyaltyAccounts') AND name='AvailablePoints')
    ALTER TABLE LoyaltyAccounts ADD AvailablePoints INT NOT NULL DEFAULT 0;
GO

-- ============================================================
-- LoyaltyTransactions — column name mismatches + missing columns
-- Entity uses: UserId, TransactionType; DB has: Type (no UserId)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('LoyaltyTransactions') AND name='UserId')
    ALTER TABLE LoyaltyTransactions ADD UserId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('LoyaltyTransactions') AND name='TransactionType')
    ALTER TABLE LoyaltyTransactions ADD TransactionType NVARCHAR(20) NOT NULL DEFAULT 'Earn';
GO

-- ============================================================
-- FlashSales — table missing entirely
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='FlashSales')
CREATE TABLE FlashSales (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(300)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    StartTime   DATETIME2        NOT NULL,
    EndTime     DATETIME2        NOT NULL,
    Status      INT              NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy   NVARCHAR(256)    NULL,
    UpdatedBy   NVARCHAR(256)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION
);
GO

-- ============================================================
-- FlashSaleItems — table missing entirely
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='FlashSaleItems')
CREATE TABLE FlashSaleItems (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    FlashSaleId       UNIQUEIDENTIFIER NOT NULL,
    ProductId         UNIQUEIDENTIFIER NOT NULL,
    ProductName       NVARCHAR(300)    NOT NULL,
    OriginalPrice     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    SalePrice         DECIMAL(18,2)    NOT NULL DEFAULT 0,
    AllocatedQuantity INT              NOT NULL DEFAULT 0,
    SoldQuantity      INT              NOT NULL DEFAULT 0,
    CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy         NVARCHAR(256)    NULL,
    UpdatedBy         NVARCHAR(256)    NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0,
    Description       NVARCHAR(MAX)    NULL,
    RowVersion        ROWVERSION,
    CONSTRAINT FK_FlashSaleItems_FlashSales FOREIGN KEY (FlashSaleId)
        REFERENCES FlashSales(Id) ON DELETE CASCADE
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FlashSaleItems_FlashSaleId_ProductId' AND object_id=OBJECT_ID('FlashSaleItems'))
    CREATE UNIQUE INDEX IX_FlashSaleItems_FlashSaleId_ProductId ON FlashSaleItems (FlashSaleId, ProductId);
GO

-- ============================================================
-- PromotionProductCache — table missing entirely
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='PromotionProductCache')
CREATE TABLE PromotionProductCache (
    ProductId   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Category    NVARCHAR(200)    NOT NULL,
    Price       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    SyncedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PromotionProductCache_Category' AND object_id=OBJECT_ID('PromotionProductCache'))
    CREATE INDEX IX_PromotionProductCache_Category ON PromotionProductCache (Category);
GO
