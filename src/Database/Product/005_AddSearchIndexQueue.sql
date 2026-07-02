-- ============================================================
-- ProductDB Schema Update — ProductService (Port 5008, Code: PRD)
-- Migration 005: SearchIndexQueue — internal processing queue that mirrors
-- the OutboxMessages shape (no audit columns, not a business entity). Rows
-- are enqueued whenever a domain event that affects the Elasticsearch
-- "products" search index fires (ProductSearchIndexRequestedDomainEvent),
-- deduplicated by ProductId, and consumed by SearchIndexProcessor which
-- rebuilds the derived, read-optimized ES document for that product.
-- SQL Server remains the system-of-record; Elasticsearch is a rebuildable,
-- denormalized read index on top of it.
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ProductDB')
    CREATE DATABASE ProductDB;
GO

USE ProductDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SearchIndexQueue')
CREATE TABLE SearchIndexQueue (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    RequestedAt DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IndexedAt   DATETIME2        NULL,
    Error       NVARCHAR(MAX)    NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SearchIndexQueue_Pending')
CREATE INDEX IX_SearchIndexQueue_Pending ON SearchIndexQueue (IndexedAt) WHERE IndexedAt IS NULL;
GO
