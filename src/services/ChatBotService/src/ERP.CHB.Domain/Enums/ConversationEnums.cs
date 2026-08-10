namespace ERP.CHB.Domain.Enums;

/// <summary>Which frontend app started the conversation — drives which tool set and model tier apply.</summary>
public enum ConversationChannel
{
    Storefront = 1,
    Admin = 2
}

public enum ConversationStatus
{
    Active = 1,
    Escalated = 2,
    Closed = 3
}

/// <summary>
/// Resolved once at conversation start from the tenant's licensed_module JWT claims
/// (see ERP.Shared.Authorization.ModuleCodes.ChatBotPremium) and fixed for the whole
/// conversation, so mid-conversation switches from a mid-refresh token can't silently
/// change providers under a user.
/// </summary>
public enum AiProvider
{
    Claude = 1,
    ChatGpt = 2
}

public enum MessageRole
{
    User = 1,
    Assistant = 2,
    Tool = 3
}
