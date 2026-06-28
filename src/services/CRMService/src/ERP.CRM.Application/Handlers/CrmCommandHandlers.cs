using ERP.CRM.Application.Commands;
using ERP.CRM.Application.DTOs;
using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.CRM.Application.Handlers;

// ─── Contact Handlers ───────────────────────────────────────────────────────

public class CreateContactCommandHandler : IRequestHandler<CreateContactCommand, ContactDto>
{
    private readonly IContactRepository _contacts;
    private readonly ILogger<CreateContactCommandHandler> _logger;

    public CreateContactCommandHandler(IContactRepository contacts, ILogger<CreateContactCommandHandler> logger)
    {
        _contacts = contacts;
        _logger = logger;
    }

    public async Task<ContactDto> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = Contact.Create(request.Name, request.Email, request.Phone, request.UserId);
        contact.Company = request.Company;
        contact.ContactSource = request.ContactSource;

        await _contacts.AddAsync(contact, cancellationToken);

        _logger.LogInformation("Contact {ContactId} created for {Name}", contact.Id, request.Name);

        return MapToDto(contact);
    }

    internal static ContactDto MapToDto(Contact c) => new(
        c.Id,
        c.Name,
        c.Email,
        c.Phone,
        c.Company,
        c.UserId,
        ((ContactSource)c.ContactSource).ToString(),
        c.CreatedAt,
        c.UpdatedAt
    );
}

public class UpdateContactCommandHandler : IRequestHandler<UpdateContactCommand, ContactDto>
{
    private readonly IContactRepository _contacts;
    private readonly ILogger<UpdateContactCommandHandler> _logger;

    public UpdateContactCommandHandler(IContactRepository contacts, ILogger<UpdateContactCommandHandler> logger)
    {
        _contacts = contacts;
        _logger = logger;
    }

    public async Task<ContactDto> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await _contacts.GetByIdAsync(request.ContactId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contact {request.ContactId} not found.");

        contact.Update(request.Name, request.Email, request.Phone, request.Company);
        await _contacts.UpdateAsync(contact, cancellationToken);

        _logger.LogInformation("Contact {ContactId} updated", request.ContactId);

        return CreateContactCommandHandler.MapToDto(contact);
    }
}

// ─── Lead Handlers ───────────────────────────────────────────────────────────

public class CreateLeadCommandHandler : IRequestHandler<CreateLeadCommand, LeadDto>
{
    private readonly ILeadRepository _leads;
    private readonly ILogger<CreateLeadCommandHandler> _logger;

    public CreateLeadCommandHandler(ILeadRepository leads, ILogger<CreateLeadCommandHandler> logger)
    {
        _leads = leads;
        _logger = logger;
    }

    public async Task<LeadDto> Handle(CreateLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = Lead.Create(request.LeadName, request.LeadSource);
        lead.EstimatedValue = request.EstimatedValue;
        lead.ContactId = request.ContactId;

        await _leads.AddAsync(lead, cancellationToken);

        _logger.LogInformation("Lead {LeadId} created: {LeadName}", lead.Id, request.LeadName);

        return MapToDto(lead);
    }

    internal static LeadDto MapToDto(Lead l) => new(
        l.Id,
        l.LeadName,
        ((LeadStatus)l.LeadStatus).ToString(),
        ((LeadSource)l.LeadSource).ToString(),
        l.EstimatedValue,
        l.ContactId,
        l.CreatedAt,
        l.UpdatedAt
    );
}

public class QualifyLeadCommandHandler : IRequestHandler<QualifyLeadCommand, LeadDto>
{
    private readonly ILeadRepository _leads;
    private readonly IOpportunityRepository _opportunities;
    private readonly ILogger<QualifyLeadCommandHandler> _logger;

    public QualifyLeadCommandHandler(
        ILeadRepository leads,
        IOpportunityRepository opportunities,
        ILogger<QualifyLeadCommandHandler> logger)
    {
        _leads = leads;
        _opportunities = opportunities;
        _logger = logger;
    }

    public async Task<LeadDto> Handle(QualifyLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = await _leads.GetByIdAsync(request.LeadId, cancellationToken)
            ?? throw new KeyNotFoundException($"Lead {request.LeadId} not found.");

        // Create an opportunity from this lead
        var opportunity = Opportunity.Create(
            lead.LeadName,
            lead.ContactId ?? Guid.Empty,
            lead.EstimatedValue ?? 0,
            DateTime.UtcNow.AddMonths(3)
        );

        await _opportunities.AddAsync(opportunity, cancellationToken);

        lead.Qualify(opportunity.Id);
        await _leads.UpdateAsync(lead, cancellationToken);

        _logger.LogInformation("Lead {LeadId} qualified, opportunity {OpportunityId} created", lead.Id, opportunity.Id);

        return CreateLeadCommandHandler.MapToDto(lead);
    }
}

public class ContactLeadCommandHandler : IRequestHandler<ContactLeadCommand, LeadDto>
{
    private readonly ILeadRepository _leads;
    private readonly ILogger<ContactLeadCommandHandler> _logger;

    public ContactLeadCommandHandler(ILeadRepository leads, ILogger<ContactLeadCommandHandler> logger)
    {
        _leads = leads;
        _logger = logger;
    }

    public async Task<LeadDto> Handle(ContactLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = await _leads.GetByIdAsync(request.LeadId, cancellationToken)
            ?? throw new KeyNotFoundException($"Lead {request.LeadId} not found.");

        lead.Contact();
        await _leads.UpdateAsync(lead, cancellationToken);

        _logger.LogInformation("Lead {LeadId} marked as contacted", request.LeadId);

        return CreateLeadCommandHandler.MapToDto(lead);
    }
}

public class LoseLeadCommandHandler : IRequestHandler<LoseLeadCommand, LeadDto>
{
    private readonly ILeadRepository _leads;
    private readonly ILogger<LoseLeadCommandHandler> _logger;

    public LoseLeadCommandHandler(ILeadRepository leads, ILogger<LoseLeadCommandHandler> logger)
    {
        _leads = leads;
        _logger = logger;
    }

    public async Task<LeadDto> Handle(LoseLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = await _leads.GetByIdAsync(request.LeadId, cancellationToken)
            ?? throw new KeyNotFoundException($"Lead {request.LeadId} not found.");

        lead.MarkLost(request.Reason);
        await _leads.UpdateAsync(lead, cancellationToken);

        _logger.LogInformation("Lead {LeadId} marked as lost", request.LeadId);

        return CreateLeadCommandHandler.MapToDto(lead);
    }
}

// ─── Opportunity Handlers ─────────────────────────────────────────────────────

public class CreateOpportunityCommandHandler : IRequestHandler<CreateOpportunityCommand, OpportunityDto>
{
    private readonly IOpportunityRepository _opportunities;
    private readonly ILogger<CreateOpportunityCommandHandler> _logger;

    public CreateOpportunityCommandHandler(IOpportunityRepository opportunities, ILogger<CreateOpportunityCommandHandler> logger)
    {
        _opportunities = opportunities;
        _logger = logger;
    }

    public async Task<OpportunityDto> Handle(CreateOpportunityCommand request, CancellationToken cancellationToken)
    {
        var opportunity = Opportunity.Create(
            request.Title,
            request.ContactId,
            request.Value,
            request.ExpectedCloseDate,
            request.AssignedToUserId
        );

        await _opportunities.AddAsync(opportunity, cancellationToken);

        _logger.LogInformation("Opportunity {OpportunityId} created: {Title}", opportunity.Id, request.Title);

        return MapToDto(opportunity);
    }

    internal static OpportunityDto MapToDto(Opportunity o) => new(
        o.Id,
        o.OpportunityName,
        o.ContactId,
        ((OpportunityStage)o.OpportunityStage).ToString(),
        o.ProbabilityPercent,
        o.EstimatedValue,
        o.ExpectedCloseDate,
        o.AssignedToUserId,
        o.LostReason,
        o.CreatedAt,
        o.UpdatedAt
    );
}

public class AdvanceOpportunityStageCommandHandler : IRequestHandler<AdvanceOpportunityStageCommand, OpportunityDto>
{
    private readonly IOpportunityRepository _opportunities;
    private readonly ILogger<AdvanceOpportunityStageCommandHandler> _logger;

    public AdvanceOpportunityStageCommandHandler(IOpportunityRepository opportunities, ILogger<AdvanceOpportunityStageCommandHandler> logger)
    {
        _opportunities = opportunities;
        _logger = logger;
    }

    public async Task<OpportunityDto> Handle(AdvanceOpportunityStageCommand request, CancellationToken cancellationToken)
    {
        var opportunity = await _opportunities.GetByIdAsync(request.OpportunityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Opportunity {request.OpportunityId} not found.");

        opportunity.AdvanceStage();
        await _opportunities.UpdateAsync(opportunity, cancellationToken);

        _logger.LogInformation("Opportunity {OpportunityId} advanced to stage {Stage}", request.OpportunityId, opportunity.OpportunityStage);

        return CreateOpportunityCommandHandler.MapToDto(opportunity);
    }
}

public class WinOpportunityCommandHandler : IRequestHandler<WinOpportunityCommand, OpportunityDto>
{
    private readonly IOpportunityRepository _opportunities;
    private readonly ILogger<WinOpportunityCommandHandler> _logger;

    public WinOpportunityCommandHandler(IOpportunityRepository opportunities, ILogger<WinOpportunityCommandHandler> logger)
    {
        _opportunities = opportunities;
        _logger = logger;
    }

    public async Task<OpportunityDto> Handle(WinOpportunityCommand request, CancellationToken cancellationToken)
    {
        var opportunity = await _opportunities.GetByIdAsync(request.OpportunityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Opportunity {request.OpportunityId} not found.");

        opportunity.MarkWon();
        await _opportunities.UpdateAsync(opportunity, cancellationToken);

        _logger.LogInformation("Opportunity {OpportunityId} marked as won", request.OpportunityId);

        return CreateOpportunityCommandHandler.MapToDto(opportunity);
    }
}

public class LoseOpportunityCommandHandler : IRequestHandler<LoseOpportunityCommand, OpportunityDto>
{
    private readonly IOpportunityRepository _opportunities;
    private readonly ILogger<LoseOpportunityCommandHandler> _logger;

    public LoseOpportunityCommandHandler(IOpportunityRepository opportunities, ILogger<LoseOpportunityCommandHandler> logger)
    {
        _opportunities = opportunities;
        _logger = logger;
    }

    public async Task<OpportunityDto> Handle(LoseOpportunityCommand request, CancellationToken cancellationToken)
    {
        var opportunity = await _opportunities.GetByIdAsync(request.OpportunityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Opportunity {request.OpportunityId} not found.");

        opportunity.MarkLost(request.Reason);
        await _opportunities.UpdateAsync(opportunity, cancellationToken);

        _logger.LogInformation("Opportunity {OpportunityId} marked as lost", request.OpportunityId);

        return CreateOpportunityCommandHandler.MapToDto(opportunity);
    }
}

// ─── Case Handlers ───────────────────────────────────────────────────────────

public class OpenCaseCommandHandler : IRequestHandler<OpenCaseCommand, CaseDto>
{
    private readonly ICaseRepository _cases;
    private readonly ILogger<OpenCaseCommandHandler> _logger;

    public OpenCaseCommandHandler(ICaseRepository cases, ILogger<OpenCaseCommandHandler> logger)
    {
        _cases = cases;
        _logger = logger;
    }

    public async Task<CaseDto> Handle(OpenCaseCommand request, CancellationToken cancellationToken)
    {
        var caseEntity = Case.Create(request.Title, request.ContactId, request.Description, request.Priority);

        await _cases.AddAsync(caseEntity, cancellationToken);

        _logger.LogInformation("Case {CaseId} opened: {Title}", caseEntity.Id, request.Title);

        return MapToDto(caseEntity);
    }

    internal static CaseDto MapToDto(Case c) => new(
        c.Id,
        c.CaseNumber,
        c.CustomerId,
        c.CaseSubject,
        c.CaseDescription,
        ((CasePriority)c.CasePriority).ToString(),
        ((CaseStatus)c.CaseStatus).ToString(),
        c.ResolutionNote,
        c.AssignedToUserId,
        c.CreatedAt,
        c.UpdatedAt
    );
}

public class AssignCaseCommandHandler : IRequestHandler<AssignCaseCommand, CaseDto>
{
    private readonly ICaseRepository _cases;
    private readonly ILogger<AssignCaseCommandHandler> _logger;

    public AssignCaseCommandHandler(ICaseRepository cases, ILogger<AssignCaseCommandHandler> logger)
    {
        _cases = cases;
        _logger = logger;
    }

    public async Task<CaseDto> Handle(AssignCaseCommand request, CancellationToken cancellationToken)
    {
        var caseEntity = await _cases.GetByIdAsync(request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case {request.CaseId} not found.");

        caseEntity.Assign(request.AssignedToUserId);
        await _cases.UpdateAsync(caseEntity, cancellationToken);

        _logger.LogInformation("Case {CaseId} assigned to {UserId}", request.CaseId, request.AssignedToUserId);

        return OpenCaseCommandHandler.MapToDto(caseEntity);
    }
}

public class ResolveCaseCommandHandler : IRequestHandler<ResolveCaseCommand, CaseDto>
{
    private readonly ICaseRepository _cases;
    private readonly ILogger<ResolveCaseCommandHandler> _logger;

    public ResolveCaseCommandHandler(ICaseRepository cases, ILogger<ResolveCaseCommandHandler> logger)
    {
        _cases = cases;
        _logger = logger;
    }

    public async Task<CaseDto> Handle(ResolveCaseCommand request, CancellationToken cancellationToken)
    {
        var caseEntity = await _cases.GetByIdAsync(request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case {request.CaseId} not found.");

        caseEntity.Resolve(request.Resolution);
        await _cases.UpdateAsync(caseEntity, cancellationToken);

        _logger.LogInformation("Case {CaseId} resolved", request.CaseId);

        return OpenCaseCommandHandler.MapToDto(caseEntity);
    }
}

public class CloseCaseCommandHandler : IRequestHandler<CloseCaseCommand, CaseDto>
{
    private readonly ICaseRepository _cases;
    private readonly ILogger<CloseCaseCommandHandler> _logger;

    public CloseCaseCommandHandler(ICaseRepository cases, ILogger<CloseCaseCommandHandler> logger)
    {
        _cases = cases;
        _logger = logger;
    }

    public async Task<CaseDto> Handle(CloseCaseCommand request, CancellationToken cancellationToken)
    {
        var caseEntity = await _cases.GetByIdAsync(request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case {request.CaseId} not found.");

        caseEntity.Close();
        await _cases.UpdateAsync(caseEntity, cancellationToken);

        _logger.LogInformation("Case {CaseId} closed", request.CaseId);

        return OpenCaseCommandHandler.MapToDto(caseEntity);
    }
}
