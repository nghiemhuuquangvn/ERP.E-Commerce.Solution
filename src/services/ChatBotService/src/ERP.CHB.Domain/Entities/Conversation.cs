using ERP.CHB.Domain.Enums;
using ERP.Shared.Core.Domain;

namespace ERP.CHB.Domain.Entities;

/// <summary>
/// Aggregate root for one chat session. TenantId/CreatedBy come from BaseEntity;
/// CreatedBy holds the authenticated UserId, or null for an unauthenticated
/// Storefront guest (see GuestSessionId).
/// </summary>
public class Conversation : BaseEntity
{
    public ConversationChannel Channel { get; private set; }
    public AiProvider Provider { get; private set; }
    public ConversationStatus Status { get; private set; }

    /// <summary>Client-generated id (e.g. a browser-stored UUID) identifying an unauthenticated Storefront visitor. Null once CreatedBy (UserId) is set.</summary>
    public string? GuestSessionId { get; private set; }

    public DateTime StartedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    /// <summary>Set when Status becomes Escalated — the CRM case opened on the caller's behalf.</summary>
    public Guid? EscalatedCaseId { get; private set; }

    private readonly List<Message> _messages = new();
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    public static Conversation Start(Guid tenantId, ConversationChannel channel, AiProvider provider, string? userId, string? guestSessionId)
    {
        if (userId is null && guestSessionId is null)
            throw new ArgumentException("A conversation needs either an authenticated UserId or a GuestSessionId.");

        return new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = channel,
            Provider = provider,
            Status = ConversationStatus.Active,
            GuestSessionId = guestSessionId,
            CreatedBy = userId,
            StartedAt = DateTime.UtcNow
        };
    }

    public Message AppendUserMessage(string content)
    {
        EnsureActive();
        var message = Message.CreateUser(Id, content);
        _messages.Add(message);
        return message;
    }

    public Message AppendAssistantMessage(string content)
    {
        EnsureActive();
        var message = Message.CreateAssistant(Id, content);
        _messages.Add(message);
        return message;
    }

    public Message AppendToolMessage(string toolName, string toolCallId, string toolArgumentsJson, string resultContent)
    {
        EnsureActive();
        var message = Message.CreateTool(Id, toolName, toolCallId, toolArgumentsJson, resultContent);
        _messages.Add(message);
        return message;
    }

    public void Escalate(Guid crmCaseId)
    {
        EnsureActive();
        Status = ConversationStatus.Escalated;
        EscalatedCaseId = crmCaseId;
    }

    public void Close()
    {
        if (Status == ConversationStatus.Closed)
            return;

        Status = ConversationStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }

    private void EnsureActive()
    {
        if (Status == ConversationStatus.Closed)
            throw new InvalidOperationException($"Conversation {Id} is closed and cannot accept new messages.");
    }
}
