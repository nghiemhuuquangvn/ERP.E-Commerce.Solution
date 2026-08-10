-- ============================================================
-- ChatBotDB Schema
-- Service: ChatBotService (Port 5017, Code: CHB)
-- Owns Conversations/ConversationMessages only — all ERP data the bot answers with
-- (orders, products, stock, shipments, promotions) is fetched live via the Gateway per
-- IErpTool, never duplicated into this database. See ChatBotDbContext.OnModelCreating
-- for the exact EF Core mapping this schema must match.
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ChatBotDB')
    CREATE DATABASE ChatBotDB;
GO

USE ChatBotDB;
GO

CREATE TABLE Conversations (
    Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId         UNIQUEIDENTIFIER NOT NULL,
    Channel          INT              NOT NULL, -- 1=Storefront, 2=Admin
    Provider         INT              NOT NULL, -- 1=Claude, 2=ChatGpt
    Status           INT              NOT NULL DEFAULT 1, -- 1=Active, 2=Escalated, 3=Closed
    GuestSessionId   NVARCHAR(200)    NULL,
    CreatedBy        NVARCHAR(200)    NULL, -- authenticated UserId; NULL for a Storefront guest (see GuestSessionId)
    StartedAt        DATETIME2        NOT NULL,
    ClosedAt         DATETIME2        NULL,
    EscalatedCaseId  UNIQUEIDENTIFIER NULL,
    Description      NVARCHAR(MAX)    NULL,
    UpdatedBy        NVARCHAR(200)    NULL,
    CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_Conversations_Tenant_CreatedBy ON Conversations (TenantId, CreatedBy);
CREATE INDEX IX_Conversations_Tenant_GuestSessionId ON Conversations (TenantId, GuestSessionId);
GO

CREATE TABLE ConversationMessages (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    ConversationId      UNIQUEIDENTIFIER NOT NULL,
    Role                INT              NOT NULL, -- 1=User, 2=Assistant, 3=Tool
    Content             NVARCHAR(MAX)    NOT NULL,
    ToolName            NVARCHAR(100)    NULL,
    ToolCallId          NVARCHAR(200)    NULL,
    ToolArgumentsJson   NVARCHAR(MAX)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ConversationMessages_Conversation FOREIGN KEY (ConversationId) REFERENCES Conversations(Id)
);
GO

CREATE INDEX IX_ConversationMessages_ConversationId ON ConversationMessages (ConversationId);
GO
