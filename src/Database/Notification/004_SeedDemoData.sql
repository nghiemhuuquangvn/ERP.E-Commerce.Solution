-- ============================================================
-- NotificationDB Seed Demo Data — NotificationService (Port 5013, Code: NTF)
-- Migration 004.
--
-- NOTE: the schema has no table literally named "NotificationHistory".
-- The sent-notification log table is `Notifications` (created in
-- 001_CreateNotificationDB.sql) — that is what this script seeds.
-- `NotificationLogs` is a separate per-event audit trail keyed off
-- Notifications.Id (EventType/Details/EventTimestamp); it is not seeded
-- here since the task only calls for the notification log rows
-- themselves, not their event trails.
--
-- Seeds ~1000 Notifications rows:
--   - RecipientId = Identity Customer User formula
--     (20000000-0002-0000-0000-{N}, N in [1,1000]) picked pseudo-randomly
--     per row, per src/Database/SEED_DATA_CONVENTIONS.md.
--   - TemplateId references one of the 6 existing NotificationTemplates
--     rows (looked up by row order, not hardcoded, since their Ids are
--     NEWID()-generated in 001_CreateNotificationDB.sql).
--   - Channel varied across Email(0)/Sms(1)/Push(2).
--   - Status varied across the full Queued..Cancelled range, weighted
--     toward Sent/Delivered like a real send log.
--   - CreatedAt spread pseudo-randomly over the last ~12 months.
--
-- Idempotent: guarded by a row-count check, safe to re-run.
-- Generated set-based via a numbers CTE — no hand-written literal INSERTs.
-- ============================================================

USE NotificationDB;
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF (SELECT COUNT(*) FROM Notifications) < 1000
BEGIN
    ;WITH Numbers AS (
        SELECT 1 AS N
        UNION ALL
        SELECT N + 1 FROM Numbers WHERE N < 1000
    ),
    Templates AS (
        SELECT Id, Subject, Body, ROW_NUMBER() OVER (ORDER BY Code) AS rn
        FROM NotificationTemplates
    ),
    TemplateCount AS (
        SELECT COUNT(*) AS Cnt FROM NotificationTemplates
    ),
    Picked AS (
        SELECT
            n.N,
            1 + (ABS(CHECKSUM(NEWID())) % 1000) AS RecipientN,
            n.N % 3 AS ChannelVal,
            n.N % 4 AS PriorityVal,
            CASE
                WHEN n.N % 20 = 0 THEN 0                    -- Queued
                WHEN n.N % 20 = 1 THEN 1                    -- Sending
                WHEN n.N % 20 BETWEEN 2 AND 6  THEN 2        -- Sent
                WHEN n.N % 20 BETWEEN 7 AND 15 THEN 3        -- Delivered
                WHEN n.N % 20 IN (16, 17) THEN 4             -- Failed
                WHEN n.N % 20 = 18 THEN 5                    -- Bounced
                ELSE 6                                       -- Cancelled
            END AS StatusVal,
            ((n.N - 1) % (SELECT Cnt FROM TemplateCount)) + 1 AS TemplateRn,
            DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 365), SYSUTCDATETIME()) AS CreatedAtVal
        FROM Numbers n
    )
    INSERT INTO Notifications
        (Id, TenantId, RecipientId, RecipientEmail, RecipientPhone, Channel, Priority, Subject, Body,
         TemplateId, Status, RetryCount, MaxRetries, LastError, SentAt, DeliveredAt, FailedAt, ScheduledFor,
         CreatedAt, UpdatedAt)
    SELECT
        NEWID(),
        @TenantId,
        CONVERT(NVARCHAR(100), CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), p.RecipientN), 12) AS UNIQUEIDENTIFIER)),
        'customer' + CONVERT(VARCHAR(6), p.RecipientN) + '@example.com',
        CASE WHEN p.ChannelVal = 1 THEN '+8490' + RIGHT('0000000' + CONVERT(VARCHAR(7), p.RecipientN), 7) ELSE NULL END,
        p.ChannelVal,
        p.PriorityVal,
        t.Subject,
        t.Body,
        t.Id,
        p.StatusVal,
        CASE WHEN p.StatusVal = 4 THEN 1 + (p.N % 3) ELSE 0 END,
        3,
        CASE WHEN p.StatusVal IN (4, 5) THEN 'SMTP timeout / carrier rejected the message' ELSE NULL END,
        CASE WHEN p.StatusVal IN (2, 3, 4, 5) THEN DATEADD(MINUTE, 1, p.CreatedAtVal) ELSE NULL END,
        CASE WHEN p.StatusVal = 3 THEN DATEADD(MINUTE, 5, p.CreatedAtVal) ELSE NULL END,
        CASE WHEN p.StatusVal IN (4, 5) THEN DATEADD(MINUTE, 3, p.CreatedAtVal) ELSE NULL END,
        NULL,
        p.CreatedAtVal,
        CASE WHEN p.StatusVal = 3 THEN DATEADD(MINUTE, 5, p.CreatedAtVal) ELSE p.CreatedAtVal END
    FROM Picked p
    JOIN Templates t ON t.rn = p.TemplateRn
    OPTION (MAXRECURSION 0);
END
GO
