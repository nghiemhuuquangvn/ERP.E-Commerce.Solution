using ERP.CRM.Domain.Entities;
using ERP.CRM.Domain.Events;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace ERP.CRM.UnitTests.Events;

/// <summary>ROADMAP P1-6: the CRM events carry what their consumers need (Reporting: who and how much; Notification: whom to write to).</summary>
public class CrmEventTests
{
    [Fact]
    public void A_won_opportunity_names_its_contact_and_value()
    {
        var contact = Guid.NewGuid();
        var opportunity = Opportunity.Create("Big deal", contact, 250_000m, new DateTime(2026, 12, 1));
        opportunity.ClearDomainEvents();

        opportunity.MarkWon();

        var won = opportunity.DomainEvents.OfType<OpportunityWonEvent>().Single();
        won.OpportunityId.Should().Be(opportunity.Id);
        won.Value.Should().Be(250_000m);
        won.ContactId.Should().Be(contact);
        won.Name.Should().Be("Big deal");
    }

    [Fact]
    public void A_resolved_case_carries_the_contact_email_and_name_for_the_message()
    {
        var contact = Guid.NewGuid();
        var @case = Case.Create("Wrong item", contact, "Received the wrong size", 1);
        @case.ClearDomainEvents();

        @case.Resolve("Replaced", "an@example.com", "An Nguyen");

        var resolved = @case.DomainEvents.OfType<CaseResolvedEvent>().Single();
        resolved.CaseId.Should().Be(@case.Id);
        resolved.ContactId.Should().Be(contact);
        resolved.Title.Should().Be("Wrong item");
        resolved.ContactEmail.Should().Be("an@example.com");
        resolved.ContactName.Should().Be("An Nguyen");
    }

    [Fact]
    public void A_case_resolved_without_a_known_contact_still_resolves()
    {
        var @case = Case.Create("Late", Guid.NewGuid(), "Late delivery", 0);
        @case.ClearDomainEvents();

        @case.Resolve("Apologised");

        @case.DomainEvents.OfType<CaseResolvedEvent>().Single().ContactEmail.Should().BeNull();
    }

    [Fact]
    public void The_events_still_deserialize_from_the_old_payloads_without_the_new_fields()
    {
        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        var id = Guid.NewGuid();

        var won = JsonConvert.DeserializeObject<OpportunityWonEvent>($"{{\"OpportunityId\":\"{id}\",\"Value\":10.5}}", settings)!;
        var resolved = JsonConvert.DeserializeObject<CaseResolvedEvent>($"{{\"CaseId\":\"{id}\"}}", settings)!;

        won.ContactId.Should().Be(Guid.Empty);
        won.Name.Should().BeNullOrEmpty();
        resolved.ContactEmail.Should().BeNull();
        resolved.Title.Should().BeNullOrEmpty();
    }
}
