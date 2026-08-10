using System.Text.Json;
using ERP.CHB.Application.Interfaces;
using ERP.CHB.Domain.Entities;
using ERP.CHB.Domain.Enums;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.CHB.Application.Commands;

/// <summary>
/// CallerJwt is the raw bearer token of whoever is chatting (null for an unauthenticated
/// Storefront guest) — forwarded as-is to every tool call so downstream services apply
/// their own RBAC/tenant scoping. Fired from ChatHub.SendMessage, not a REST controller,
/// because the reply streams back over the same Hub connection via IChatStreamNotifier
/// rather than being returned by this command.
/// </summary>
public record SendMessageCommand(Guid ConversationId, string Content, UserContext? Caller, string? CallerJwt) : IRequest;

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand>
{
    /// <summary>Hard cap on tool-calling rounds per user turn — guards against a runaway loop (and runaway Anthropic/OpenAI spend) if a model keeps requesting tools.</summary>
    private const int MaxToolRounds = 6;

    private const string EscalateToolName = "escalate_to_human";

    /// <summary>
    /// Built-in tool available on every channel/caller (not sourced from IErpToolRegistry,
    /// which only lists tools backed by an outbound Gateway call) — always offered to the
    /// model so it has a defined way to hand off instead of guessing or refusing silently.
    /// </summary>
    private static readonly Dtos.ChatToolDefinition EscalateToolDefinition = new(
        EscalateToolName,
        "Open a support case for a human agent to follow up, when the request cannot be resolved with the available tools or the user explicitly asks for a human.",
        """{"type":"object","properties":{"reason":{"type":"string","description":"Short summary of why this needs a human."}},"required":["reason"]}""");

    private readonly IConversationRepository _conversations;
    private readonly IChatProviderResolver _providerResolver;
    private readonly IErpToolRegistry _toolRegistry;
    private readonly IChatStreamNotifier _notifier;
    private readonly ICrmEscalationClient _escalationClient;
    private readonly ILogger<SendMessageCommandHandler> _logger;

    public SendMessageCommandHandler(
        IConversationRepository conversations,
        IChatProviderResolver providerResolver,
        IErpToolRegistry toolRegistry,
        IChatStreamNotifier notifier,
        ICrmEscalationClient escalationClient,
        ILogger<SendMessageCommandHandler> logger)
    {
        _conversations = conversations;
        _providerResolver = providerResolver;
        _toolRegistry = toolRegistry;
        _notifier = notifier;
        _escalationClient = escalationClient;
        _logger = logger;
    }

    public async Task Handle(SendMessageCommand request, CancellationToken ct)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, ct)
            ?? throw new EntityNotFoundException("Conversation", request.ConversationId);

        EnsureCallerOwnsConversation(conversation, request.Caller);

        var isGuest = conversation.CreatedBy is null;
        var tools = _toolRegistry.GetAvailableTools(request.Caller, conversation.Channel, isGuest);
        var provider = _providerResolver.GetProvider(conversation.Provider);
        var model = _providerResolver.GetModel(conversation.Provider, conversation.Channel);
        var systemPrompt = BuildSystemPrompt(conversation.Channel);
        var toolDefinitions = tools.Select(t => t.Definition).Append(EscalateToolDefinition).ToList();

        conversation.AppendUserMessage(request.Content);
        await _conversations.UpdateAsync(conversation, ct);
        await _conversations.SaveChangesAsync(ct);

        try
        {
            for (var round = 0; round < MaxToolRounds; round++)
            {
                var history = MapHistory(conversation);
                var completionRequest = new ChatCompletionRequest(model, systemPrompt, history, toolDefinitions);

                var textBuffer = new System.Text.StringBuilder();
                var pendingToolCalls = new List<ToolUseRequestedEvent>();
                string stopReason = "end_turn";

                await foreach (var evt in provider.StreamAsync(completionRequest, ct))
                {
                    switch (evt)
                    {
                        case TextDeltaEvent delta:
                            textBuffer.Append(delta.Delta);
                            await _notifier.NotifyTextDeltaAsync(conversation.Id, delta.Delta, ct);
                            break;
                        case ToolUseRequestedEvent toolUse:
                            pendingToolCalls.Add(toolUse);
                            break;
                        case TurnCompletedEvent completed:
                            stopReason = completed.StopReason;
                            break;
                    }
                }

                if (textBuffer.Length > 0)
                    conversation.AppendAssistantMessage(textBuffer.ToString());

                if (stopReason != "tool_use" || pendingToolCalls.Count == 0)
                {
                    await _conversations.UpdateAsync(conversation, ct);
                    await _conversations.SaveChangesAsync(ct);
                    await _notifier.NotifyCompletedAsync(conversation.Id, ct);
                    return;
                }

                foreach (var call in pendingToolCalls)
                {
                    var resultContent = await ExecuteToolCallAsync(conversation, request.Caller, request.CallerJwt, tools, call, ct);
                    conversation.AppendToolMessage(call.ToolName, call.ToolCallId, call.ArgumentsJson, resultContent);
                }

                await _conversations.UpdateAsync(conversation, ct);
                await _conversations.SaveChangesAsync(ct);
            }

            // MaxToolRounds exhausted without a final answer — still a completed turn from
            // the client's point of view, just an incomplete one; surface it rather than
            // silently hanging the widget waiting for a signal that will never arrive.
            await _notifier.NotifyErrorAsync(conversation.Id, "The assistant needed too many tool calls to answer — please rephrase or try again.", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessageCommand failed for conversation {ConversationId}", conversation.Id);
            await _notifier.NotifyErrorAsync(conversation.Id, "Sorry, something went wrong answering that.", ct);
        }
    }

    private async Task<string> ExecuteToolCallAsync(
        Conversation conversation,
        UserContext? caller,
        string? callerJwt,
        IReadOnlyList<IErpTool> availableTools,
        ToolUseRequestedEvent call,
        CancellationToken ct)
    {
        if (call.ToolName == EscalateToolName)
            return await HandleEscalationAsync(conversation, caller, callerJwt, call.ArgumentsJson, ct);

        var tool = availableTools.FirstOrDefault(t => t.Definition.Name == call.ToolName);
        if (tool is null)
        {
            // The model hallucinated a tool name outside the set it was offered, or tried
            // to reuse a tool from a different channel/permission tier. Fail the single
            // tool call with an explanatory result rather than throwing — the model can
            // recover and answer without it, same as a real API returning a 4xx.
            return JsonSerializer.Serialize(new { error = $"Tool '{call.ToolName}' is not available to you." });
        }

        await _notifier.NotifyToolCallStartedAsync(conversation.Id, tool.Definition.Name, ct);
        try
        {
            var result = await tool.ExecuteAsync(caller, callerJwt, call.ArgumentsJson, ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tool {ToolName} failed for conversation {ConversationId}", tool.Definition.Name, conversation.Id);
            return JsonSerializer.Serialize(new { error = "This lookup failed. Tell the user you couldn't retrieve that information right now." });
        }
        finally
        {
            await _notifier.NotifyToolCallCompletedAsync(conversation.Id, tool.Definition.Name, ct);
        }
    }

    private async Task<string> HandleEscalationAsync(Conversation conversation, UserContext? caller, string? callerJwt, string argumentsJson, CancellationToken ct)
    {
        if (caller is null || !Guid.TryParse(caller.UserId, out var callerUserId))
        {
            // No IErpTool-style "downstream 4xx" to surface here since we never even call
            // out — a guest has no CRM Contact to attach a Case to at all.
            return JsonSerializer.Serialize(new { error = "Escalation requires an authenticated user. Tell the user to sign in, or share a support contact instead." });
        }

        var subject = "Chatbot escalation";
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("reason", out var reasonEl))
                subject = reasonEl.GetString() ?? subject;
        }
        catch (JsonException)
        {
            // Model sent malformed arguments — fall back to the default subject rather than failing the whole turn.
        }

        var transcript = string.Join("\n", conversation.Messages
            .Where(m => m.Role != MessageRole.Tool)
            .TakeLast(20)
            .Select(m => $"{m.Role}: {m.Content}"));

        try
        {
            var caseId = await _escalationClient.OpenCaseAsync(callerUserId, callerJwt, subject, transcript, ct);
            conversation.Escalate(caseId);
            return JsonSerializer.Serialize(new { caseId, message = "A support case was opened. Tell the user a human will follow up and give them the case reference." });
        }
        catch (Exception ex)
        {
            // Most commonly a 403 (caller lacks crm:cases:manage — see ICrmEscalationClient's
            // doc comment) or 404 (no CRM Contact linked to this user yet). Either way this is
            // a legitimate "couldn't do that" outcome for the model to relay, not a crash.
            _logger.LogWarning(ex, "Escalation failed for conversation {ConversationId}", conversation.Id);
            return JsonSerializer.Serialize(new { error = "Could not open a support case automatically. Tell the user to contact support directly." });
        }
    }

    private static void EnsureCallerOwnsConversation(Conversation conversation, UserContext? caller)
    {
        // Authenticated conversations must match the caller's own UserId. Guest
        // conversations (CreatedBy == null) have no JWT to check against — access
        // control there rests on the ConversationId being an unguessable server-issued
        // GUID handed only to the Hub connection that started it; see StartConversationCommand.
        if (conversation.CreatedBy is not null)
        {
            if (caller is null || !caller.IsAuthenticated || caller.UserId != conversation.CreatedBy)
                throw new UnauthorizedAccessException("This conversation does not belong to the current caller.");
        }
    }

    private static string BuildSystemPrompt(ConversationChannel channel) => channel switch
    {
        ConversationChannel.Storefront =>
            "You are the storefront shopping assistant for this store. Answer questions about " +
            "orders, products, stock, shipping and promotions using the tools provided — never " +
            "guess at data you can look up. Only discuss the caller's own orders. If you cannot " +
            "resolve the request, call escalate_to_human. Keep answers short and friendly.",
        ConversationChannel.Admin =>
            "You are an internal assistant for staff of this ERP system. Use the tools provided " +
            "to look up live data; never fabricate figures. Only use tools the caller is permitted " +
            "to see. If asked to do something outside your tools, say so plainly.",
        _ => "You are a helpful assistant."
    };

    private static IReadOnlyList<ProviderMessage> MapHistory(Conversation conversation)
    {
        var history = new List<ProviderMessage>();
        foreach (var message in conversation.Messages)
        {
            switch (message.Role)
            {
                case MessageRole.User:
                    history.Add(new ProviderUserMessage(message.Content));
                    break;
                case MessageRole.Assistant:
                    history.Add(new ProviderAssistantMessage(message.Content, null));
                    break;
                case MessageRole.Tool:
                    history.Add(new ProviderAssistantMessage(null,
                        new[] { new ProviderToolUse(message.ToolCallId!, message.ToolName!, message.ToolArgumentsJson ?? "{}") }));
                    history.Add(new ProviderToolResultMessage(message.ToolCallId!, message.ToolName!, message.Content));
                    break;
            }
        }
        return history;
    }
}
