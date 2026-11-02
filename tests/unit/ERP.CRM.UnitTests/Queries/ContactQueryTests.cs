using ERP.CRM.Application.Handlers;
using ERP.CRM.Application.Interfaces;
using ERP.CRM.Application.Queries;
using ERP.CRM.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.CRM.UnitTests.Queries;

/// <summary>R84: contact reads go to the (tenant-filtered) repository every time; nothing is served from a cache shared by all shops.</summary>
public class ContactQueryTests
{
    [Fact]
    public async Task ListContacts_asks_the_repository_on_every_call_so_a_new_contact_shows_at_once_and_no_shop_sees_another_shops_page()
    {
        var contacts = new Mock<IContactRepository>();
        var first = new List<Contact>();
        contacts.SetupSequence(c => c.GetPagedAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((first, 0))
            .ReturnsAsync((new List<Contact> { Contact.Create("Ada Lovelace", "ada@shop.test", null) }, 1));
        var handler = new ListContactsQueryHandler(contacts.Object);
        var query = new ListContactsQuery(null, 1, 20);

        (await handler.Handle(query, default)).TotalCount.Should().Be(0);
        (await handler.Handle(query, default)).TotalCount.Should().Be(1);
        contacts.Verify(c => c.GetPagedAsync(null, 1, 20, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetContact_reads_the_repository_every_time_and_answers_null_for_an_id_of_another_shop()
    {
        var id = Guid.NewGuid();
        var contacts = new Mock<IContactRepository>();
        contacts.SetupSequence(c => c.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contact.Create("Ada Lovelace", "ada@shop.test", null))
            .ReturnsAsync((Contact?)null); // the tenant filter hides it from the other shop
        var handler = new GetContactQueryHandler(contacts.Object);

        (await handler.Handle(new GetContactQuery(id), default)).Should().NotBeNull();
        (await handler.Handle(new GetContactQuery(id), default)).Should().BeNull();
    }
}
