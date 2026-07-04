using ERP.CRM.Application.DTOs;
using MediatR;

namespace ERP.CRM.Application.Queries;

// Contact queries
public record GetContactQuery(Guid ContactId) : IRequest<ContactDto?>;

public record ListContactsQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<ContactDto>>;

// Lead queries
public record GetLeadQuery(Guid LeadId) : IRequest<LeadDto?>;

public record ListLeadsQuery(
    int? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<LeadDto>>;

// Opportunity queries
public record GetOpportunityQuery(Guid OpportunityId) : IRequest<OpportunityDto?>;

public record ListOpportunitiesQuery(
    Guid? ContactId = null,
    int? Stage = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<OpportunityDto>>;

// Case queries
public record GetCaseQuery(Guid CaseId) : IRequest<CaseDto?>;

public record ListCasesQuery(
    Guid? ContactId = null,
    int? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<CaseDto>>;

// Activity queries
public record GetActivityQuery(Guid ActivityId) : IRequest<ActivityDto?>;

public record ListActivitiesQuery(
    Guid? ContactId = null,
    Guid? OpportunityId = null,
    int? ActivityType = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<ActivityDto>>;

// Tag queries
public record GetContactTagsQuery(Guid ContactId) : IRequest<ContactTagsDto>;

public record ListContactsByTagQuery(string Tag, int Page = 1, int PageSize = 20) : IRequest<PagedResult<ContactDto>>;

public record GetAllTagsQuery() : IRequest<IEnumerable<string>>;

// Pipeline summary
public record GetPipelineSummaryQuery() : IRequest<PipelineSummaryDto>;
