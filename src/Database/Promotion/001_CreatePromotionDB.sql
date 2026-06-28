-- ============================================================
-- PromotionDB Schema
-- Service: PromotionService (Port 5012, Code: PRM)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PromotionDB')
    CREATE DATABASE PromotionDB;
GO

USE PromotionDB;
GO

CREATE TABLE Promotions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    Description     NVARCHAR(1000)   NULL,
    Type            INT              NOT NULL, -- 0=OrderDiscount, 1=ProductDiscount, 2=Coupon, 3=FlashSale, 4=Loyalty
    DiscountType    INT              NOT NULL, -- 0=Percentage, 1=Fixed
    DiscountValue   DECIMAL(18,2)    NOT NULL,
    MinOrderAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
    MaxDiscountAmount DECIMAL(18,2)  NULL,
    MaxUsage        INT              NULL,
    CurrentUsage    INT              NOT NULL DEFAULT 0,
    StartDate       DATETIME2        NOT NULL,
    EndDate         DATETIME2        NOT NULL,
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=Active, 2=Paused, 3=Expired
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_Promotions_TenantId ON Promotions (TenantId);
CREATE INDEX IX_Promotions_Status   ON Promotions (Status, StartDate, EndDate);
GO

CREATE TABLE Coupons (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    PromotionId UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(50)     NOT NULL,
    IsUsed      BIT              NOT NULL DEFAULT 0,
    UsedByUserId NVARCHAR(100)   NULL,
    UsedAt      DATETIME2        NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (PromotionId) REFERENCES Promotions(Id)
);

CREATE UNIQUE INDEX IX_Coupons_Code     ON Coupons (Code, TenantId);
CREATE INDEX IX_Coupons_PromotionId     ON Coupons (PromotionId);
CREATE INDEX IX_Coupons_TenantId        ON Coupons (TenantId);
GO

CREATE TABLE LoyaltyAccounts (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    UserId      NVARCHAR(100)    NOT NULL,
    TotalPoints INT              NOT NULL DEFAULT 0,
    Tier        INT              NOT NULL DEFAULT 0, -- 0=Bronze, 1=Silver, 2=Gold, 3=Platinum
    ExpiresAt   DATETIME2        NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE UNIQUE INDEX IX_LoyaltyAccounts_UserId ON LoyaltyAccounts (UserId, TenantId);
GO

CREATE TABLE LoyaltyTransactions (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    AccountId   UNIQUEIDENTIFIER NOT NULL,
    Points      INT              NOT NULL,
    Type        INT              NOT NULL, -- 0=Earn, 1=Redeem, 2=Expire, 3=Adjustment
    Reason      NVARCHAR(500)    NULL,
    ReferenceId NVARCHAR(100)    NULL,
    OccurredAt  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (AccountId) REFERENCES LoyaltyAccounts(Id)
);

CREATE INDEX IX_LoyaltyTransactions_AccountId ON LoyaltyTransactions (AccountId);
GO

CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
GO
