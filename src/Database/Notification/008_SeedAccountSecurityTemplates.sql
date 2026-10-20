-- ============================================================
-- NotificationDB Migration 008 — Account-security email templates
--
-- Templates for IdentityService's PasswordResetRequestedDomainEvent and
-- EmailConfirmationRequestedDomainEvent (identity-topic → IdentityEventConsumer →
-- PasswordResetRequested/EmailConfirmationRequestedIntegrationEventHandler).
-- The links carry single-use tokens; IdentityService stores only their hashes.
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
     'We received a request to reset your password. Use this link within {expiryMinutes} minutes: {resetLink} — if you did not request this, you can ignore this email.'),
    ('EMAIL_CONFIRMATION', 'Email Confirmation', 'Confirm your email address',
     'Please confirm your email address using this link within {expiryHours} hours: {confirmLink}')
) AS v(Code, Name, Subject, Body)
WHERE NOT EXISTS (SELECT 1 FROM NotificationTemplates t WHERE t.Code = v.Code);
GO
