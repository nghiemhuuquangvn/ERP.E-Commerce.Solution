-- ============================================================
-- NotificationDB Migration 013 - customer notifications (ROADMAP P1-6)
--   * OrderRecipients  who an order's notifications go to (written from the Sales SalesOrderCreated event)
--   * UserContacts     an account's e-mail (written from UserCreated), for loyalty / coupon messages
--   * Notifications.DedupKey  one notification per business fact; unique per tenant when set
--   * Platform-default templates (SYSTEM tenant): the three order templates now use the variables the handlers send, and
--     the new customer messages are added. A tenant overrides a template by creating its own with the same code.
-- Idempotent.
-- ============================================================

USE NotificationDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderRecipients')
CREATE TABLE OrderRecipients (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OrderId      UNIQUEIDENTIFIER NOT NULL,
    CustomerId   UNIQUEIDENTIFIER NOT NULL,
    OrderNo      NVARCHAR(50)     NULL,
    CustomerName NVARCHAR(200)    NULL,
    Email        NVARCHAR(320)    NOT NULL,
    Phone        NVARCHAR(50)     NULL,
    Description  NVARCHAR(MAX)    NULL,
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CreatedBy    NVARCHAR(MAX)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy    NVARCHAR(MAX)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion   ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OrderRecipients_Tenant_Order')
    CREATE UNIQUE INDEX UX_OrderRecipients_Tenant_Order ON OrderRecipients (TenantId, OrderId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserContacts')
CREATE TABLE UserContacts (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    UserId      UNIQUEIDENTIFIER NOT NULL,
    Email       NVARCHAR(320)    NOT NULL,
    FullName    NVARCHAR(200)    NULL,
    Description NVARCHAR(MAX)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CreatedBy   NVARCHAR(MAX)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy   NVARCHAR(MAX)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion  ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_UserContacts_Tenant_User')
    CREATE UNIQUE INDEX UX_UserContacts_Tenant_User ON UserContacts (TenantId, UserId);
GO

IF COL_LENGTH('Notifications', 'DedupKey') IS NULL
    ALTER TABLE Notifications ADD DedupKey NVARCHAR(300) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Notifications_Tenant_DedupKey')
    CREATE UNIQUE INDEX UX_Notifications_Tenant_DedupKey ON Notifications (TenantId, DedupKey) WHERE DedupKey IS NOT NULL;
GO

-- Platform defaults. UPDATE what exists, INSERT what does not (SYSTEM tenant).
DECLARE @system UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @t TABLE (Code NVARCHAR(100), Name NVARCHAR(200), Subject NVARCHAR(500), Body NVARCHAR(MAX), Variables NVARCHAR(500));
INSERT @t (Code, Name, Subject, Body, Variables) VALUES
 ('ORDER_CONFIRMATION', 'Order confirmation', N'Order #{orderNumber} confirmed',
  N'Hi {customerName}, thank you for your order #{orderNumber} placed on {orderDate}. Total: {totalAmount}.', 'customerName,orderNumber,orderDate,totalAmount'),
 ('PAYMENT_RECEIPT', 'Payment receipt', N'Payment received for order #{orderNumber}',
  N'Hi {customerName}, we received your payment of {amount} for order #{orderNumber}. Thank you!', 'customerName,orderNumber,amount,currency'),
 ('SHIPMENT_NOTIFICATION', 'Shipment prepared', N'Your order #{orderNumber} is being prepared for delivery',
  N'Hi {customerName}, order #{orderNumber} is being prepared for delivery by {carrier}. You will get another message when it ships.', 'customerName,orderNumber,carrier,trackingNumber'),
 ('ORDER_SHIPPED', 'Order shipped', N'Your order #{orderNumber} has shipped',
  N'Hi {customerName}, order #{orderNumber} is on its way with {carrier}. Tracking number: {trackingNumber}.', 'customerName,orderNumber,carrier,trackingNumber'),
 ('ORDER_DELIVERED', 'Order delivered', N'Your order #{orderNumber} was delivered',
  N'Hi {customerName}, order #{orderNumber} ({totalAmount}) was delivered. We hope you enjoy it - tell us if anything is wrong.', 'customerName,orderNumber,totalAmount'),
 ('ORDER_CANCELLED', 'Order cancelled', N'Your order #{orderNumber} was cancelled',
  N'Hi {customerName}, order #{orderNumber} was cancelled. Reason: {reason}. A payment you made is refunded to the original method.', 'customerName,orderNumber,reason'),
 ('ORDER_RETURNED', 'Return completed', N'Your return for order #{orderNumber} is complete',
  N'Hi {customerName}, we received the goods of order #{orderNumber} ({reason}). Refund: {refundAmount}.', 'customerName,orderNumber,reason,refundAmount'),
 ('PAYMENT_FAILED', 'Payment failed', N'Payment for order #{orderNumber} failed',
  N'Hi {customerName}, the payment of {amount} for order #{orderNumber} did not go through ({reason}). Please try again or use another method.', 'customerName,orderNumber,amount,reason'),
 ('PAYMENT_REFUNDED', 'Refund issued', N'Refund for order #{orderNumber}',
  N'Hi {customerName}, we refunded {amount} for order #{orderNumber}. It can take a few days to appear on your statement.', 'customerName,orderNumber,amount'),
 ('LOYALTY_POINTS_EARNED', 'Loyalty points earned', N'You earned {points} points',
  N'Hi {customerName}, you earned {points} loyalty points ({reason}). You now have {totalPoints} points to spend.', 'customerName,points,totalPoints,reason'),
 ('COUPON_REDEEMED', 'Coupon used', N'Your coupon {code} was applied',
  N'Hi {customerName}, coupon {code} was applied to your order and saved you {discount}.', 'customerName,code,discount'),
 ('CASE_RESOLVED', 'Support case resolved', N'Your support case was resolved: {title}',
  N'Hi {customerName}, your support case "{title}" has been resolved. If something is still wrong, reply to this message and we will reopen it.', 'customerName,title');

UPDATE nt
SET nt.Name = s.Name, nt.Subject = s.Subject, nt.Body = s.Body, nt.Variables = s.Variables, nt.UpdatedAt = SYSUTCDATETIME()
FROM NotificationTemplates nt JOIN @t s ON s.Code = nt.Code
WHERE nt.TenantId = @system;

INSERT NotificationTemplates (Id, TenantId, Code, Name, Channel, Subject, Body, Variables, IsActive, CreatedAt, UpdatedAt, IsDeleted)
SELECT NEWID(), @system, s.Code, s.Name, 0, s.Subject, s.Body, s.Variables, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM @t s
WHERE NOT EXISTS (SELECT 1 FROM NotificationTemplates nt WHERE nt.TenantId = @system AND nt.Code = s.Code);
GO
