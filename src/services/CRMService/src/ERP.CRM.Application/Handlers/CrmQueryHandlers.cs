using ERP.CRM.Application.DTOs;
using ERP.CRM.Application.Handlers;
using ERP.CRM.Application.Interfaces;
using ERP.CRM.Application.Queries;
using ERP.CRM.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.CRM.Application.Handlers;

// ─── Contact Query Handlers ───────────────────────────────────────────────────

public class GetContactQueryHandler : IRequestHandler<GetContactQuery, ContactDto?>
{
    private readonly IContactRepository _contacts;

    public GetContactQueryHandler(IContactRepository contacts)
    {
        _contacts = contacts;
    }

    public async Task<ContactDto?> Handle(GetContactQuery request, CancellationToken cancellationToken)
    {
        var contact = await _contacts.GetByIdAsync(request.ContactId, cancellationToken);
        return contact is null ? null : CreateContactCommandHandler.MapToDto(contact);
    }
}

public class ListContactsQueryHandler : IRequestHandler<ListContactsQuery, PagedResult<ContactDto>>
{
    private readonly IContactRepository _contacts;

    public ListContactsQueryHandler(IContactRepository contacts)
    {
        _contacts = contacts;
    }

    public async Task<PagedResult<ContactDto>> Handle(ListContactsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _contacts.GetPagedAsync(request.Search, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateContactCommandHandler.MapToDto);
        return new PagedResult<ContactDto>(dtos, total, request.Page, request.PageSize);
    }
}

// ─── Lead Query Handlers ──────────────────────────────────────────────────────

public class GetLeadQueryHandler : IRequestHandler<GetLeadQuery, LeadDto?>
{
    private readonly ILeadRepository _leads;

    public GetLeadQueryHandler(ILeadRepository leads)
    {
        _leads = leads;
    }

    public async Task<LeadDto?> Handle(GetLeadQuery request, CancellationToken cancellationToken)
    {
        var lead = await _leads.GetByIdAsync(request.LeadId, cancellationToken);
        return lead is null ? null : CreateLeadCommandHandler.MapToDto(lead);
    }
}

public class ListLeadsQueryHandler : IRequestHandler<ListLeadsQuery, PagedResult<LeadDto>>
{
    private readonly ILeadRepository _leads;

    public ListLeadsQueryHandler(ILeadRepository leads)
    {
        _leads = leads;
    }

    public async Task<PagedResult<LeadDto>> Handle(ListLeadsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _leads.GetPagedAsync(request.Status, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateLeadCommandHandler.MapToDto);
        return new PagedResult<LeadDto>(dtos, total, request.Page, request.PageSize);
    }
}

// ─── Opportunity Query Handlers ───────────────────────────────────────────────

public class GetOpportunityQueryHandler : IRequestHandler<GetOpportunityQuery, OpportunityDto?>
{
    private readonly IOpportunityRepository _opportunities;

    public GetOpportunityQueryHandler(IOpportunityRepository opportunities)
    {
        _opportunities = opportunities;
    }

    public async Task<OpportunityDto?> Handle(GetOpportunityQuery request, CancellationToken cancellationToken)
    {
        var opportunity = await _opportunities.GetByIdAsync(request.OpportunityId, cancellationToken);
        return opportunity is null ? null : CreateOpportunityCommandHandler.MapToDto(opportunity);
    }
}

public class ListOpportunitiesQueryHandler : IRequestHandler<ListOpportunitiesQuery, PagedResult<OpportunityDto>>
{
    private readonly IOpportunityRepository _opportunities;

    public ListOpportunitiesQueryHandler(IOpportunityRepository opportunities)
    {
        _opportunities = opportunities;
    }

    public async Task<PagedResult<OpportunityDto>> Handle(ListOpportunitiesQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _opportunities.GetPagedAsync(
            request.ContactId, request.Stage, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateOpportunityCommandHandler.MapToDto);
        return new PagedResult<OpportunityDto>(dtos, total, request.Page, request.PageSize);
    }
}

public class GetPipelineSummaryQueryHandler : IRequestHandler<GetPipelineSummaryQuery, PipelineSummaryDto>
{
    private readonly IOpportunityRepository _opportunities;

    public GetPipelineSummaryQueryHandler(IOpportunityRepository opportunities)
    {
        _opportunities = opportunities;
    }

    public async Task<PipelineSummaryDto> Handle(GetPipelineSummaryQuery request, CancellationToken cancellationToken)
    {
        var stageCounts = await _opportunities.GetStageCounts(cancellationToken);
        var totalValue = await _opportunities.GetTotalValue(cancellationToken);
        var wonValue = await _opportunities.GetWonValue(cancellationToken);

        int GetCount(OpportunityStage stage) =>
            stageCounts.TryGetValue((int)stage, out var count) ? count : 0;

        return new PipelineSummaryDto(
            Prospecting: GetCount(OpportunityStage.Prospecting),
            Qualification: GetCount(OpportunityStage.Qualification),
            Proposal: GetCount(OpportunityStage.Proposal),
            Negotiation: GetCount(OpportunityStage.Negotiation),
            Won: GetCount(OpportunityStage.Won),
            Lost: GetCount(OpportunityStage.Lost),
            TotalValue: totalValue,
            WonValue: wonValue
        );
    }
}

// ─── Case Query Handlers ──────────────────────────────────────────────────────

public class GetCaseQueryHandler : IRequestHandler<GetCaseQuery, CaseDto?>
{
    private readonly ICaseRepository _cases;

    public GetCaseQueryHandler(ICaseRepository cases)
    {
        _cases = cases;
    }

    public async Task<CaseDto?> Handle(GetCaseQuery request, CancellationToken cancellationToken)
    {
        var caseEntity = await _cases.GetByIdAsync(request.CaseId, cancellationToken);
        return caseEntity is null ? null : OpenCaseCommandHandler.MapToDto(caseEntity);
    }
}

public class ListCasesQueryHandler : IRequestHandler<ListCasesQuery, PagedResult<CaseDto>>
{
    private readonly ICaseRepository _cases;

    public ListCasesQueryHandler(ICaseRepository cases)
    {
        _cases = cases;
    }

    public async Task<PagedResult<CaseDto>> Handle(ListCasesQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _cases.GetPagedAsync(
            request.ContactId, request.Status, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(OpenCaseCommandHandler.MapToDto);
        return new PagedResult<CaseDto>(dtos, total, request.Page, request.PageSize);
    }
}
