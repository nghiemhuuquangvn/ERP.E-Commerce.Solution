namespace ERP.HRM.Application.DTOs;

public record DepartmentDto(
    Guid Id,
    string DepartmentName,
    string Code,
    Guid? ManagerId,
    Guid? ParentDepartmentId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record EmployeeDto(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string FullName,
    string? Email,
    string? Phone,
    DateTime DateOfHire,
    Guid DepartmentId,
    string EmploymentStatus,
    string EmploymentType,
    DateTime CreatedAt,
    DateTime UpdatedAt);

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

public record DepartmentHeadcountDto(
    Guid DepartmentId,
    string DepartmentName,
    string Code,
    int ActiveCount,
    int OnLeaveCount,
    int TerminatedCount,
    int TotalCount);

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
