using ERP.CHB.Application.Dtos;
using ERP.CHB.Domain.Enums;
using ERP.Shared.Authorization;

namespace ERP.CHB.Application.Interfaces;

/// <summary>
/// One function the LLM can call, backed by a live REST call to another service through
/// the Gateway using the CALLER'S OWN forwarded JWT (see IErpGatewayClient) — never an
/// elevated/service identity. This is what makes tool-calling safe by construction: the
/// downstream service's own RBAC (e.g. SalesOrdersController's "my orders" scoping)
/// decides what the caller can see, ChatBotService never duplicates that logic.
/// </summary>
public interface IErpTool
{
    ChatToolDefinition Definition { get; }

    /// <summary>Channels allowed to see/use this tool at all.</summary>
    IReadOnlyCollection<ConversationChannel> Channels { get; }

    /// <summary>True if an unauthenticated Storefront guest may use this tool (public catalog/stock/promotion lookups only).</summary>
    bool AllowGuest { get; }

    /// <summary>Extra permission check beyond "licensed for chat" — e.g. Admin-only tools like get_sales_summary require a Reporting permission. Null means no extra check.</summary>
    string? RequiredPermission { get; }

    /// <summary>caller is null for an unauthenticated guest; callerJwt is the raw bearer token to forward (null for guest — public endpoints only).</summary>
    Task<string> ExecuteAsync(UserContext? caller, string? callerJwt, string argumentsJson, CancellationToken ct);
}

public interface IErpToolRegistry
{
    IReadOnlyList<IErpTool> GetAvailableTools(UserContext? caller, ConversationChannel channel, bool isGuest);
    IErpTool? Find(string name);
}
