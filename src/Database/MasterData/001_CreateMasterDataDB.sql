-- ============================================================
-- MasterDataDB Schema — SharedMasterDataService (Port 5015, Code: MST)
-- Departments, Positions, Employees, Units of Measure,
-- Geography (Country -> Province -> District -> Ward), Supported Languages
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'MasterDataDB')
    CREATE DATABASE MasterDataDB;
GO

USE MasterDataDB;
GO

-- ============================================================
-- Departments
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Departments')
CREATE TABLE Departments (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    DepartmentName      NVARCHAR(200)    NOT NULL,
    Code                NVARCHAR(50)     NOT NULL,
    ManagerId           UNIQUEIDENTIFIER NULL, -- Employees.Id
    ParentDepartmentId  UNIQUEIDENTIFIER NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Dept_Parent FOREIGN KEY (ParentDepartmentId) REFERENCES Departments(Id)
);
CREATE UNIQUE INDEX IX_Departments_Code ON Departments (Code, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Departments_TenantId    ON Departments (TenantId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DepartmentTranslations')
CREATE TABLE DepartmentTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_DeptTranslation_Dept FOREIGN KEY (OwnerId) REFERENCES Departments(Id)
);
CREATE UNIQUE INDEX IX_DepartmentTranslations_Owner ON DepartmentTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Positions
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Positions')
CREATE TABLE Positions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Title           NVARCHAR(200)    NOT NULL,
    Code            NVARCHAR(50)     NOT NULL,
    Description     NVARCHAR(1000)   NULL,
    DepartmentId    UNIQUEIDENTIFIER NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Position_Dept FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
);
CREATE UNIQUE INDEX IX_Positions_Code ON Positions (Code, TenantId) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PositionTranslations')
CREATE TABLE PositionTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Title        NVARCHAR(200)    NOT NULL,
    Description  NVARCHAR(1000)   NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_PositionTranslation_Position FOREIGN KEY (OwnerId) REFERENCES Positions(Id)
);
CREATE UNIQUE INDEX IX_PositionTranslations_Owner ON PositionTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Employees (moved from HrmDB — see src/Database/HRM/002_RemoveEmployeeDepartmentOwnership.sql)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Employees')
CREATE TABLE Employees (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    EmployeeNumber      NVARCHAR(50)     NOT NULL,
    FirstName           NVARCHAR(100)    NOT NULL,
    LastName            NVARCHAR(100)    NOT NULL,
    Email               NVARCHAR(256)    NULL,
    Phone               NVARCHAR(30)     NULL,
    DateOfHire          DATE             NOT NULL,
    DepartmentId        UNIQUEIDENTIFIER NOT NULL,
    PositionId          UNIQUEIDENTIFIER NULL,
    EmploymentStatus    INT              NOT NULL DEFAULT 0, -- 0=Active, 1=OnLeave, 2=Terminated
    EmploymentType      INT              NOT NULL DEFAULT 0, -- 0=FullTime, 1=PartTime, 2=Contract
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Employee_Dept     FOREIGN KEY (DepartmentId) REFERENCES Departments(Id),
    CONSTRAINT FK_Employee_Position FOREIGN KEY (PositionId)   REFERENCES Positions(Id)
);
CREATE UNIQUE INDEX IX_Employees_Number ON Employees (EmployeeNumber, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Employees_Email        ON Employees (Email, TenantId) WHERE Email IS NOT NULL;
CREATE INDEX IX_Employees_Dept         ON Employees (DepartmentId, EmploymentStatus);
CREATE INDEX IX_Employees_TenantId     ON Employees (TenantId, EmploymentStatus);
GO

-- ============================================================
-- Units of Measure
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UnitsOfMeasure')
CREATE TABLE UnitsOfMeasure (
    Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId         UNIQUEIDENTIFIER NOT NULL,
    Code             NVARCHAR(20)     NOT NULL,
    Name             NVARCHAR(100)    NOT NULL,
    Symbol           NVARCHAR(20)     NOT NULL,
    UnitType         INT              NOT NULL, -- 0=Weight, 1=Volume, 2=Length, 3=Count, 4=Area
    BaseUnitId       UNIQUEIDENTIFIER NULL,
    ConversionFactor DECIMAL(18, 6)   NOT NULL DEFAULT 1,
    CreatedBy        NVARCHAR(256)    NULL,
    CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy        NVARCHAR(256)    NULL,
    UpdatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted        BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_UoM_BaseUnit FOREIGN KEY (BaseUnitId) REFERENCES UnitsOfMeasure(Id)
);
CREATE UNIQUE INDEX IX_UnitsOfMeasure_Code ON UnitsOfMeasure (Code, TenantId) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UnitOfMeasureTranslations')
CREATE TABLE UnitOfMeasureTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(100)    NOT NULL,
    Symbol       NVARCHAR(20)     NOT NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_UoMTranslation_UoM FOREIGN KEY (OwnerId) REFERENCES UnitsOfMeasure(Id)
);
CREATE UNIQUE INDEX IX_UnitOfMeasureTranslations_Owner ON UnitOfMeasureTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Geography: Country -> Province -> District -> Ward
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Countries')
CREATE TABLE Countries (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(3)      NOT NULL, -- ISO alpha-2/3, e.g. "VN"
    Name        NVARCHAR(200)    NOT NULL,
    PhoneCode   NVARCHAR(10)     NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_Countries_Code ON Countries (Code) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CountryTranslations')
CREATE TABLE CountryTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_CountryTranslation_Country FOREIGN KEY (OwnerId) REFERENCES Countries(Id)
);
CREATE UNIQUE INDEX IX_CountryTranslations_Owner ON CountryTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Provinces')
CREATE TABLE Provinces (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    CountryId   UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Province_Country FOREIGN KEY (CountryId) REFERENCES Countries(Id)
);
CREATE UNIQUE INDEX IX_Provinces_Code ON Provinces (CountryId, Code) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProvinceTranslations')
CREATE TABLE ProvinceTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProvinceTranslation_Province FOREIGN KEY (OwnerId) REFERENCES Provinces(Id)
);
CREATE UNIQUE INDEX IX_ProvinceTranslations_Owner ON ProvinceTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Districts')
CREATE TABLE Districts (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    ProvinceId  UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_District_Province FOREIGN KEY (ProvinceId) REFERENCES Provinces(Id)
);
CREATE UNIQUE INDEX IX_Districts_Code ON Districts (ProvinceId, Code) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DistrictTranslations')
CREATE TABLE DistrictTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_DistrictTranslation_District FOREIGN KEY (OwnerId) REFERENCES Districts(Id)
);
CREATE UNIQUE INDEX IX_DistrictTranslations_Owner ON DistrictTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Wards')
CREATE TABLE Wards (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    DistrictId  UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Ward_District FOREIGN KEY (DistrictId) REFERENCES Districts(Id)
);
CREATE UNIQUE INDEX IX_Wards_Code ON Wards (DistrictId, Code) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WardTranslations')
CREATE TABLE WardTranslations (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    OwnerId      UNIQUEIDENTIFIER NOT NULL,
    LanguageCode NVARCHAR(10)     NOT NULL,
    Name         NVARCHAR(200)    NOT NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_WardTranslation_Ward FOREIGN KEY (OwnerId) REFERENCES Wards(Id)
);
CREATE UNIQUE INDEX IX_WardTranslations_Owner ON WardTranslations (OwnerId, LanguageCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Supported Languages (admin-managed reference list; each service's own
-- appsettings.json Localization:SupportedCultures is the runtime source
-- of truth for request-culture negotiation — see docs/architecture)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupportedLanguages')
CREATE TABLE SupportedLanguages (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(10)     NOT NULL, -- e.g. "vi-VN"
    Name        NVARCHAR(100)    NOT NULL, -- e.g. "Vietnamese"
    NativeName  NVARCHAR(100)    NOT NULL, -- e.g. "Tiếng Việt"
    IsDefault   BIT              NOT NULL DEFAULT 0,
    IsActive    BIT              NOT NULL DEFAULT 1,
    SortOrder   INT              NOT NULL DEFAULT 0,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted   BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_SupportedLanguages_Code ON SupportedLanguages (Code) WHERE IsDeleted = 0;
GO

MERGE SupportedLanguages AS target
USING (VALUES
    (N'en-US', N'English', N'English', 1, 1, 0),
    (N'vi-VN', N'Vietnamese', N'Tiếng Việt', 0, 1, 1)
) AS source (Code, Name, NativeName, IsDefault, IsActive, SortOrder)
ON target.Code = source.Code
WHEN NOT MATCHED THEN
    INSERT (Id, TenantId, Code, Name, NativeName, IsDefault, IsActive, SortOrder)
    VALUES (NEWID(), '00000000-0000-0000-0000-000000000000', source.Code, source.Name, source.NativeName, source.IsDefault, source.IsActive, source.SortOrder);
GO

-- ============================================================
-- Outbox Messages
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
CREATE INDEX IX_OutboxMessages_Unprocessed ON OutboxMessages (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL;
GO
