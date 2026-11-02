using ERP.CRM.Application.Commands;
using ERP.CRM.Application.Handlers;
using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.CRM.UnitTests.Leads;

/// <summary>R83: qualifying a lead without a contact answered a generic 500 (the opportunity was built with an empty contact id).</summary>
public class QualifyLeadTests
{
    private readonly Mock<ICrmUnitOfWork> _uow = new();
    private readonly Mock<ILeadRepository> _leads = new();
    private readonly Mock<IOpportunityRepository> _opportunities = new();
    private readonly List<Opportunity> _added = new();

    public QualifyLeadTests()
    {
        _uow.SetupGet(u => u.Leads).Returns(_leads.Object);
        _uow.SetupGet(u => u.Opportunities).Returns(_opportunities.Object);
        _opportunities.Setup(o => o.AddAsync(It.IsAny<Opportunity>(), It.IsAny<CancellationToken>()))
            .Callback<Opportunity, CancellationToken>((o, _) => _added.Add(o)).Returns(Task.CompletedTask);
    }

    private Lead Lead(Guid? contact, LeadStatus status = LeadStatus.New)
    {
        var lead = ERP.CRM.Domain.Entities.Lead.Create("Acme deal", 0);
        lead.ContactId = contact;
        lead.EstimatedValue = 5_000m;
        lead.LeadStatus = (int)status;
        _leads.Setup(l => l.GetByIdAsync(lead.Id, It.IsAny<CancellationToken>())).ReturnsAsync(lead);
        return lead;
    }

    private Task<ERP.CRM.Application.DTOs.LeadDto> Qualify(Lead lead) =>
        new QualifyLeadCommandHandler(_uow.Object, NullLogger<QualifyLeadCommandHandler>.Instance).Handle(new QualifyLeadCommand(lead.Id), default);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_lead_without_a_contact_is_refused_and_nothing_is_added_or_saved(bool emptyGuid)
    {
        var lead = Lead(emptyGuid ? Guid.Empty : null);

        var act = () => Qualify(lead);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("contact");
        _added.Should().BeEmpty();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        lead.LeadStatus.Should().Be((int)LeadStatus.New);
    }

    [Theory]
    [InlineData(LeadStatus.Qualified)]
    [InlineData(LeadStatus.Lost)]
    public async Task A_lead_that_is_already_qualified_or_lost_is_a_business_rule_refusal(LeadStatus status)
    {
        var lead = Lead(Guid.NewGuid(), status);

        var act = () => Qualify(lead);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _added.Should().BeEmpty();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(LeadStatus.New)]
    [InlineData(LeadStatus.Contacted)]
    public async Task A_new_or_contacted_lead_with_a_contact_becomes_an_opportunity_for_that_contact(LeadStatus status)
    {
        var contactId = Guid.NewGuid();
        var lead = Lead(contactId, status);

        await Qualify(lead);

        _added.Should().ContainSingle().Which.ContactId.Should().Be(contactId);
        lead.LeadStatus.Should().Be((int)LeadStatus.Qualified);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
