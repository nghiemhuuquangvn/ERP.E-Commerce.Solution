-- ============================================================
-- AuthDB Migration 040 - token revocation outbox (security review 2026-11, wave S4a: SEC-M8).
-- docs/business/Identity/TokenRevocation.md
-- A logout / lock / password / role / tenant change writes its revocation here in the SAME commit; it is published to
-- the shared revocation store (Redis) right after the commit and, when Redis is down, again by the
-- RevocationOutboxPublisher until it succeeds. Before this, a failed publish was only logged and the revocation was lost.
-- Idempotent, append-only.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID('RevocationOutbox', 'U') IS NULL
BEGIN
    CREATE TABLE RevocationOutbox (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RevocationOutbox PRIMARY KEY,
        Kind           TINYINT          NOT NULL,   -- 1 Token (jti) / 2 Users / 3 Tenant
        Subject        NVARCHAR(200)    NOT NULL,   -- jti / user id / tenant id
        ExpiresAt      DATETIME2        NULL,       -- token only
        ChangedAtUnix  BIGINT           NOT NULL,   -- when the change happened (the marker carries it)
        CreatedAt      DATETIME2        NOT NULL,
        PublishedAt    DATETIME2        NULL,
        Attempts       INT              NOT NULL CONSTRAINT DF_RevocationOutbox_Attempts DEFAULT 0,
        LastError      NVARCHAR(1000)   NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('RevocationOutbox') AND name = 'IX_RevocationOutbox_CreatedAt')
    CREATE INDEX IX_RevocationOutbox_CreatedAt ON RevocationOutbox (CreatedAt) WHERE PublishedAt IS NULL;
GO

PRINT 'Migration 040 completed: token revocation outbox';
GO
