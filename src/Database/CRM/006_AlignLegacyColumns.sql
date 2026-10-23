-- ============================================================
-- CrmDB Migration 006 — legacy columns the entities no longer write (ROADMAP P0-9)
-- 002_FixCrmEntityColumns.sql added the columns the entities use (Name, ContactSource, CaseSubject,
-- OpportunityName, ActivityType …) but left the original NOT NULL ones in place, so every INSERT failed.
-- The old columns are kept (data preserved) and allowed to be NULL. AssignedToUserId was nvarchar while the
-- entities use a Guid (reads threw InvalidCast); every stored value is a GUID, so it is converted.
-- Idempotent.
-- ============================================================

USE CrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'FirstName' AND is_nullable = 0)
    ALTER TABLE Contacts ALTER COLUMN FirstName NVARCHAR(100) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'LastName' AND is_nullable = 0)
    ALTER TABLE Contacts ALTER COLUMN LastName NVARCHAR(100) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'Subject' AND is_nullable = 0)
    ALTER TABLE Cases ALTER COLUMN Subject NVARCHAR(500) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Opportunities') AND name = 'Name' AND is_nullable = 0)
    ALTER TABLE Opportunities ALTER COLUMN Name NVARCHAR(300) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'Type' AND is_nullable = 0)
    ALTER TABLE Activities ALTER COLUMN [Type] INT NULL;
GO

-- nvarchar -> uniqueidentifier: refuse to lose a value that is not a GUID.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'AssignedToUserId' AND system_type_id = TYPE_ID('nvarchar'))
BEGIN
    IF EXISTS (SELECT 1 FROM Cases WHERE AssignedToUserId IS NOT NULL AND TRY_CONVERT(UNIQUEIDENTIFIER, AssignedToUserId) IS NULL)
        THROW 50026, 'Cases.AssignedToUserId holds a value that is not a GUID; fix the data before running CRM 006.', 1;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cases_Assigned' AND object_id = OBJECT_ID('Cases')) DROP INDEX IX_Cases_Assigned ON Cases;
    ALTER TABLE Cases ALTER COLUMN AssignedToUserId UNIQUEIDENTIFIER NULL;
    CREATE INDEX IX_Cases_Assigned ON Cases (AssignedToUserId, Status) WHERE IsDeleted = 0;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Opportunities') AND name = 'AssignedToUserId' AND system_type_id = TYPE_ID('nvarchar'))
BEGIN
    IF EXISTS (SELECT 1 FROM Opportunities WHERE AssignedToUserId IS NOT NULL AND TRY_CONVERT(UNIQUEIDENTIFIER, AssignedToUserId) IS NULL)
        THROW 50026, 'Opportunities.AssignedToUserId holds a value that is not a GUID; fix the data before running CRM 006.', 1;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Opps_Assigned' AND object_id = OBJECT_ID('Opportunities')) DROP INDEX IX_Opps_Assigned ON Opportunities;
    ALTER TABLE Opportunities ALTER COLUMN AssignedToUserId UNIQUEIDENTIFIER NULL;
    CREATE INDEX IX_Opps_Assigned ON Opportunities (AssignedToUserId, Stage) WHERE IsDeleted = 0;
END
GO
