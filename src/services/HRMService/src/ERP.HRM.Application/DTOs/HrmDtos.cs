namespace ERP.HRM.Application.DTOs;

// Department/Employee/Headcount DTOs moved to SharedMasterDataService.

public record LeaveRequestDto(
    Guid Id,
    string LeaveRequestNumber,
    Guid EmployeeId,
    string LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    int DaysRequested,
    string LeaveStatus,
    string? Reason,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record PaySlipDto(
    Guid Id,
    string PaySlipNumber,
    Guid EmployeeId,
    int Year,
    int Month,
    string PayPeriod,
    decimal GrossSalary,
    decimal Deductions,
    decimal NetSalary,
    DateTime PaymentDate,
    string PaymentStatus,
    DateTime CreatedAt);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
