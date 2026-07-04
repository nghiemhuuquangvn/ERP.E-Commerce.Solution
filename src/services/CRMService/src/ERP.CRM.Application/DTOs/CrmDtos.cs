namespace ERP.CRM.Application.DTOs;

public record ContactDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    Guid? UserId,
    string ContactSource,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record LeadDto(
    Guid Id,
    string LeadName,
    string LeadStatus,
    string LeadSource,
    decimal? EstimatedValue,
    Guid? ContactId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record OpportunityDto(
    Guid Id,
    string OpportunityName,
    Guid ContactId,
    string Stage,
    int ProbabilityPercent,
    decimal? EstimatedValue,
    DateTime? ExpectedCloseDate,
    Guid? AssignedToUserId,
    string? LostReason,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CaseDto(
    Guid Id,
    string CaseNumber,
    Guid ContactId,
    string CaseSubject,
    string? CaseDescription,
    string Priority,
    string Status,
    string? ResolutionNote,
    Guid? AssignedToUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PipelineSummaryDto(
    int Prospecting,
    int Qualification,
    int Proposal,
    int Negotiation,
    int Won,
    int Lost,
    decimal TotalValue,
    decimal WonValue
);

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record ActivityDto(
    Guid Id,
    Guid ContactId,
    Guid? OpportunityId,
    Guid? CaseId,
    string ActivityType,
    string Subject,
    string? Notes,
    string Status,
    DateTime? ScheduledAt,
    DateTime? CompletedAt,
    Guid? AssignedToUserId,
    string? Source,
    DateTime CreatedAt
);

public record ContactTagDto(
    Guid ContactId,
    string Tag
);

public record ContactTagsDto(
    Guid ContactId,
    IEnumerable<string> Tags
);
