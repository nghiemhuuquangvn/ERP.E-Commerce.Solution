using ERP.CRM.Application.Commands;
using ERP.CRM.Application.EventHandlers;
using ERP.CRM.Application.Handlers;
using ERP.CRM.Application.Interfaces;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Domain.Enums;
using ERP.CRM.Infrastructure.Persistence;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.CRM.UnitTests.Activities;

/// <summary>R82: Activities.CreatedBy is NOT NULL in the database; every activity insert failed because nothing set it.</summary>
public class ActivityAuthorTests
{
    private static readonly Guid Shop = Guid.NewGuid();

    private sealed class Tenant(Guid id) : ITenantProvider
    {
        public Guid? CurrentTenantId { get; } = id;
    }

    [Fact]
    public void An_activity_has_an_author_the_given_one_or_system()
    {
        Activity.Create(Guid.NewGuid(), ActivityType.Note, "x", createdBy: "user-1").CreatedBy.Should().Be("user-1");
        Activity.Create(Guid.NewGuid(), ActivityType.Note, "x").CreatedBy.Should().Be("system");
        Activity.Create(Guid.NewGuid(), ActivityType.Note, "x", createdBy: "  ").CreatedBy.Should().Be("system");
    }

    [Fact]
    public async Task The_create_command_hands_the_authenticated_user_to_the_activity()
    {
        var uow = new Mock<ICrmUnitOfWork>();
        var activities = new Mock<IActivityRepository>();
        Activity? added = null;
        activities.Setup(a => a.AddAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>())).Callback<Activity, CancellationToken>((a, _) => added = a).Returns(Task.CompletedTask);
        uow.SetupGet(u => u.Activities).Returns(activities.Object);
        var handler = new CreateActivityCommandHandler(uow.Object, NullLogger<CreateActivityCommandHandler>.Instance);

        await handler.Handle(new CreateActivityCommand(Guid.NewGuid(), (int)ActivityType.Note, "Called the customer", null, null, null, null, null, null, "user-42"), default);

        added!.CreatedBy.Should().Be("user-42");
    }

    [Fact]
    public async Task The_order_consumer_writes_its_note_authored_by_the_sales_service()
    {
        var contact = Contact.Create("Ada", "ada@shop.test", null, userId: Guid.NewGuid());
        var contacts = new Mock<IContactRepository>();
        contacts.Setup(c => c.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(contact);
        var activities = new Mock<IActivityRepository>();
        Activity? added = null;
        activities.Setup(a => a.AddAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>())).Callback<Activity, CancellationToken>((a, _) => added = a).Returns(Task.CompletedTask);
        var uow = new Mock<ICrmUnitOfWork>();
        var handler = new SalesOrderCreatedEventHandler(contacts.Object, activities.Object, uow.Object, NullLogger<SalesOrderCreatedEventHandler>.Instance);

        await handler.HandleAsync(new SalesOrderCreatedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), 10m), default);

        added!.CreatedBy.Should().Be("SalesOrderService");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_database_context_never_saves_a_new_row_without_an_author()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new CrmDbContext(options, new Tenant(Shop));
        var contact = Contact.Create("Ada", "ada@shop.test", null);
        contact.CreatedBy = null;
        db.Contacts.Add(contact);

        await db.SaveChangesAsync();

        (await db.Contacts.AsNoTracking().SingleAsync()).CreatedBy.Should().Be("system");
    }

    [Fact]
    public async Task Completing_twice_or_cancelling_a_completed_activity_is_a_business_rule_refusal()
    {
        var activity = Activity.Create(Guid.NewGuid(), ActivityType.Note, "x"); // logged now: already completed
        var activities = new Mock<IActivityRepository>();
        activities.Setup(a => a.GetByIdAsync(activity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
        var uow = new Mock<ICrmUnitOfWork>();
        uow.SetupGet(u => u.Activities).Returns(activities.Object);

        var complete = () => new CompleteActivityCommandHandler(uow.Object, NullLogger<CompleteActivityCommandHandler>.Instance).Handle(new CompleteActivityCommand(activity.Id, null), default);
        var cancel = () => new CancelActivityCommandHandler(uow.Object, NullLogger<CancelActivityCommandHandler>.Instance).Handle(new CancelActivityCommand(activity.Id), default);

        await complete.Should().ThrowAsync<BusinessRuleException>();
        await cancel.Should().ThrowAsync<BusinessRuleException>();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
