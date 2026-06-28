-- ============================================================
-- PaymentDB Schema
-- Service: PaymentService (Port 5009, Code: PAY)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PaymentDB')
    CREATE DATABASE PaymentDB;
GO

USE PaymentDB;
GO

CREATE TABLE Payments (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    OrderId         UNIQUEIDENTIFIER NOT NULL,
    Amount          DECIMAL(18,2)    NOT NULL,
    Currency        NVARCHAR(3)      NOT NULL DEFAULT 'USD',
    Method          INT              NOT NULL, -- 0=CreditCard, 1=DebitCard, 2=BankTransfer, 3=EWallet, 4=Cash, 5=Crypto
    Status          INT              NOT NULL, -- 0=Pending, 1=Authorized, 2=Captured, 3=Failed, 4=Cancelled, 5=Refunded
    GatewayProvider NVARCHAR(100)    NULL,
    GatewayRef      NVARCHAR(500)    NULL,
    FailureReason   NVARCHAR(500)    NULL,
    AuthorizedAt    DATETIME2        NULL,
    CapturedAt      DATETIME2        NULL,
    CancelledAt     DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_Payments_OrderId  ON Payments (OrderId);
CREATE INDEX IX_Payments_TenantId ON Payments (TenantId);
CREATE INDEX IX_Payments_Status   ON Payments (Status);
GO

CREATE TABLE PaymentRefunds (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    PaymentId       UNIQUEIDENTIFIER NOT NULL,
    Amount          DECIMAL(18,2)    NOT NULL,
    Reason          NVARCHAR(500)    NULL,
    Status          INT              NOT NULL, -- 0=Pending, 1=Completed, 2=Failed
    GatewayRefundId NVARCHAR(500)    NULL,
    FailureReason   NVARCHAR(500)    NULL,
    CompletedAt     DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (PaymentId) REFERENCES Payments(Id)
);

CREATE INDEX IX_PaymentRefunds_PaymentId ON PaymentRefunds (PaymentId);
GO

CREATE TABLE Settlements (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    PeriodStart     DATE             NOT NULL,
    PeriodEnd       DATE             NOT NULL,
    TotalAmount     DECIMAL(18,2)    NOT NULL,
    Status          INT              NOT NULL, -- 0=Pending, 1=Processing, 2=Completed
    Notes           NVARCHAR(1000)   NULL,
    ProcessedAt     DATETIME2        NULL,
    CompletedAt     DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
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
