using ERP.HRM.Application.Handlers;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Application.Queries;
using ERP.HRM.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.HRM.UnitTests.Leave;

/// <summary>The leave request list: employee, status, day range (a request counts when its leave period touches the range) and number search are part of the paged query.</summary>
public class LeaveRequestListTests
{
    private static LeaveRequest Leave(string number, string start, string end, int status = 0, Guid? employee = null, bool deleted = false)
    {
        var request = new LeaveRequest { Id = Guid.NewGuid(), LeaveRequestNumber = number, EmployeeId = employee ?? Guid.NewGuid(), StartDate = DateTime.Parse(start), EndDate = DateTime.Parse(end), LeaveStatus = status };
        if (deleted) typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.IsDeleted))!.SetValue(request, true);
        return request;
    }

    private static List<LeaveRequest> Requests() => new()
    {
        Leave("LV-001", "2026-10-28", "2026-10-30"),
        Leave("LV-002", "2026-10-30", "2026-11-02"),
        Leave("LV-003", "2026-11-10", "2026-11-12", status: 1),
        Leave("LV-004", "2026-11-29", "2026-12-03"),
        Leave("LV-005", "2026-12-05", "2026-12-06"),
        Leave("LV-GONE", "2026-11-10", "2026-11-11", deleted: true),
    };

    private static List<string> Numbers(LeaveRequestFilter f) => f.Apply(Requests().AsQueryable()).Select(r => r.LeaveRequestNumber).OrderBy(n => n).ToList();

    [Fact]
    public void A_request_matches_when_its_leave_period_touches_the_range()
    {
        Numbers(new LeaveRequestFilter(From: new DateOnly(2026, 11, 1), To: new DateOnly(2026, 11, 30))).Should().Equal("LV-002", "LV-003", "LV-004");
    }

    [Fact]
    public void Both_end_days_count_and_a_one_day_range_works()
    {
        Numbers(new LeaveRequestFilter(From: new DateOnly(2026, 10, 30), To: new DateOnly(2026, 10, 30))).Should().Equal("LV-001", "LV-002");
        Numbers(new LeaveRequestFilter(From: new DateOnly(2026, 12, 3), To: new DateOnly(2026, 12, 4))).Should().Equal("LV-004");
        Numbers(new LeaveRequestFilter(From: new DateOnly(2026, 12, 7))).Should().BeEmpty();
        Numbers(new LeaveRequestFilter(To: new DateOnly(2026, 10, 27))).Should().BeEmpty();
    }

    [Fact]
    public void Status_search_and_employee_narrow_the_list_and_deleted_rows_never_show()
    {
        Numbers(new LeaveRequestFilter()).Should().HaveCount(5);
        Numbers(new LeaveRequestFilter(Status: 1)).Should().Equal("LV-003");
        Numbers(new LeaveRequestFilter(Search: " lv-005 ")).Should().Equal("LV-005");
        Numbers(new LeaveRequestFilter(EmployeeId: Guid.NewGuid())).Should().BeEmpty();
    }

    [Fact]
    public void A_start_after_the_end_is_refused()
    {
        var act = () => Numbers(new LeaveRequestFilter(From: new DateOnly(2026, 12, 1), To: new DateOnly(2026, 11, 1)));
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public async Task The_handler_passes_the_filter_and_clamps_the_page()
    {
        var repo = new Mock<ILeaveRequestRepository>();
        LeaveRequestFilter? seen = null;
        int seenPage = 0, seenSize = 0;
        repo.Setup(r => r.ListAsync(It.IsAny<LeaveRequestFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveRequestFilter, int, int, CancellationToken>((f, p, z, _) => { seen = f; seenPage = p; seenSize = z; })
            .ReturnsAsync((new List<LeaveRequest>(), 0));

        var result = await new ListLeaveRequestsQueryHandler(repo.Object).Handle(
            new ListLeaveRequestsQuery(null, 0, 0, 900, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "LV-1"), CancellationToken.None);

        seen.Should().Be(new LeaveRequestFilter(null, 0, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "LV-1"));
        (seenPage, seenSize).Should().Be((1, 100));
        result.TotalCount.Should().Be(0);
    }
}
