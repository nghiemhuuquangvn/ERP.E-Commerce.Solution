-- ============================================================
-- CrmDB Schema Fix — CRMService (Port 5006, Code: CRM)
-- Migration 002: Adds columns that C# entity models reference but the
-- original CREATE TABLE scripts omitted or named differently.
-- All DDL is guarded with IF NOT EXISTS — safe to run multiple times.
-- ============================================================

USE CrmDB;
GO

-- ============================================================
-- Contacts — entity uses Name (single), ContactSource; DB has FirstName+LastName, Source
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'Name')
    ALTER TABLE Contacts ADD Name NVARCHAR(200) NOT NULL DEFAULT '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'ContactSource')
    ALTER TABLE Contacts ADD ContactSource INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'Description')
    ALTER TABLE Contacts ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contacts') AND name = 'RowVersion')
    ALTER TABLE Contacts ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- Leads — missing BaseEntity columns
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'Description')
    ALTER TABLE Leads ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leads') AND name = 'RowVersion')
    ALTER TABLE Leads ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- Opportunities — entity uses OpportunityName/OpportunityStage/ProbabilityPercent/EstimatedValue;
--                 DB has Name/Stage/Probability/Amount
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Opportunities') AND name = 'OpportunityName')
    ALTER TABLE Opportunities ADD OpportunityName NVARCHAR(200) NOT NULL DEFAULT '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Opportunities') AND name = 'OpportunityStage')
    ALTER TABLE Opportunities ADD OpportunityStage INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Opportunities') AND name = 'ProbabilityPercent')
    ALTER TABLE Opportunities ADD ProbabilityPercent INT NOT NULL DEFAULT 10;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Opportunities') AND name = 'EstimatedValue')
    ALTER TABLE Opportunities ADD EstimatedValue DECIMAL(18, 2) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Opportunities') AND name = 'Description')
    ALTER TABLE Opportunities ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Opportunities') AND name = 'RowVersion')
    ALTER TABLE Opportunities ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- Cases — entity uses CustomerId/CaseSubject/CaseDescription/CasePriority/CaseStatus/ResolutionNote;
--         DB has ContactId/Subject/Description/Priority/Status/Resolution
-- Note: Description column in DB maps to BaseEntity.Description; CaseDescription is separate.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'CustomerId')
    ALTER TABLE Cases ADD CustomerId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'CaseSubject')
    ALTER TABLE Cases ADD CaseSubject NVARCHAR(200) NOT NULL DEFAULT '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'CaseDescription')
    ALTER TABLE Cases ADD CaseDescription NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'CasePriority')
    ALTER TABLE Cases ADD CasePriority INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'CaseStatus')
    ALTER TABLE Cases ADD CaseStatus INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'ResolutionNote')
    ALTER TABLE Cases ADD ResolutionNote NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cases') AND name = 'RowVersion')
    ALTER TABLE Cases ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- Activities — entity uses ActivityType/Notes/Status/ScheduledAt/AssignedToUserId/Source;
--              DB has Type/Description/IsCompleted/OccurredAt — many columns missing
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'ActivityType')
    ALTER TABLE Activities ADD ActivityType INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'Notes')
    ALTER TABLE Activities ADD Notes NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'Status')
    ALTER TABLE Activities ADD Status INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'ScheduledAt')
    ALTER TABLE Activities ADD ScheduledAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'AssignedToUserId')
    ALTER TABLE Activities ADD AssignedToUserId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'Source')
    ALTER TABLE Activities ADD Source NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'UpdatedBy')
    ALTER TABLE Activities ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'UpdatedAt')
    ALTER TABLE Activities ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'IsDeleted')
    ALTER TABLE Activities ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'Description')
    ALTER TABLE Activities ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'RowVersion')
    ALTER TABLE Activities ADD RowVersion ROWVERSION;
GO
