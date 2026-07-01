namespace ERP.MST.Application.DTOs;

public record DepartmentDto(
    Guid Id,
    string DepartmentName,
    string Code,
    Guid? ManagerId,
    Guid? ParentDepartmentId,
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
