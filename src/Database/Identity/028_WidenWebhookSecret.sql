-- ============================================================
-- AuthDB Migration 028 — room for encrypted webhook secrets (ROADMAP P0-8)
-- WebhookEndpoints.Secret now holds enc:v1:<base64 AES ciphertext> (about 1.5x the plaintext plus a fixed prefix);
-- NVARCHAR(200) is too short for it. Existing plaintext secrets are encrypted by the service at its next start.
-- Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('WebhookEndpoints', 'Secret') IS NOT NULL AND COL_LENGTH('WebhookEndpoints', 'Secret') < 2000
    ALTER TABLE WebhookEndpoints ALTER COLUMN Secret NVARCHAR(1000) NULL;
GO
