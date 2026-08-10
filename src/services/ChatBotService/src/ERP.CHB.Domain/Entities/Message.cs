using ERP.CHB.Domain.Enums;

namespace ERP.CHB.Domain.Entities;

/// <summary>
/// A single turn in a Conversation. Owned by Conversation (no independent identity
/// lookup), mirrors NotificationLog's owned-entity pattern rather than inheriting
/// BaseEntity — a chat message doesn't need tenant/soft-delete/row-version machinery
/// of its own, it's always read through its parent Conversation.
/// </summary>
public class Message
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;

    /// <summary>Set only when Role == Tool: which tool was invoked (e.g. "get_order_status").</summary>
    public string? ToolName { get; private set; }

    /// <summary>Set only when Role == Tool: the provider's tool-call id, needed to correlate the tool_result back to the tool_use block.</summary>
    public string? ToolCallId { get; private set; }

    /// <summary>Set only when Role == Tool: the JSON arguments the model requested, kept for audit/debugging.</summary>
    public string? ToolArgumentsJson { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Message() { }

    public static Message CreateUser(Guid conversationId, string content) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = MessageRole.User,
        Content = content,
        CreatedAt = DateTime.UtcNow
    };

    public static Message CreateAssistant(Guid conversationId, string content) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = MessageRole.Assistant,
        Content = content,
        CreatedAt = DateTime.UtcNow
    };

    public static Message CreateTool(Guid conversationId, string toolName, string toolCallId, string toolArgumentsJson, string resultContent) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = MessageRole.Tool,
        Content = resultContent,
        ToolName = toolName,
        ToolCallId = toolCallId,
        ToolArgumentsJson = toolArgumentsJson,
        CreatedAt = DateTime.UtcNow
    };
}
