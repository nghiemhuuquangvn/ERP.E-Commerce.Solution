-- ============================================================
-- MasterDataDB Catchup — SharedMasterDataService (Port 5015, Code: MST)
-- Migration 002: Adds missing BaseEntity columns (Description, RowVersion)
-- to the existing Departments table, and creates all other tables that were
-- never created (001 was only partially run). All DDL is guarded with
-- IF NOT EXISTS — safe to run multiple times.
-- ============================================================

USE MasterDataDB;
GO

-- ============================================================
-- Departments — add missing BaseEntity columns
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Departments') AND name = 'Description')
    ALTER TABLE Departments ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Departments') AND name = 'RowVersion')
    ALTER TABLE Departments ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- DepartmentTranslations
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DepartmentTranslations')
CREATE TABLE DepartmentTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    Description  NVARCHAR(MAX)    NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    RowVersion   ROWVERSION,
    CONSTRAINT FK_DeptTranslation_Dept FOREIGN KEY (OwnerId) REFERENCES Departments(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DepartmentTranslations_Owner' AND object_id = OBJECT_ID('DepartmentTranslations'))
    CREATE UNIQUE INDEX IX_DepartmentTranslations_Owner ON DepartmentTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Positions
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Positions')
CREATE TABLE Positions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Title           NVARCHAR(200)    NOT NULL,
    Code            NVARCHAR(50)     NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    DepartmentId    UNIQUEIDENTIFIER NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION,
    CONSTRAINT FK_Position_Dept FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Positions_Code' AND object_id = OBJECT_ID('Positions'))
    CREATE UNIQUE INDEX IX_Positions_Code ON Positions (Code, TenantId) WHERE IsDeleted = 0;
GO

-- ============================================================
-- PositionTranslations
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PositionTranslations')
CREATE TABLE PositionTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Title        NVARCHAR(200)    NOT NULL,
    Description  NVARCHAR(MAX)    NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    RowVersion   ROWVERSION,
    CONSTRAINT FK_PositionTranslation_Position FOREIGN KEY (OwnerId) REFERENCES Positions(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PositionTranslations_Owner' AND object_id = OBJECT_ID('PositionTranslations'))
    CREATE UNIQUE INDEX IX_PositionTranslations_Owner ON PositionTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Employees
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Employees')
CREATE TABLE Employees (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    EmployeeNumber      NVARCHAR(50)     NOT NULL,
    FirstName           NVARCHAR(100)    NOT NULL,
    LastName            NVARCHAR(100)    NOT NULL,
    Email               NVARCHAR(256)    NULL,
    Phone               NVARCHAR(30)     NULL,
    DateOfHire          DATE             NOT NULL,
    DepartmentId        UNIQUEIDENTIFIER NOT NULL,
    PositionId          UNIQUEIDENTIFIER NULL,
    EmploymentStatus    INT              NOT NULL DEFAULT 0,
    EmploymentType      INT              NOT NULL DEFAULT 0,
    Description         NVARCHAR(MAX)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_Employee_Dept     FOREIGN KEY (DepartmentId) REFERENCES Departments(Id),
    CONSTRAINT FK_Employee_Position FOREIGN KEY (PositionId)   REFERENCES Positions(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_Number' AND object_id = OBJECT_ID('Employees'))
    CREATE UNIQUE INDEX IX_Employees_Number ON Employees (EmployeeNumber, TenantId) WHERE IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_Email' AND object_id = OBJECT_ID('Employees'))
    CREATE INDEX IX_Employees_Email ON Employees (Email, TenantId) WHERE Email IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_Dept' AND object_id = OBJECT_ID('Employees'))
    CREATE INDEX IX_Employees_Dept ON Employees (DepartmentId, EmploymentStatus);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_TenantId' AND object_id = OBJECT_ID('Employees'))
    CREATE INDEX IX_Employees_TenantId ON Employees (TenantId, EmploymentStatus);
GO

-- ============================================================
-- UnitsOfMeasure
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UnitsOfMeasure')
CREATE TABLE UnitsOfMeasure (
    Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId         UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Code             NVARCHAR(20)     NOT NULL,
    Name             NVARCHAR(100)    NOT NULL,
    Symbol           NVARCHAR(20)     NOT NULL,
    UnitType         INT              NOT NULL DEFAULT 0,
    BaseUnitId       UNIQUEIDENTIFIER NULL,
    ConversionFactor DECIMAL(18, 6)   NOT NULL DEFAULT 1,
    Description      NVARCHAR(MAX)    NULL,
    CreatedBy        NVARCHAR(256)    NULL,
    CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy        NVARCHAR(256)    NULL,
    UpdatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted        BIT              NOT NULL DEFAULT 0,
    RowVersion       ROWVERSION,
    CONSTRAINT FK_UoM_BaseUnit FOREIGN KEY (BaseUnitId) REFERENCES UnitsOfMeasure(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UnitsOfMeasure_Code' AND object_id = OBJECT_ID('UnitsOfMeasure'))
    CREATE UNIQUE INDEX IX_UnitsOfMeasure_Code ON UnitsOfMeasure (Code, TenantId) WHERE IsDeleted = 0;
GO

-- ============================================================
-- UnitOfMeasureTranslations
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UnitOfMeasureTranslations')
CREATE TABLE UnitOfMeasureTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(100)    NOT NULL,
    Symbol       NVARCHAR(20)     NOT NULL,
    Description  NVARCHAR(MAX)    NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    RowVersion   ROWVERSION,
    CONSTRAINT FK_UoMTranslation_UoM FOREIGN KEY (OwnerId) REFERENCES UnitsOfMeasure(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UnitOfMeasureTranslations_Owner' AND object_id = OBJECT_ID('UnitOfMeasureTranslations'))
    CREATE UNIQUE INDEX IX_UnitOfMeasureTranslations_Owner ON UnitOfMeasureTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Countries
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Countries')
CREATE TABLE Countries (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Code        NVARCHAR(3)      NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    PhoneCode   NVARCHAR(10)     NULL,
    Description NVARCHAR(MAX)    NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Countries_Code' AND object_id = OBJECT_ID('Countries'))
    CREATE UNIQUE INDEX IX_Countries_Code ON Countries (Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- CountryTranslations
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CountryTranslations')
CREATE TABLE CountryTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    Description  NVARCHAR(MAX)    NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    RowVersion   ROWVERSION,
    CONSTRAINT FK_CountryTranslation_Country FOREIGN KEY (OwnerId) REFERENCES Countries(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CountryTranslations_Owner' AND object_id = OBJECT_ID('CountryTranslations'))
    CREATE UNIQUE INDEX IX_CountryTranslations_Owner ON CountryTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Provinces
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Provinces')
CREATE TABLE Provinces (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    CountryId   UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION,
    CONSTRAINT FK_Province_Country FOREIGN KEY (CountryId) REFERENCES Countries(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Provinces_Code' AND object_id = OBJECT_ID('Provinces'))
    CREATE UNIQUE INDEX IX_Provinces_Code ON Provinces (CountryId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- ProvinceTranslations
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProvinceTranslations')
CREATE TABLE ProvinceTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    Description  NVARCHAR(MAX)    NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    RowVersion   ROWVERSION,
    CONSTRAINT FK_ProvinceTranslation_Province FOREIGN KEY (OwnerId) REFERENCES Provinces(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProvinceTranslations_Owner' AND object_id = OBJECT_ID('ProvinceTranslations'))
    CREATE UNIQUE INDEX IX_ProvinceTranslations_Owner ON ProvinceTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Districts
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Districts')
CREATE TABLE Districts (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    ProvinceId  UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION,
    CONSTRAINT FK_District_Province FOREIGN KEY (ProvinceId) REFERENCES Provinces(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Districts_Code' AND object_id = OBJECT_ID('Districts'))
    CREATE UNIQUE INDEX IX_Districts_Code ON Districts (ProvinceId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- DistrictTranslations
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DistrictTranslations')
CREATE TABLE DistrictTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    Description  NVARCHAR(MAX)    NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    RowVersion   ROWVERSION,
    CONSTRAINT FK_DistrictTranslation_District FOREIGN KEY (OwnerId) REFERENCES Districts(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DistrictTranslations_Owner' AND object_id = OBJECT_ID('DistrictTranslations'))
    CREATE UNIQUE INDEX IX_DistrictTranslations_Owner ON DistrictTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Wards
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Wards')
CREATE TABLE Wards (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    DistrictId  UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION,
    CONSTRAINT FK_Ward_District FOREIGN KEY (DistrictId) REFERENCES Districts(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Wards_Code' AND object_id = OBJECT_ID('Wards'))
    CREATE UNIQUE INDEX IX_Wards_Code ON Wards (DistrictId, Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- WardTranslations
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WardTranslations')
CREATE TABLE WardTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    Description  NVARCHAR(MAX)    NULL,
    CreatedBy    NVARCHAR(256)    NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy    NVARCHAR(256)    NULL,
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    RowVersion   ROWVERSION,
    CONSTRAINT FK_WardTranslation_Ward FOREIGN KEY (OwnerId) REFERENCES Wards(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WardTranslations_Owner' AND object_id = OBJECT_ID('WardTranslations'))
    CREATE UNIQUE INDEX IX_WardTranslations_Owner ON WardTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- SupportedLanguages
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupportedLanguages')
CREATE TABLE SupportedLanguages (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    Code        NVARCHAR(10)     NOT NULL,
    Name        NVARCHAR(100)    NOT NULL,
    NativeName  NVARCHAR(100)    NOT NULL,
    IsDefault   BIT              NOT NULL DEFAULT 0,
    IsActive    BIT              NOT NULL DEFAULT 1,
    SortOrder   INT              NOT NULL DEFAULT 0,
    Description NVARCHAR(MAX)    NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupportedLanguages_Code' AND object_id = OBJECT_ID('SupportedLanguages'))
    CREATE UNIQUE INDEX IX_SupportedLanguages_Code ON SupportedLanguages (Code) WHERE IsDeleted = 0;
GO

MERGE SupportedLanguages AS target
USING (VALUES
    (N'en-US', N'English',    N'English',     1, 1, 0),
    (N'vi-VN', N'Vietnamese', N'Tiếng Việt',  0, 1, 1)
) AS source (Code, Name, NativeName, IsDefault, IsActive, SortOrder)
ON target.Code = source.Code
WHEN NOT MATCHED THEN
    INSERT (Id, TenantId, Code, Name, NativeName, IsDefault, IsActive, SortOrder)
    VALUES (NEWID(), '00000000-0000-0000-0000-000000000000', source.Code, source.Name, source.NativeName, source.IsDefault, source.IsActive, source.SortOrder);
GO

-- ============================================================
-- OutboxMessages
-- ============================================================

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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboxMessages_Unprocessed' AND object_id = OBJECT_ID('OutboxMessages'))
    CREATE INDEX IX_OutboxMessages_Unprocessed ON OutboxMessages (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL;
GO
