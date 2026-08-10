using ERP.CHB.Domain.Enums;

namespace ERP.CHB.Application.Dtos;

public record ConversationDto(
    Guid Id,
    ConversationChannel Channel,
    AiProvider Provider,
    ConversationStatus Status,
    DateTime StartedAt,
    DateTime? ClosedAt,
    Guid? EscalatedCaseId);

public record MessageDto(
    Guid Id,
    MessageRole Role,
    string Content,
    string? ToolName,
    DateTime CreatedAt);

public record ConversationHistoryDto(ConversationDto Conversation, IReadOnlyList<MessageDto> Messages);

/// <summary>Provider-agnostic tool definition surfaced to the LLM — each IErpTool implementation supplies one of these; providers convert it to their own wire format (Anthropic "input_schema" / OpenAI "parameters" are structurally identical JSON Schema objects).</summary>
public record ChatToolDefinition(string Name, string Description, string InputSchemaJson);
