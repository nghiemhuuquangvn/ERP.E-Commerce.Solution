-- ============================================================
-- CrmDB Schema — CRMService (Port 5006, Code: CRM)
-- Contacts, Leads, Opportunities, Cases, Activities
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'CrmDB')
    CREATE DATABASE CrmDB;
GO

USE CrmDB;
GO

-- ============================================================
-- Contacts (customers, leads, prospects)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Contacts')
CREATE TABLE Contacts (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    FirstName       NVARCHAR(100)    NOT NULL,
    LastName        NVARCHAR(100)    NOT NULL,
    Email           NVARCHAR(256)    NULL,
    Phone           NVARCHAR(30)     NULL,
    Mobile          NVARCHAR(30)     NULL,
    Company         NVARCHAR(300)    NULL,
    JobTitle        NVARCHAR(200)    NULL,
    Type            INT              NOT NULL DEFAULT 0, -- 0=Individual, 1=Company, 2=Partner
    Source          INT              NOT NULL DEFAULT 0, -- 0=Website, 1=Referral, 2=Social, 3=Cold, 4=Event
    OwnedByUserId   NVARCHAR(100)    NULL,
    -- Address
    Street          NVARCHAR(255)    NULL,
    City            NVARCHAR(100)    NULL,
    State           NVARCHAR(100)    NULL,
    PostalCode      NVARCHAR(20)     NULL,
    Country         NVARCHAR(100)    NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    Tags            NVARCHAR(1000)   NULL, -- comma-separated or JSON
    Notes           NVARCHAR(2000)   NULL,
    UserId          UNIQUEIDENTIFIER NULL, -- linked Identity user (for customer portal)
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
CREATE INDEX IX_Contacts_Email    ON Contacts (Email, TenantId) WHERE IsDeleted = 0 AND Email IS NOT NULL;
CREATE INDEX IX_Contacts_TenantId ON Contacts (TenantId, IsActive, UpdatedAt DESC);
CREATE INDEX IX_Contacts_UserId   ON Contacts (UserId) WHERE UserId IS NOT NULL;
GO

-- ============================================================
-- Leads
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Leads')
CREATE TABLE Leads (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    LeadName        NVARCHAR(300)    NOT NULL,
    ContactId       UNIQUEIDENTIFIER NULL,
    Email           NVARCHAR(256)    NULL,
    Phone           NVARCHAR(30)     NULL,
    Company         NVARCHAR(300)    NULL,
    LeadStatus      INT              NOT NULL DEFAULT 0, -- 0=New, 1=Contacted, 2=Qualified, 3=Unqualified, 4=Converted
    LeadSource      INT              NOT NULL DEFAULT 0, -- 0=Website, 1=Referral, 2=Social, 3=Event, 4=Cold
    EstimatedValue  DECIMAL(18, 2)   NULL,
    AssignedToUserId NVARCHAR(100)   NULL,
    QualifiedAt     DATETIME2        NULL,
    ConvertedAt     DATETIME2        NULL,
    ConvertedToId   UNIQUEIDENTIFIER NULL, -- Opportunity.Id
    LostAt          DATETIME2        NULL,
    LostReason      NVARCHAR(500)    NULL,
    Notes           NVARCHAR(2000)   NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Leads_Contact FOREIGN KEY (ContactId) REFERENCES Contacts(Id)
);
CREATE INDEX IX_Leads_TenantId   ON Leads (TenantId, LeadStatus, CreatedAt DESC);
CREATE INDEX IX_Leads_Assigned   ON Leads (AssignedToUserId, LeadStatus) WHERE IsDeleted = 0;
CREATE INDEX IX_Leads_Contact    ON Leads (ContactId) WHERE ContactId IS NOT NULL;
GO

-- ============================================================
-- Opportunities
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Opportunities')
CREATE TABLE Opportunities (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(300)    NOT NULL,
    ContactId           UNIQUEIDENTIFIER NULL,
    LeadId              UNIQUEIDENTIFIER NULL,
    Stage               INT              NOT NULL DEFAULT 0, -- 0=Prospect, 1=Qualification, 2=Proposal, 3=Negotiation, 4=Won, 5=Lost
    Amount              DECIMAL(18, 2)   NULL,
    Probability         INT              NOT NULL DEFAULT 10, -- 0-100%
    ExpectedCloseDate   DATE             NULL,
    AssignedToUserId    NVARCHAR(100)    NULL,
    WonAt               DATETIME2        NULL,
    LostAt              DATETIME2        NULL,
    LostReason          NVARCHAR(500)    NULL,
    Notes               NVARCHAR(2000)   NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Opp_Contact FOREIGN KEY (ContactId) REFERENCES Contacts(Id),
    CONSTRAINT FK_Opp_Lead    FOREIGN KEY (LeadId)    REFERENCES Leads(Id)
);
CREATE INDEX IX_Opps_TenantId  ON Opportunities (TenantId, Stage, UpdatedAt DESC);
CREATE INDEX IX_Opps_Assigned  ON Opportunities (AssignedToUserId, Stage) WHERE IsDeleted = 0;
CREATE INDEX IX_Opps_Contact   ON Opportunities (ContactId) WHERE ContactId IS NOT NULL;
GO

-- ============================================================
-- Cases (Customer Support)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Cases')
CREATE TABLE Cases (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    CaseNumber          NVARCHAR(50)     NOT NULL,
    ContactId           UNIQUEIDENTIFIER NULL,
    OrderId             UNIQUEIDENTIFIER NULL,
    Subject             NVARCHAR(500)    NOT NULL,
    Description         NVARCHAR(MAX)    NULL,
    Status              INT              NOT NULL DEFAULT 0, -- 0=Open, 1=InProgress, 2=Pending, 3=Resolved, 4=Closed
    Priority            INT              NOT NULL DEFAULT 1, -- 0=Low, 1=Normal, 2=High, 3=Urgent
    Category            NVARCHAR(100)    NULL,
    AssignedToUserId    NVARCHAR(100)    NULL,
    Resolution          NVARCHAR(MAX)    NULL,
    ResolvedAt          DATETIME2        NULL,
    ClosedAt            DATETIME2        NULL,
    SlaDeadline         DATETIME2        NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Cases_Contact FOREIGN KEY (ContactId) REFERENCES Contacts(Id)
);
CREATE UNIQUE INDEX IX_Cases_Number ON Cases (CaseNumber, TenantId) WHERE IsDeleted = 0;
CREATE INDEX IX_Cases_TenantId      ON Cases (TenantId, Status, Priority, CreatedAt DESC);
CREATE INDEX IX_Cases_Assigned      ON Cases (AssignedToUserId, Status) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Activities (interaction history: calls, emails, meetings, notes)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Activities')
CREATE TABLE Activities (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Type            INT              NOT NULL, -- 0=Call, 1=Email, 2=Meeting, 3=Note, 4=Task
    Subject         NVARCHAR(500)    NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    ContactId       UNIQUEIDENTIFIER NULL,
    LeadId          UNIQUEIDENTIFIER NULL,
    OpportunityId   UNIQUEIDENTIFIER NULL,
    CaseId          UNIQUEIDENTIFIER NULL,
    DurationMinutes INT              NULL,
    OccurredAt      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt     DATETIME2        NULL,
    IsCompleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy       NVARCHAR(256)    NOT NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_Activities_Contact      ON Activities (ContactId, OccurredAt DESC) WHERE ContactId IS NOT NULL;
CREATE INDEX IX_Activities_Lead         ON Activities (LeadId, OccurredAt DESC)    WHERE LeadId IS NOT NULL;
CREATE INDEX IX_Activities_Opportunity  ON Activities (OpportunityId, OccurredAt DESC) WHERE OpportunityId IS NOT NULL;
CREATE INDEX IX_Activities_Case         ON Activities (CaseId, OccurredAt DESC)    WHERE CaseId IS NOT NULL;
CREATE INDEX IX_Activities_TenantId     ON Activities (TenantId, OccurredAt DESC);
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
