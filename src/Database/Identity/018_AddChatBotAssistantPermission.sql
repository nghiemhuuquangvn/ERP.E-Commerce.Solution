-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 018: chatbot:assistant:use permission — gates the Admin channel of
-- ChatBotService (StartConversationCommandHandler checks this for
-- ConversationChannel.Admin; the Admin frontend's Sidebar/route also gate on it).
-- Missed in migration 017 (which only added the license-module side of the ChatBot
-- feature) — found via live testing: even SuperAdmin's token lacked this permission
-- string because it was referenced in code/frontend but never seeded here.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
DECLARE @PermChatBotAssistantUseId UNIQUEIDENTIFIER = '50000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'chatbot:assistant:use')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermChatBotAssistantUseId, 'chatbot:assistant:use', 'Use Admin AI ChatBot Assistant', 'ChatBot');
GO

DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
DECLARE @PermChatBotAssistantUseId UNIQUEIDENTIFIER = (SELECT Id FROM Permissions WHERE PermissionCode = 'chatbot:assistant:use');

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermChatBotAssistantUseId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermChatBotAssistantUseId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermChatBotAssistantUseId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermChatBotAssistantUseId);
GO
