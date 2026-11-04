using ERP.HRM.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.HRM.Application.Queries;

/// <summary>
/// What the leave request list can be narrowed by (Admin web and mobile app). Applied inside the database query, before paging.
/// <c>From</c> / <c>To</c> are calendar days; a request matches when its leave period touches that range (both ends inclusive).
/// <c>Search</c> is part of the request number.
/// </summary>
public sealed record LeaveRequestFilter(Guid? EmployeeId = null, int? Status = null, DateOnly? From = null, DateOnly? To = null, string? Search = null)
{
    public IQueryable<LeaveRequest> Apply(IQueryable<LeaveRequest> requests)
    {
        if (From.HasValue && To.HasValue && From > To)
            throw new BusinessRuleException("HRM", "The start date is after the end date.");

        requests = requests.Where(lr => !lr.IsDeleted);
        if (EmployeeId.HasValue) requests = requests.Where(lr => lr.EmployeeId == EmployeeId.Value);
        if (Status.HasValue) requests = requests.Where(lr => lr.LeaveStatus == Status.Value);
        if (From.HasValue)
        {
            var from = From.Value.ToDateTime(TimeOnly.MinValue);
            requests = requests.Where(lr => lr.EndDate >= from);
        }
        if (To.HasValue)
        {
            var before = To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            requests = requests.Where(lr => lr.StartDate < before);
        }
        var text = Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(text))
            requests = requests.Where(lr => lr.LeaveRequestNumber.ToLower().Contains(text));
        return requests;
    }
}
