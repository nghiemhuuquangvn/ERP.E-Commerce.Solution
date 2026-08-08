-- ============================================================
-- NotificationDB Migration 005 — Workflow-automation templates
--
-- New templates for the PurchaseOrder multi-level approval chain and the
-- Leave Request approval flow (see NotificationKafkaConsumer's new
-- purchasing-topic/hrm-topic subscriptions and the corresponding
-- IntegrationEventHandlers.cs handlers).
-- ============================================================

USE NotificationDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO NotificationTemplates (Id, TenantId, Code, Name, Channel, Subject, Body, IsActive, CreatedAt, UpdatedAt)
SELECT NEWID(), '00000000-0000-0000-0000-000000000000', v.Code, v.Name, 0, v.Subject, v.Body, 1, GETUTCDATE(), GETUTCDATE()
FROM (VALUES
    ('PO_PENDING_FINANCE_APPROVAL', 'PO Pending Finance Approval', 'Purchase Order #{poNumber} needs Finance approval',
     'Purchase order #{poNumber} ({totalAmount}) was approved by manager {managerApprovedBy} and now requires Finance approval.'),
    ('PO_APPROVED', 'PO Approved', 'Purchase Order #{poNumber} approved',
     'Purchase order #{poNumber} has been fully approved by {approvedBy}.'),
    ('LEAVE_APPROVED', 'Leave Request Approved', 'Your leave request has been approved',
     'Your leave request from {startDate} to {endDate} has been approved.'),
    ('LEAVE_REJECTED', 'Leave Request Rejected', 'Your leave request has been rejected',
     'Your leave request was rejected. Reason: {reason}')
) AS v(Code, Name, Subject, Body)
WHERE NOT EXISTS (SELECT 1 FROM NotificationTemplates t WHERE t.Code = v.Code);
GO
