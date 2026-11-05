using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using ERP.HRM.Domain.Events;
using FluentAssertions;
using Xunit;

namespace ERP.HRM.UnitTests.Leave;

/// <summary>A new leave request tells the approvers (Notification turns the event into a bell message for the holders of hrm:leave:manage).</summary>
public class LeaveSubmittedEventTests
{
    [Fact]
    public void Creating_a_request_raises_one_event_with_its_number_employee_period_and_reason()
    {
        var employee = Guid.NewGuid();

        var request = LeaveRequest.Create("LR-501", employee, (int)LeaveType.Annual, new DateTime(2026, 12, 7), new DateTime(2026, 12, 9), 3, "Family trip");

        var raised = request.DomainEvents.OfType<LeaveRequestSubmittedEvent>().Should().ContainSingle().Subject;
        raised.LeaveRequestId.Should().Be(request.Id);
        raised.LeaveRequestNumber.Should().Be("LR-501");
        raised.EmployeeId.Should().Be(employee);
        raised.StartDate.Should().Be(new DateTime(2026, 12, 7));
        raised.EndDate.Should().Be(new DateTime(2026, 12, 9));
        raised.DaysRequested.Should().Be(3);
        raised.Reason.Should().Be("Family trip");
    }

    [Fact]
    public void The_event_is_a_tenant_event()
    {
        var request = LeaveRequest.Create("LR-502", Guid.NewGuid(), (int)LeaveType.Sick, new DateTime(2026, 12, 7), new DateTime(2026, 12, 7), 1);

        request.DomainEvents.OfType<LeaveRequestSubmittedEvent>().Single().Should().BeAssignableTo<ERP.Shared.Core.Domain.ITenantEvent>();
    }

    [Fact]
    public void Approving_does_not_raise_the_submitted_event_again()
    {
        var request = LeaveRequest.Create("LR-503", Guid.NewGuid(), (int)LeaveType.Annual, new DateTime(2026, 12, 7), new DateTime(2026, 12, 8), 2);
        request.ClearDomainEvents();

        request.Approve();

        request.DomainEvents.OfType<LeaveRequestSubmittedEvent>().Should().BeEmpty();
        request.DomainEvents.OfType<LeaveApprovedEvent>().Should().ContainSingle();
    }
}
