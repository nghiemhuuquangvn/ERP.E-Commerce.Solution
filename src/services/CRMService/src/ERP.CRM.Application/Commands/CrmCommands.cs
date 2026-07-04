using ERP.CRM.Application.DTOs;
using MediatR;

namespace ERP.CRM.Application.Commands;

// Contact commands
public record CreateContactCommand(
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    Guid? UserId,
    int ContactSource = 0
) : IRequest<ContactDto>;

public record UpdateContactCommand(
    Guid ContactId,
    string Name,
    string? Email,
    string? Phone,
    string? Company
) : IRequest<ContactDto>;

// Lead commands
public record CreateLeadCommand(
    string LeadName,
    int LeadSource,
    decimal? EstimatedValue,
    Guid? ContactId
) : IRequest<LeadDto>;

public record QualifyLeadCommand(Guid LeadId) : IRequest<LeadDto>;

public record ContactLeadCommand(Guid LeadId) : IRequest<LeadDto>;

public record LoseLeadCommand(Guid LeadId, string Reason) : IRequest<LeadDto>;

// Opportunity commands
public record CreateOpportunityCommand(
    string Title,
    Guid ContactId,
    decimal Value,
    DateTime ExpectedCloseDate,
    Guid? AssignedToUserId
) : IRequest<OpportunityDto>;

public record AdvanceOpportunityStageCommand(Guid OpportunityId) : IRequest<OpportunityDto>;

public record WinOpportunityCommand(Guid OpportunityId) : IRequest<OpportunityDto>;

public record LoseOpportunityCommand(Guid OpportunityId, string Reason) : IRequest<OpportunityDto>;

// Activity commands
public record CreateActivityCommand(
    Guid ContactId,
    int ActivityType,
    string Subject,
    string? Notes,
    DateTime? ScheduledAt,
    Guid? OpportunityId,
    Guid? CaseId,
    Guid? AssignedToUserId,
    string? Source
) : IRequest<ActivityDto>;

public record CompleteActivityCommand(Guid ActivityId, string? Notes) : IRequest<ActivityDto>;

public record CancelActivityCommand(Guid ActivityId) : IRequest<ActivityDto>;

public record UpdateActivityNotesCommand(Guid ActivityId, string Notes) : IRequest<ActivityDto>;

// Tag commands
public record AddContactTagCommand(Guid ContactId, string Tag) : IRequest<ContactTagsDto>;

public record RemoveContactTagCommand(Guid ContactId, string Tag) : IRequest<ContactTagsDto>;

// Case commands
public record OpenCaseCommand(
    string Title,
    Guid ContactId,
    string Description,
    int Priority
) : IRequest<CaseDto>;

public record AssignCaseCommand(Guid CaseId, Guid AssignedToUserId) : IRequest<CaseDto>;

public record ResolveCaseCommand(Guid CaseId, string Resolution) : IRequest<CaseDto>;

public record CloseCaseCommand(Guid CaseId) : IRequest<CaseDto>;
