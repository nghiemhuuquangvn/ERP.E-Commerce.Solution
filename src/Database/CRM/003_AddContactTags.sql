-- ============================================================
-- CrmDB Migration 003 — ContactTags
-- ContactTag : BaseEntity is fully wired (ContactsController, ContactTagRepository,
-- CrmCommands/Queries/Validators), but no migration ever created its table —
-- every tag read/write 500'd with "Invalid object name 'ContactTags'".
-- ============================================================

USE CrmDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ContactTags')
CREATE TABLE ContactTags (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    ContactId   UNIQUEIDENTIFIER NOT NULL,
    Tag         NVARCHAR(50)     NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion  ROWVERSION,
    CONSTRAINT FK_ContactTags_Contact FOREIGN KEY (ContactId) REFERENCES Contacts(Id)
);
CREATE UNIQUE INDEX UX_ContactTags_Contact_Tag ON ContactTags (ContactId, Tag);
CREATE INDEX IX_ContactTags_Tag ON ContactTags (Tag);
GO
