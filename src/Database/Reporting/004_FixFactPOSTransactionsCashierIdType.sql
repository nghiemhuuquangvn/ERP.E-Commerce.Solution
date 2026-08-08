-- ============================================================
-- ReportingDB Migration 004 — Fix FactPOSTransactions.CashierId type
--
-- Pre-existing bug (not introduced by the demo-data seeding effort, just
-- never surfaced before because FactPOSTransactions was always empty until
-- 003_SeedFactsFromSeedData.sql populated it): the original 001 script
-- defined CashierId as NVARCHAR(100), but the EF Core entity
-- (ERP.RPT.Domain.Models.FactPOSTransaction.CashierId) has always been typed
-- Guid. EF's SqlDataReader.GetGuid() throws InvalidCastException reading an
-- NVARCHAR column back as a GUID, breaking GET /api/v1/reports/pos with a
-- 500 whenever the table has any rows. Every value ever written to this
-- column by this repo's own seed scripts is a well-formed GUID string, so
-- the ALTER converts cleanly.
-- ============================================================

USE ReportingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('FactPOSTransactions')
      AND c.name = 'CashierId'
      AND t.name <> 'uniqueidentifier'
)
BEGIN
    ALTER TABLE FactPOSTransactions ALTER COLUMN CashierId UNIQUEIDENTIFIER NOT NULL;
END
GO
