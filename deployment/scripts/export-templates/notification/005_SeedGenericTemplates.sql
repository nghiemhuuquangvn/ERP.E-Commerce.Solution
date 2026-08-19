-- ============================================================
-- NotificationDB Migration 005 — Generic example templates
--
-- Replaces the source monorepo's PO-approval/Leave-request-specific templates
-- (tied to a purchasing/HR workflow this standalone export doesn't include) with
-- a couple of illustrative, domain-agnostic examples. 001_CreateNotificationDB.sql
-- already seeds WELCOME_EMAIL and TWO_FACTOR_CODE — this adds a third pattern
-- (password reset) so the template mechanism has more than two examples to learn
-- from. Replace/extend with your own application's templates.
-- ============================================================

USE NotificationDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO NotificationTemplates (Id, TenantId, Code, Name, Channel, Subject, Body, IsActive, CreatedAt, UpdatedAt)
SELECT NEWID(), '00000000-0000-0000-0000-000000000000', v.Code, v.Name, 0, v.Subject, v.Body, 1, GETUTCDATE(), GETUTCDATE()
FROM (VALUES
    ('PASSWORD_RESET', 'Password Reset', 'Reset your password',
     'Hi {firstName}, click the link below to reset your password. This link expires in {expiryMinutes} minutes: {resetLink}')
) AS v(Code, Name, Subject, Body)
WHERE NOT EXISTS (SELECT 1 FROM NotificationTemplates t WHERE t.Code = v.Code);
GO
