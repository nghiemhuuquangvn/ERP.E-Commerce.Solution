-- ============================================================
-- MasterDataDB Migration 004 — Organization Structure Entities
-- Adds: Companies, Branches, Divisions, Teams, BusinessUnits, CostCenters,
-- ProfitCenters, OrganizationCalendars, Holidays, EmployeeTeamAssignments.
-- Also adds Departments.DivisionId (+FK to Divisions).
-- All DDL is guarded with IF NOT EXISTS — safe to run multiple times.
-- ============================================================

USE MasterDataDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Companies
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Companies')
CREATE TABLE Companies (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Name            NVARCHAR(200)    NOT NULL,
    Code            NVARCHAR(50)     NOT NULL,
    TaxCode         NVARCHAR(50)     NULL,
    LegalAddress    NVARCHAR(500)    NULL,
    CurrencyCode    NVARCHAR(3)      NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION
);
GO

-- Company is the root of the hierarchy (no parent to scope by), so the
-- unique code index is on Code alone.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Companies_Code' AND object_id = OBJECT_ID('Companies'))
    CREATE UNIQUE INDEX IX_Companies_Code ON Companies (Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Branches (FK CompanyId -> Companies)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Branches')
CREATE TABLE Branches (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    CompanyId           UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(200)    NOT NULL,
    Code                NVARCHAR(50)     NOT NULL,
    Address             NVARCHAR(500)    NULL,
    WardId              UNIQUEIDENTIFIER NULL,
    ManagerEmployeeId   UNIQUEIDENTIFIER NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    Description         NVARCHAR(MAX)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_Branches_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Branches_Company' AND object_id = OBJECT_ID('Branches'))
    CREATE INDEX IX_Branches_Company ON Branches (CompanyId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Branches_Code' AND object_id = OBJECT_ID('Branches'))
    CREATE UNIQUE INDEX IX_Branches_Code ON Branches (CompanyId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Divisions (FK BranchId -> Branches, self-ref FK ParentDivisionId -> Divisions)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Divisions')
CREATE TABLE Divisions (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    BranchId            UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(200)    NOT NULL,
    Code                NVARCHAR(50)     NOT NULL,
    ParentDivisionId    UNIQUEIDENTIFIER NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    Description         NVARCHAR(MAX)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_Divisions_Branches FOREIGN KEY (BranchId) REFERENCES Branches(Id),
    CONSTRAINT FK_Divisions_Divisions FOREIGN KEY (ParentDivisionId) REFERENCES Divisions(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Divisions_Branch' AND object_id = OBJECT_ID('Divisions'))
    CREATE INDEX IX_Divisions_Branch ON Divisions (BranchId);
GO

-- Division has no CompanyId column of its own; scope the unique code index
-- by its direct parent (BranchId) instead.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Divisions_Code' AND object_id = OBJECT_ID('Divisions'))
    CREATE UNIQUE INDEX IX_Divisions_Code ON Divisions (BranchId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Departments — add DivisionId (FK to Divisions)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Departments') AND name = 'DivisionId')
    ALTER TABLE Departments ADD DivisionId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Departments_Divisions')
    ALTER TABLE Departments ADD CONSTRAINT FK_Departments_Divisions FOREIGN KEY (DivisionId) REFERENCES Divisions(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Departments_Division' AND object_id = OBJECT_ID('Departments'))
    CREATE INDEX IX_Departments_Division ON Departments (DivisionId);
GO

-- ============================================================
-- Teams (FK DepartmentId -> Departments)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Teams')
CREATE TABLE Teams (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    DepartmentId        UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(200)    NOT NULL,
    Code                NVARCHAR(50)     NOT NULL,
    LeaderEmployeeId    UNIQUEIDENTIFIER NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    Description         NVARCHAR(MAX)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_Teams_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Teams_Department' AND object_id = OBJECT_ID('Teams'))
    CREATE INDEX IX_Teams_Department ON Teams (DepartmentId);
GO

-- Team has no CompanyId column of its own; scope the unique code index by
-- its direct parent (DepartmentId) instead.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Teams_Code' AND object_id = OBJECT_ID('Teams'))
    CREATE UNIQUE INDEX IX_Teams_Code ON Teams (DepartmentId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- BusinessUnits (FK CompanyId -> Companies)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BusinessUnits')
CREATE TABLE BusinessUnits (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    CompanyId       UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    Code            NVARCHAR(50)     NOT NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_BusinessUnits_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BusinessUnits_Company' AND object_id = OBJECT_ID('BusinessUnits'))
    CREATE INDEX IX_BusinessUnits_Company ON BusinessUnits (CompanyId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BusinessUnits_Code' AND object_id = OBJECT_ID('BusinessUnits'))
    CREATE UNIQUE INDEX IX_BusinessUnits_Code ON BusinessUnits (CompanyId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- CostCenters (FK CompanyId -> Companies, nullable FK DepartmentId -> Departments)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CostCenters')
CREATE TABLE CostCenters (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    CompanyId       UNIQUEIDENTIFIER NOT NULL,
    DepartmentId    UNIQUEIDENTIFIER NULL,
    Name            NVARCHAR(200)    NOT NULL,
    Code            NVARCHAR(50)     NOT NULL,
    BudgetAmount    DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    IsActive        BIT              NOT NULL DEFAULT 1,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_CostCenters_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id),
    CONSTRAINT FK_CostCenters_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CostCenters_Company' AND object_id = OBJECT_ID('CostCenters'))
    CREATE INDEX IX_CostCenters_Company ON CostCenters (CompanyId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CostCenters_Department' AND object_id = OBJECT_ID('CostCenters'))
    CREATE INDEX IX_CostCenters_Department ON CostCenters (DepartmentId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CostCenters_Code' AND object_id = OBJECT_ID('CostCenters'))
    CREATE UNIQUE INDEX IX_CostCenters_Code ON CostCenters (CompanyId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- ProfitCenters (FK CompanyId -> Companies, nullable FK BusinessUnitId -> BusinessUnits)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProfitCenters')
CREATE TABLE ProfitCenters (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    CompanyId       UNIQUEIDENTIFIER NOT NULL,
    BusinessUnitId  UNIQUEIDENTIFIER NULL,
    Name            NVARCHAR(200)    NOT NULL,
    Code            NVARCHAR(50)     NOT NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_ProfitCenters_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id),
    CONSTRAINT FK_ProfitCenters_BusinessUnits FOREIGN KEY (BusinessUnitId) REFERENCES BusinessUnits(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProfitCenters_Company' AND object_id = OBJECT_ID('ProfitCenters'))
    CREATE INDEX IX_ProfitCenters_Company ON ProfitCenters (CompanyId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProfitCenters_BusinessUnit' AND object_id = OBJECT_ID('ProfitCenters'))
    CREATE INDEX IX_ProfitCenters_BusinessUnit ON ProfitCenters (BusinessUnitId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProfitCenters_Code' AND object_id = OBJECT_ID('ProfitCenters'))
    CREATE UNIQUE INDEX IX_ProfitCenters_Code ON ProfitCenters (CompanyId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- OrganizationCalendars (FK CompanyId -> Companies)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrganizationCalendars')
CREATE TABLE OrganizationCalendars (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    CompanyId       UNIQUEIDENTIFIER NOT NULL,
    Year            INT              NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    IsDefault       BIT              NOT NULL DEFAULT 0,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_OrganizationCalendars_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrganizationCalendars_Company' AND object_id = OBJECT_ID('OrganizationCalendars'))
    CREATE INDEX IX_OrganizationCalendars_Company ON OrganizationCalendars (CompanyId);
GO

-- OrganizationCalendar has no Code field — no unique code index.

-- ============================================================
-- Holidays (FK CalendarId -> OrganizationCalendars)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Holidays')
CREATE TABLE Holidays (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    CalendarId          UNIQUEIDENTIFIER NOT NULL,
    Date                DATE             NOT NULL,
    Name                NVARCHAR(200)    NOT NULL,
    IsRecurringYearly   BIT              NOT NULL DEFAULT 0,
    Description         NVARCHAR(MAX)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_Holidays_OrganizationCalendars FOREIGN KEY (CalendarId) REFERENCES OrganizationCalendars(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Holidays_Calendar' AND object_id = OBJECT_ID('Holidays'))
    CREATE INDEX IX_Holidays_Calendar ON Holidays (CalendarId);
GO

-- Holiday has no Code field — no unique code index.

-- ============================================================
-- EmployeeTeamAssignments (FK EmployeeId -> Employees, FK TeamId -> Teams,
-- nullable FK PositionId -> Positions)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmployeeTeamAssignments')
CREATE TABLE EmployeeTeamAssignments (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    EmployeeId      UNIQUEIDENTIFIER NOT NULL,
    TeamId          UNIQUEIDENTIFIER NOT NULL,
    PositionId      UNIQUEIDENTIFIER NULL,
    EffectiveFrom   DATE             NOT NULL,
    EffectiveTo     DATE             NULL,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_EmployeeTeamAssignments_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
    CONSTRAINT FK_EmployeeTeamAssignments_Teams FOREIGN KEY (TeamId) REFERENCES Teams(Id),
    CONSTRAINT FK_EmployeeTeamAssignments_Positions FOREIGN KEY (PositionId) REFERENCES Positions(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeTeamAssignments_Employee' AND object_id = OBJECT_ID('EmployeeTeamAssignments'))
    CREATE INDEX IX_EmployeeTeamAssignments_Employee ON EmployeeTeamAssignments (EmployeeId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeTeamAssignments_Team' AND object_id = OBJECT_ID('EmployeeTeamAssignments'))
    CREATE INDEX IX_EmployeeTeamAssignments_Team ON EmployeeTeamAssignments (TeamId);
GO

-- EmployeeTeamAssignment has no Code field — no unique code index.
