-- ============================================================
-- AuthDB Schema - IdentityService (Port 5001)
-- Migration 020: OpenIddict server schema (OAuth2/OIDC provider layer) — see
-- docs/business/Identity/CommonServiceRoadmap.md §1, Part A of the OAuth2/OIDC +
-- packaging plan.
--
-- This repo does not use EF Core Migrations (hand-written, numbered, idempotent
-- SQL is the convention — see run-migrations.sh). OpenIddict's EF Core store
-- normally ships its schema via `dotnet ef migrations add`; the DDL below was
-- generated once from a scratch project calling modelBuilder.UseOpenIddict<Guid>()
-- (Guid keys, not OpenIddict's string-key default, to match every other table in
-- this DB) and hand-transcribed into this repo's migration style. AuthDbContext
-- itself calls modelBuilder.UseOpenIddict<Guid>() in OnModelCreating so EF's
-- runtime model matches these tables exactly — do not rename columns here without
-- updating that call's expectations.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- OpenIddictApplications — registered OAuth2/OIDC clients (client_credentials
-- machine clients today; Authorization Code+PKCE clients once Part A2 ships)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OpenIddictApplications')
CREATE TABLE OpenIddictApplications (
    Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ApplicationType         NVARCHAR(50)     NULL,
    ClientId                NVARCHAR(100)    NULL,
    ClientSecret            NVARCHAR(MAX)    NULL,
    ClientType              NVARCHAR(50)     NULL,
    ConcurrencyToken        NVARCHAR(50)     NULL,
    ConsentType             NVARCHAR(50)     NULL,
    DisplayName             NVARCHAR(MAX)    NULL,
    DisplayNames            NVARCHAR(MAX)    NULL,
    JsonWebKeySet           NVARCHAR(MAX)    NULL,
    Permissions             NVARCHAR(MAX)    NULL,
    PostLogoutRedirectUris  NVARCHAR(MAX)    NULL,
    Properties              NVARCHAR(MAX)    NULL,
    RedirectUris            NVARCHAR(MAX)    NULL,
    Requirements            NVARCHAR(MAX)    NULL,
    Settings                NVARCHAR(MAX)    NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OpenIddictApplications_ClientId' AND object_id = OBJECT_ID('OpenIddictApplications'))
    CREATE UNIQUE INDEX IX_OpenIddictApplications_ClientId ON OpenIddictApplications (ClientId) WHERE ClientId IS NOT NULL;
GO

-- ============================================================
-- OpenIddictScopes
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OpenIddictScopes')
CREATE TABLE OpenIddictScopes (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ConcurrencyToken  NVARCHAR(50)     NULL,
    Description       NVARCHAR(MAX)    NULL,
    Descriptions      NVARCHAR(MAX)    NULL,
    DisplayName       NVARCHAR(MAX)    NULL,
    DisplayNames      NVARCHAR(MAX)    NULL,
    Name              NVARCHAR(200)    NULL,
    Properties        NVARCHAR(MAX)    NULL,
    Resources         NVARCHAR(MAX)    NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OpenIddictScopes_Name' AND object_id = OBJECT_ID('OpenIddictScopes'))
    CREATE UNIQUE INDEX IX_OpenIddictScopes_Name ON OpenIddictScopes (Name) WHERE Name IS NOT NULL;
GO

-- ============================================================
-- OpenIddictAuthorizations — one row per granted consent (client_credentials
-- grants get an authorization row too, per OpenIddict convention)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OpenIddictAuthorizations')
CREATE TABLE OpenIddictAuthorizations (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ApplicationId     UNIQUEIDENTIFIER NULL,
    ConcurrencyToken  NVARCHAR(50)     NULL,
    CreationDate      DATETIME2        NULL,
    Properties        NVARCHAR(MAX)    NULL,
    Scopes            NVARCHAR(MAX)    NULL,
    Status            NVARCHAR(50)     NULL,
    Subject           NVARCHAR(400)    NULL,
    Type              NVARCHAR(50)     NULL,
    CONSTRAINT FK_OpenIddictAuthorizations_OpenIddictApplications FOREIGN KEY (ApplicationId) REFERENCES OpenIddictApplications(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type' AND object_id = OBJECT_ID('OpenIddictAuthorizations'))
    CREATE INDEX IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type ON OpenIddictAuthorizations (ApplicationId, Status, Subject, Type);
GO

-- ============================================================
-- OpenIddictTokens — access/refresh/authorization-code token records (payload
-- is the encrypted/signed token material OpenIddict manages itself; this table
-- is separate from the pre-existing RefreshTokens table used by the proprietary
-- /api/v{version}/auth/login flow, which is untouched by this migration)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OpenIddictTokens')
CREATE TABLE OpenIddictTokens (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ApplicationId     UNIQUEIDENTIFIER NULL,
    AuthorizationId   UNIQUEIDENTIFIER NULL,
    ConcurrencyToken  NVARCHAR(50)     NULL,
    CreationDate      DATETIME2        NULL,
    ExpirationDate    DATETIME2        NULL,
    Payload           NVARCHAR(MAX)    NULL,
    Properties        NVARCHAR(MAX)    NULL,
    RedemptionDate    DATETIME2        NULL,
    ReferenceId       NVARCHAR(100)    NULL,
    Status            NVARCHAR(50)     NULL,
    Subject           NVARCHAR(400)    NULL,
    Type              NVARCHAR(150)    NULL,
    CONSTRAINT FK_OpenIddictTokens_OpenIddictApplications FOREIGN KEY (ApplicationId) REFERENCES OpenIddictApplications(Id),
    CONSTRAINT FK_OpenIddictTokens_OpenIddictAuthorizations FOREIGN KEY (AuthorizationId) REFERENCES OpenIddictAuthorizations(Id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OpenIddictTokens_ApplicationId_Status_Subject_Type' AND object_id = OBJECT_ID('OpenIddictTokens'))
    CREATE INDEX IX_OpenIddictTokens_ApplicationId_Status_Subject_Type ON OpenIddictTokens (ApplicationId, Status, Subject, Type);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OpenIddictTokens_AuthorizationId' AND object_id = OBJECT_ID('OpenIddictTokens'))
    CREATE INDEX IX_OpenIddictTokens_AuthorizationId ON OpenIddictTokens (AuthorizationId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OpenIddictTokens_ReferenceId' AND object_id = OBJECT_ID('OpenIddictTokens'))
    CREATE UNIQUE INDEX IX_OpenIddictTokens_ReferenceId ON OpenIddictTokens (ReferenceId) WHERE ReferenceId IS NOT NULL;
GO
