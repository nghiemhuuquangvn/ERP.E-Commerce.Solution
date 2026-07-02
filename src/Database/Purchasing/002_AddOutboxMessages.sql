-- ============================================================
-- PurchasingDB Schema Update — PurchaseOrderService (Port 5003, Code: PO)
-- Migration 002: OutboxMessages table for the Outbox pattern — PurchaseOrderService
-- previously raised PurchaseOrderCreatedDomainEvent/PurchaseOrderApprovedDomainEvent
-- into memory with no publishing mechanism at all. This table + the new
-- KafkaMessageBus/OutboxProcessor (Infrastructure) give it real outbound publishing
-- to Kafka topic "purchasing-topic", matching every other Outbox-pattern service.
-- ============================================================

USE PurchasingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutboxMessages')
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboxMessages_Unprocessed')
CREATE INDEX IX_OutboxMessages_Unprocessed ON OutboxMessages (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL;
GO
