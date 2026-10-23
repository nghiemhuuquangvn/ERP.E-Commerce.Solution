-- ============================================================
-- NotificationDB Migration 012 — default templates stranded on the empty tenant (ROADMAP P0-7)
-- 009 moves every row whose TenantId is the empty GUID to the SYSTEM tenant in one UPDATE. On a database whose
-- templates had also been seeded under SYSTEM (004/005/008), that UPDATE hit the unique (Code, TenantId) index and
-- failed as a whole, leaving the original defaults behind. Templates are tenant-filtered, so nobody could see them:
-- "Template 'TWO_FACTOR_CODE' not found" - the 2FA code was never sent.
--   * a stranded row whose code already exists under SYSTEM is a duplicate default -> removed;
--   * the others move to SYSTEM.
-- Idempotent.
-- ============================================================

USE NotificationDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DELETE t
FROM NotificationTemplates t
WHERE t.TenantId = '00000000-0000-0000-0000-000000000000'
  AND EXISTS (SELECT 1 FROM NotificationTemplates s
              WHERE s.TenantId = '00000000-0000-0000-0000-000000000001' AND s.Code = t.Code);
GO

UPDATE NotificationTemplates
SET TenantId = '00000000-0000-0000-0000-000000000001'
WHERE TenantId = '00000000-0000-0000-0000-000000000000';
GO
