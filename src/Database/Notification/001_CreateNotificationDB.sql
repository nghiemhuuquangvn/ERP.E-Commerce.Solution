-- ============================================================
-- NotificationDB Schema
-- Service: NotificationService (Port 5013, Code: NTF)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'NotificationDB')
    CREATE DATABASE NotificationDB;
GO

USE NotificationDB;
GO

CREATE TABLE NotificationTemplates (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(100)    NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Channel     INT              NOT NULL, -- 0=Email, 1=Sms, 2=Push, 3=InApp
    Subject     NVARCHAR(500)    NULL,
    Body        NVARCHAR(MAX)    NOT NULL,
    IsActive    BIT              NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE UNIQUE INDEX IX_Templates_Code ON NotificationTemplates (Code, TenantId);
GO

CREATE TABLE Notifications (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    RecipientId     NVARCHAR(100)    NOT NULL,
    RecipientEmail  NVARCHAR(300)    NULL,
    RecipientPhone  NVARCHAR(50)     NULL,
    Channel         INT              NOT NULL,
    Priority        INT              NOT NULL DEFAULT 1, -- 0=Low, 1=Normal, 2=High, 3=Critical
    Subject         NVARCHAR(500)    NULL,
    Body            NVARCHAR(MAX)    NOT NULL,
    TemplateId      UNIQUEIDENTIFIER NULL,
    Status          INT              NOT NULL DEFAULT 0, -- 0=Queued, 1=Sending, 2=Sent, 3=Delivered, 4=Failed, 5=Bounced, 6=Cancelled
    RetryCount      INT              NOT NULL DEFAULT 0,
    MaxRetries      INT              NOT NULL DEFAULT 3,
    LastError       NVARCHAR(1000)   NULL,
    SentAt          DATETIME2        NULL,
    DeliveredAt     DATETIME2        NULL,
    FailedAt        DATETIME2        NULL,
    ScheduledFor    DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (TemplateId) REFERENCES NotificationTemplates(Id)
);

CREATE INDEX IX_Notifications_RecipientId ON Notifications (RecipientId);
CREATE INDEX IX_Notifications_Status      ON Notifications (Status, CreatedAt);
CREATE INDEX IX_Notifications_TenantId    ON Notifications (TenantId);
GO

CREATE TABLE NotificationLogs (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    NotificationId  UNIQUEIDENTIFIER NOT NULL,
    EventType       NVARCHAR(100)    NOT NULL,
    Details         NVARCHAR(1000)   NULL,
    EventTimestamp  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (NotificationId) REFERENCES Notifications(Id) ON DELETE CASCADE
);

CREATE INDEX IX_NotificationLogs_NotificationId ON NotificationLogs (NotificationId);
GO

CREATE TABLE NotificationPreferences (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    UserId      NVARCHAR(100)    NOT NULL,
    EmailEnabled BIT             NOT NULL DEFAULT 1,
    SmsEnabled  BIT              NOT NULL DEFAULT 1,
    PushEnabled BIT              NOT NULL DEFAULT 1,
    InAppEnabled BIT             NOT NULL DEFAULT 1,
    OptedOutAt  DATETIME2        NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE UNIQUE INDEX IX_Preferences_UserId ON NotificationPreferences (UserId, TenantId);
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

-- Seed default templates
INSERT INTO NotificationTemplates (Id, TenantId, Code, Name, Channel, Subject, Body, IsActive, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), '00000000-0000-0000-0000-000000000000', 'WELCOME_EMAIL',    'Welcome Email',         0, 'Welcome to {AppName}!',      'Hi {FirstName}, welcome to {AppName}. Your account is ready.', 1, GETUTCDATE(), GETUTCDATE()),
    (NEWID(), '00000000-0000-0000-0000-000000000000', 'ORDER_CONFIRMATION','Order Confirmation',    0, 'Order #{OrderNumber} Confirmed', 'Hi {CustomerName}, your order #{OrderNumber} has been confirmed. Total: {TotalAmount}', 1, GETUTCDATE(), GETUTCDATE()),
    (NEWID(), '00000000-0000-0000-0000-000000000000', 'PAYMENT_RECEIPT',  'Payment Receipt',        0, 'Payment Received',           'Payment of {Amount} for order #{OrderNumber} received.', 1, GETUTCDATE(), GETUTCDATE()),
    (NEWID(), '00000000-0000-0000-0000-000000000000', 'SHIPMENT_NOTIFICATION','Shipment Notification', 0, 'Your order has shipped!', 'Your order #{OrderNumber} has shipped. Tracking: {TrackingNumber}', 1, GETUTCDATE(), GETUTCDATE()),
    (NEWID(), '00000000-0000-0000-0000-000000000000', 'EMPLOYEE_WELCOME', 'Employee Welcome Email', 0, 'Welcome to the team!',      'Hi {FirstName}, welcome aboard! Your employee profile is ready.', 1, GETUTCDATE(), GETUTCDATE());
GO
