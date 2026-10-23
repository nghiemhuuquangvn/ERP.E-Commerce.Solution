-- ============================================================
-- AuthDB Migration 027 — one-time secrets no longer stay in OutboxMessages (ROADMAP P0-8)
-- Domain events with a 2FA code or a reset / confirmation link were stored in plaintext and kept for ever.
-- The service now encrypts those fields when it writes the row and overwrites them once the message is published.
-- This blanks the ones already published by an older version. Rows not yet published are left alone: the
-- processor redacts them after delivery (NotificationService still accepts the plaintext of those).
-- Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

UPDATE OutboxMessages
SET Content = JSON_MODIFY(Content, '$.Code', '[redacted]')
WHERE ProcessedOnUtc IS NOT NULL
  AND Type = 'TwoFactorCodeGeneratedDomainEvent'
  AND ISJSON(Content) = 1
  AND JSON_VALUE(Content, '$.Code') IS NOT NULL
  AND JSON_VALUE(Content, '$.Code') <> '[redacted]';
GO

UPDATE OutboxMessages
SET Content = JSON_MODIFY(Content, '$.ResetLink', '[redacted]')
WHERE ProcessedOnUtc IS NOT NULL
  AND Type = 'PasswordResetRequestedDomainEvent'
  AND ISJSON(Content) = 1
  AND JSON_VALUE(Content, '$.ResetLink') IS NOT NULL
  AND JSON_VALUE(Content, '$.ResetLink') <> '[redacted]';
GO

UPDATE OutboxMessages
SET Content = JSON_MODIFY(Content, '$.ConfirmLink', '[redacted]')
WHERE ProcessedOnUtc IS NOT NULL
  AND Type = 'EmailConfirmationRequestedDomainEvent'
  AND ISJSON(Content) = 1
  AND JSON_VALUE(Content, '$.ConfirmLink') IS NOT NULL
  AND JSON_VALUE(Content, '$.ConfirmLink') <> '[redacted]';
GO
