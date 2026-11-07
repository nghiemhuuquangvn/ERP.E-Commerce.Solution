using ERP.NTF.Application.Commands;
using ERP.NTF.Application.EventHandlers;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace ERP.NTF.UnitTests.Notifications;

/// <summary>A newly hired employee gets a welcome e-mail; people brought in by the CSV import (MST, source "import") do not.</summary>
public class EmployeeWelcomeTests
{
    private static EmployeeHiredIntegrationEvent Hired(string source) => new()
    {
        EmployeeId = Guid.NewGuid(), Email = "an@example.com", FirstName = "An", LastName = "Nguyen", Department = "Sales", Source = source,
    };

    [Theory]
    [InlineData("hire", 1)]
    [InlineData("import", 0)]
    [InlineData("IMPORT", 0)]
    public async Task The_welcome_message_is_sent_only_for_a_real_hire(string source, int sent)
    {
        var mediator = new Mock<IMediator>();
        var handler = new EmployeeHiredIntegrationEventHandler(mediator.Object);

        await handler.Handle(Hired(source), default);

        mediator.Verify(m => m.Send(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(sent));
    }

    [Fact]
    public void An_event_without_a_source_is_a_hire()
    {
        new EmployeeHiredIntegrationEvent().Source.Should().Be("hire");
    }
}
