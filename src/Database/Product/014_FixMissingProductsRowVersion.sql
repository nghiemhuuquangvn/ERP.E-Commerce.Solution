-- ============================================================
-- ProductDB Schema Fix — ProductService (Port 5008, Code: PRD)
-- Migration 014: Adds Products.RowVersion where missing.
--
-- 007_RebuildProductsAndMissingTables.sql's header comment claims it "adds
-- missing BaseEntity+entity columns (RowVersion, ...)" for Products, but the
-- actual RowVersion column only appears inside that script's
-- `CREATE TABLE Products (...)` (guarded by `IF NOT EXISTS (SELECT 1 FROM
-- sys.tables WHERE name = 'Products')`) — i.e. it only takes effect if the
-- whole Products table doesn't exist yet. On any database where Products was
-- already created by 001_CreateProductDB.sql (which never had RowVersion),
-- that CREATE TABLE no-ops and RowVersion is never added via any ALTER TABLE
-- fallback, unlike 007's own Categories fix (see its STEP 2). Found
-- 2026-08-07 running db-migrate against a long-dormant docker-compose
-- ProductDB whose migration history stopped short of 007's table-rebuild
-- path — surfaced as "Invalid column name 'RowVersion'" on GET /products.
-- ============================================================

USE ProductDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'RowVersion')
    ALTER TABLE Products ADD RowVersion ROWVERSION;
GO
