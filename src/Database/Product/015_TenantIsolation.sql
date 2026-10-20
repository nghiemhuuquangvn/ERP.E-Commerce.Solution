-- ============================================================
-- ProductDB Migration 015 — Tenant isolation (2026-10-20, service authz phase 2)
--
-- ProductDbContext now applies a per-tenant query filter and stamps/validates TenantId on every
-- save (CLAUDE.md "Multi-Tenant Isolation"). Rows still at the empty GUID would become
-- invisible to every tenant, so — decision 2026-10-20, same as CRM 005 — they are
-- assigned to the SYSTEM tenant. System*Settings keep TenantId = empty: they are
-- platform-wide (IPlatformWideEntity).
-- Idempotent. Run BEFORE deploying the new ProductService.
-- ============================================================

USE ProductDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Codes/SKUs/slugs are chosen by each tenant, so uniqueness is per tenant (two shops may
-- both sell "TSHIRT-001"). Old global indexes are dropped under every name they had.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Sku' AND object_id = OBJECT_ID('Products'))
    DROP INDEX IX_Products_Sku ON Products;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Products_Tenant_Sku')
    CREATE UNIQUE INDEX UX_Products_Tenant_Sku ON Products (TenantId, Sku) WHERE IsDeleted = 0;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductVariants_Sku' AND object_id = OBJECT_ID('ProductVariants'))
    DROP INDEX IX_ProductVariants_Sku ON ProductVariants;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ProductVariants_Tenant_Sku')
    CREATE UNIQUE INDEX UX_ProductVariants_Tenant_Sku ON ProductVariants (TenantId, Sku) WHERE IsDeleted = 0;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Slug' AND object_id = OBJECT_ID('Products'))
    DROP INDEX IX_Products_Slug ON Products;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Products_Tenant_Slug')
    CREATE UNIQUE INDEX UX_Products_Tenant_Slug ON Products (TenantId, Slug) WHERE Slug IS NOT NULL;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Brands_Code' AND object_id = OBJECT_ID('Brands'))
    DROP INDEX UX_Brands_Code ON Brands;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Brands_Tenant_Code')
    CREATE UNIQUE INDEX UX_Brands_Tenant_Code ON Brands (TenantId, Code) WHERE IsDeleted = 0;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Categories_Code' AND object_id = OBJECT_ID('Categories'))
    DROP INDEX UX_Categories_Code ON Categories;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Categories_Tenant_Code')
    CREATE UNIQUE INDEX UX_Categories_Tenant_Code ON Categories (TenantId, Code) WHERE Code IS NOT NULL;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExternalMappings_System_ExternalId' AND object_id = OBJECT_ID('ProductExternalMappings'))
    DROP INDEX IX_ExternalMappings_System_ExternalId ON ProductExternalMappings;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ExternalMappings_Tenant_System_ExternalId')
    CREATE UNIQUE INDEX UX_ExternalMappings_Tenant_System_ExternalId
        ON ProductExternalMappings (TenantId, ExternalSystem, ExternalId) WHERE IsDeleted = 0;
GO

-- Backfill: every tenant column still at the empty GUID -> SYSTEM.
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
             + N' SET TenantId = ''00000000-0000-0000-0000-000000000001'''
             + N' WHERE TenantId = ''00000000-0000-0000-0000-000000000000'';' + CHAR(10)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'TenantId'
WHERE t.name NOT LIKE 'System%Settings';
EXEC sp_executesql @sql;
GO

-- Elasticsearch documents now carry TenantId and every search filters on it: documents
-- indexed before this release have none and would never match. Queue every product for
-- re-indexing (SearchIndexProcessor indexes each one inside its own tenant).
INSERT INTO SearchIndexQueue (Id, ProductId, RequestedAt)
SELECT NEWID(), p.Id, GETUTCDATE()
FROM Products p
WHERE p.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM SearchIndexQueue q WHERE q.ProductId = p.Id AND q.IndexedAt IS NULL);
GO
