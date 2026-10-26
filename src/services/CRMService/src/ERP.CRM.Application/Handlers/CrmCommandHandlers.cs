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
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<CreateContactCommandHandler> _logger;

    public CreateContactCommandHandler(ICrmUnitOfWork uow, ILogger<CreateContactCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<ContactDto> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = Contact.Create(request.Name, request.Email, request.Phone, request.UserId);
        contact.Company = request.Company;
        contact.ContactSource = request.ContactSource;

        await _uow.Contacts.AddAsync(contact, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

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
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<UpdateContactCommandHandler> _logger;

    public UpdateContactCommandHandler(ICrmUnitOfWork uow, ILogger<UpdateContactCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<ContactDto> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await _uow.Contacts.GetByIdAsync(request.ContactId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contact {request.ContactId} not found.");

        contact.Update(request.Name, request.Email, request.Phone, request.Company);
        await _uow.Contacts.UpdateAsync(contact, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contact {ContactId} updated", request.ContactId);

        return CreateContactCommandHandler.MapToDto(contact);
    }
}

// ─── Lead Handlers ───────────────────────────────────────────────────────────

public class CreateLeadCommandHandler : IRequestHandler<CreateLeadCommand, LeadDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<CreateLeadCommandHandler> _logger;

    public CreateLeadCommandHandler(ICrmUnitOfWork uow, ILogger<CreateLeadCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<LeadDto> Handle(CreateLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = Lead.Create(request.LeadName, request.LeadSource);
        lead.EstimatedValue = request.EstimatedValue;
        lead.ContactId = request.ContactId;

        await _uow.Leads.AddAsync(lead, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

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
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<QualifyLeadCommandHandler> _logger;

    public QualifyLeadCommandHandler(ICrmUnitOfWork uow, ILogger<QualifyLeadCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<LeadDto> Handle(QualifyLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = await _uow.Leads.GetByIdAsync(request.LeadId, cancellationToken)
            ?? throw new KeyNotFoundException($"Lead {request.LeadId} not found.");

        // Create an opportunity from this lead
        var opportunity = Opportunity.Create(
            lead.LeadName,
            lead.ContactId ?? Guid.Empty,
            lead.EstimatedValue ?? 0,
            DateTime.UtcNow.AddMonths(3)
        );

        await _uow.Opportunities.AddAsync(opportunity, cancellationToken);

        lead.Qualify(opportunity.Id);
        await _uow.Leads.UpdateAsync(lead, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Lead {LeadId} qualified, opportunity {OpportunityId} created", lead.Id, opportunity.Id);

        return CreateLeadCommandHandler.MapToDto(lead);
    }
}

public class ContactLeadCommandHandler : IRequestHandler<ContactLeadCommand, LeadDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<ContactLeadCommandHandler> _logger;

    public ContactLeadCommandHandler(ICrmUnitOfWork uow, ILogger<ContactLeadCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<LeadDto> Handle(ContactLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = await _uow.Leads.GetByIdAsync(request.LeadId, cancellationToken)
            ?? throw new KeyNotFoundException($"Lead {request.LeadId} not found.");

        lead.Contact();
        await _uow.Leads.UpdateAsync(lead, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Lead {LeadId} marked as contacted", request.LeadId);

        return CreateLeadCommandHandler.MapToDto(lead);
    }
}

public class LoseLeadCommandHandler : IRequestHandler<LoseLeadCommand, LeadDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<LoseLeadCommandHandler> _logger;

    public LoseLeadCommandHandler(ICrmUnitOfWork uow, ILogger<LoseLeadCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<LeadDto> Handle(LoseLeadCommand request, CancellationToken cancellationToken)
    {
        var lead = await _uow.Leads.GetByIdAsync(request.LeadId, cancellationToken)
            ?? throw new KeyNotFoundException($"Lead {request.LeadId} not found.");

        lead.MarkLost(request.Reason);
        await _uow.Leads.UpdateAsync(lead, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Lead {LeadId} marked as lost", request.LeadId);

        return CreateLeadCommandHandler.MapToDto(lead);
    }
}

// ─── Opportunity Handlers ─────────────────────────────────────────────────────

public class CreateOpportunityCommandHandler : IRequestHandler<CreateOpportunityCommand, OpportunityDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<CreateOpportunityCommandHandler> _logger;

    public CreateOpportunityCommandHandler(ICrmUnitOfWork uow, ILogger<CreateOpportunityCommandHandler> logger)
    {
        _uow = uow;
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

        await _uow.Opportunities.AddAsync(opportunity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

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
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<AdvanceOpportunityStageCommandHandler> _logger;

    public AdvanceOpportunityStageCommandHandler(ICrmUnitOfWork uow, ILogger<AdvanceOpportunityStageCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<OpportunityDto> Handle(AdvanceOpportunityStageCommand request, CancellationToken cancellationToken)
    {
        var opportunity = await _uow.Opportunities.GetByIdAsync(request.OpportunityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Opportunity {request.OpportunityId} not found.");

        opportunity.AdvanceStage();
        await _uow.Opportunities.UpdateAsync(opportunity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Opportunity {OpportunityId} advanced to stage {Stage}", request.OpportunityId, opportunity.OpportunityStage);

        return CreateOpportunityCommandHandler.MapToDto(opportunity);
    }
}

public class WinOpportunityCommandHandler : IRequestHandler<WinOpportunityCommand, OpportunityDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<WinOpportunityCommandHandler> _logger;

    public WinOpportunityCommandHandler(ICrmUnitOfWork uow, ILogger<WinOpportunityCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<OpportunityDto> Handle(WinOpportunityCommand request, CancellationToken cancellationToken)
    {
        var opportunity = await _uow.Opportunities.GetByIdAsync(request.OpportunityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Opportunity {request.OpportunityId} not found.");

        opportunity.MarkWon();
        await _uow.Opportunities.UpdateAsync(opportunity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Opportunity {OpportunityId} marked as won", request.OpportunityId);

        return CreateOpportunityCommandHandler.MapToDto(opportunity);
    }
}

public class LoseOpportunityCommandHandler : IRequestHandler<LoseOpportunityCommand, OpportunityDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<LoseOpportunityCommandHandler> _logger;

    public LoseOpportunityCommandHandler(ICrmUnitOfWork uow, ILogger<LoseOpportunityCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<OpportunityDto> Handle(LoseOpportunityCommand request, CancellationToken cancellationToken)
    {
        var opportunity = await _uow.Opportunities.GetByIdAsync(request.OpportunityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Opportunity {request.OpportunityId} not found.");

        opportunity.MarkLost(request.Reason);
        await _uow.Opportunities.UpdateAsync(opportunity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Opportunity {OpportunityId} marked as lost", request.OpportunityId);

        return CreateOpportunityCommandHandler.MapToDto(opportunity);
    }
}

// ─── Case Handlers ───────────────────────────────────────────────────────────

public class OpenCaseCommandHandler : IRequestHandler<OpenCaseCommand, CaseDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<OpenCaseCommandHandler> _logger;

    public OpenCaseCommandHandler(ICrmUnitOfWork uow, ILogger<OpenCaseCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<CaseDto> Handle(OpenCaseCommand request, CancellationToken cancellationToken)
    {
        var caseEntity = Case.Create(request.Title, request.ContactId, request.Description, request.Priority);

        await _uow.Cases.AddAsync(caseEntity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

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
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<AssignCaseCommandHandler> _logger;

    public AssignCaseCommandHandler(ICrmUnitOfWork uow, ILogger<AssignCaseCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<CaseDto> Handle(AssignCaseCommand request, CancellationToken cancellationToken)
    {
        var caseEntity = await _uow.Cases.GetByIdAsync(request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case {request.CaseId} not found.");

        caseEntity.Assign(request.AssignedToUserId);
        await _uow.Cases.UpdateAsync(caseEntity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Case {CaseId} assigned to {UserId}", request.CaseId, request.AssignedToUserId);

        return OpenCaseCommandHandler.MapToDto(caseEntity);
    }
}

public class ResolveCaseCommandHandler : IRequestHandler<ResolveCaseCommand, CaseDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<ResolveCaseCommandHandler> _logger;

    public ResolveCaseCommandHandler(ICrmUnitOfWork uow, ILogger<ResolveCaseCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<CaseDto> Handle(ResolveCaseCommand request, CancellationToken cancellationToken)
    {
        var caseEntity = await _uow.Cases.GetByIdAsync(request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case {request.CaseId} not found.");

        // The customer's e-mail and name go with the event so Notification can tell them the case is resolved.
        var contact = await _uow.Contacts.GetByIdAsync(caseEntity.CustomerId, cancellationToken);
        caseEntity.Resolve(request.Resolution, contact?.Email, contact?.Name);
        await _uow.Cases.UpdateAsync(caseEntity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Case {CaseId} resolved", request.CaseId);

        return OpenCaseCommandHandler.MapToDto(caseEntity);
    }
}

public class CloseCaseCommandHandler : IRequestHandler<CloseCaseCommand, CaseDto>
{
    private readonly ICrmUnitOfWork _uow;
    private readonly ILogger<CloseCaseCommandHandler> _logger;

    public CloseCaseCommandHandler(ICrmUnitOfWork uow, ILogger<CloseCaseCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<CaseDto> Handle(CloseCaseCommand request, CancellationToken cancellationToken)
    {
        var caseEntity = await _uow.Cases.GetByIdAsync(request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case {request.CaseId} not found.");

        caseEntity.Close();
        await _uow.Cases.UpdateAsync(caseEntity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Case {CaseId} closed", request.CaseId);

        return OpenCaseCommandHandler.MapToDto(caseEntity);
    }
}
