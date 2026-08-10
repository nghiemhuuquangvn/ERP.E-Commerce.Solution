using ERP.CHB.Application.Dtos;
using ERP.CHB.Domain.Enums;

namespace ERP.CHB.Application.Interfaces;

/// <summary>One turn of prior conversation, in a shape both Anthropic and OpenAI providers can convert to their own wire format.</summary>
public abstract record ProviderMessage;
public sealed record ProviderUserMessage(string Content) : ProviderMessage;
public sealed record ProviderToolUse(string ToolCallId, string ToolName, string ArgumentsJson);
public sealed record ProviderAssistantMessage(string? Content, IReadOnlyList<ProviderToolUse>? ToolUses) : ProviderMessage;
public sealed record ProviderToolResultMessage(string ToolCallId, string ToolName, string Content) : ProviderMessage;

public sealed record ChatCompletionRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<ProviderMessage> History,
    IReadOnlyList<ChatToolDefinition> Tools);

/// <summary>One increment of a streamed model turn. Exactly one of the concrete types below.</summary>
public abstract record ChatStreamEvent;
public sealed record TextDeltaEvent(string Delta) : ChatStreamEvent;
public sealed record ToolUseRequestedEvent(string ToolCallId, string ToolName, string ArgumentsJson) : ChatStreamEvent;

/// <summary>Terminal event for one model turn. StopReason "tool_use" means the caller must execute the requested tools and call the provider again with the results appended; any other value means the turn is done.</summary>
public sealed record TurnCompletedEvent(string StopReason) : ChatStreamEvent;

/// <summary>
/// Abstraction over a single LLM vendor's Messages/Chat-Completions API. Two
/// implementations exist (AnthropicChatCompletionProvider, OpenAiChatCompletionProvider)
/// — which one is used for a given conversation is decided once at conversation start by
/// IChatProviderResolver from the caller's tenant license, not by this interface.
/// </summary>
public interface IChatCompletionProvider
{
    AiProvider Provider { get; }
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatCompletionRequest request, CancellationToken ct);
}

/// <summary>Resolves which provider/model a conversation should use. See docs/business/Identity/License.md and ModuleCodes.ChatBotPremium — this reuses the existing per-tenant LicensePlan entitlement instead of new billing infrastructure.</summary>
public interface IChatProviderResolver
{
    AiProvider ResolveProvider(ERP.Shared.Authorization.UserContext caller);
    IChatCompletionProvider GetProvider(AiProvider provider);
    string GetModel(AiProvider provider, ConversationChannel channel);
}
