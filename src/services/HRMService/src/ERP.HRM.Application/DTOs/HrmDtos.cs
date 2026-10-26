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
    decimal OvertimeHours,
    decimal OvertimePay,
    decimal NetSalary,
    DateTime PaymentDate,
    string PaymentStatus,
    DateTime CreatedAt,
    decimal BaseSalary = 0m,
    decimal Allowances = 0m,
    decimal WorkedDays = 0m,
    decimal StandardDays = 0m,
    decimal EmployeeSocial = 0m,
    decimal EmployeeHealth = 0m,
    decimal EmployeeUnemployment = 0m,
    decimal EmployerSocial = 0m,
    decimal EmployerHealth = 0m,
    decimal EmployerUnemployment = 0m,
    decimal TaxableIncome = 0m,
    decimal PersonalIncomeTax = 0m,
    int DependentCount = 0);

public record AttendanceDto(
    Guid Id,
    Guid EmployeeId,
    DateTime Date,
    DateTime? CheckInTime,
    DateTime? CheckOutTime,
    decimal TotalHours,
    decimal OvertimeHours,
    string Status,
    Guid? WorkShiftId,
    string? Notes,
    DateTime CreatedAt);

public record WorkShiftDto(
    Guid Id,
    string Name,
    string StartTime,
    string EndTime,
    decimal ShiftHours,
    bool IsActive,
    DateTime CreatedAt);

public record EmployeeSnapshotDto(
    Guid Id,
    string EmployeeNumber,
    string FullName,
    string? Email,
    string EmploymentStatus,
    Guid DepartmentId);

public record AttendanceSummaryDto(
    Guid EmployeeId,
    int Year,
    int Month,
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int LeaveDays,
    decimal TotalHours,
    decimal TotalOvertimeHours);

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
