namespace ERP.MST.Application.DTOs;

public record DepartmentDto(
    Guid Id,
    string DepartmentName,
    string Code,
    Guid? ManagerId,
    Guid? ParentDepartmentId,
    Guid? DivisionId,
    string? LanguageCode,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record PositionDto(
    Guid Id,
    string Title,
    string Code,
    string? Description,
    Guid? DepartmentId,
    bool IsActive,
    string? LanguageCode,
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
    Guid? PositionId,
    string EmploymentStatus,
    string EmploymentType,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UnitOfMeasureDto(
    Guid Id,
    string Code,
    string Name,
    string Symbol,
    string UnitType,
    Guid? BaseUnitId,
    decimal ConversionFactor,
    string? LanguageCode,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CountryDto(
    Guid Id,
    string Code,
    string Name,
    string? PhoneCode,
    string? LanguageCode,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ProvinceDto(
    Guid Id,
    Guid CountryId,
    string Code,
    string Name,
    string? LanguageCode,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record DistrictDto(
    Guid Id,
    Guid ProvinceId,
    string Code,
    string Name,
    string? LanguageCode,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record WardDto(
    Guid Id,
    Guid DistrictId,
    string Code,
    string Name,
    string? LanguageCode,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SupportedLanguageDto(
    Guid Id,
    string Code,
    string Name,
    string NativeName,
    bool IsDefault,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CompanyDto(
    Guid Id,
    string Name,
    string Code,
    string? TaxCode,
    string? LegalAddress,
    string? CurrencyCode,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record BranchDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string Code,
    string? Address,
    Guid? WardId,
    Guid? ManagerEmployeeId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record DivisionDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string Code,
    Guid? ParentDivisionId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record TeamDto(
    Guid Id,
    Guid DepartmentId,
    string Name,
    string Code,
    Guid? LeaderEmployeeId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record BusinessUnitDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string Code,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CostCenterDto(
    Guid Id,
    Guid CompanyId,
    Guid? DepartmentId,
    string Name,
    string Code,
    decimal BudgetAmount,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ProfitCenterDto(
    Guid Id,
    Guid CompanyId,
    Guid? BusinessUnitId,
    string Name,
    string Code,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record OrganizationCalendarDto(
    Guid Id,
    Guid CompanyId,
    int Year,
    string Name,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record HolidayDto(
    Guid Id,
    Guid CalendarId,
    DateTime Date,
    string Name,
    bool IsRecurringYearly,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record EmployeeTeamAssignmentDto(
    Guid Id,
    Guid EmployeeId,
    Guid TeamId,
    Guid? PositionId,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CurrencyDto(
    Guid Id,
    string Code,
    string Name,
    string Symbol,
    int DecimalPlaces,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ExchangeRateDto(
    Guid Id,
    Guid FromCurrencyId,
    Guid ToCurrencyId,
    decimal Rate,
    DateTime EffectiveDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record PaymentTermDto(
    Guid Id,
    string Code,
    string Name,
    int NetDays,
    decimal? DepositPercent,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

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
