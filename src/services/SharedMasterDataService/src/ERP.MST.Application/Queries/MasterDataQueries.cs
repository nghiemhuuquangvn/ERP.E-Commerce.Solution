using ERP.MST.Application.DTOs;
using MediatR;

namespace ERP.MST.Application.Queries;

// ─── Department Queries ────────────────────────────────────────────────────

public record GetDepartmentQuery(Guid DepartmentId, string? LanguageCode = null) : IRequest<DepartmentDto?>;

public record ListDepartmentsQuery(
    string? LanguageCode = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<DepartmentDto>>;

// ─── Position Queries ───────────────────────────────────────────────────────

public record GetPositionQuery(Guid PositionId, string? LanguageCode = null) : IRequest<PositionDto?>;

public record ListPositionsQuery(
    Guid? DepartmentId = null,
    string? LanguageCode = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PositionDto>>;

// ─── Employee Queries ──────────────────────────────────────────────────────

public record GetEmployeeQuery(Guid EmployeeId) : IRequest<EmployeeDto?>;

public record ListEmployeesQuery(
    Guid? DepartmentId = null,
    int? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<EmployeeDto>>;

// ─── Unit Of Measure Queries ────────────────────────────────────────────────

public record GetUnitOfMeasureQuery(Guid UnitOfMeasureId, string? LanguageCode = null) : IRequest<UnitOfMeasureDto?>;

public record ListUnitsOfMeasureQuery(
    string? LanguageCode = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<UnitOfMeasureDto>>;

// ─── Geography Queries ──────────────────────────────────────────────────────

public record GetCountryQuery(Guid CountryId, string? LanguageCode = null) : IRequest<CountryDto?>;

public record ListCountriesQuery(
    string? LanguageCode = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<CountryDto>>;

public record GetProvinceQuery(Guid ProvinceId, string? LanguageCode = null) : IRequest<ProvinceDto?>;

public record ListProvincesQuery(
    Guid CountryId,
    string? LanguageCode = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ProvinceDto>>;

public record GetDistrictQuery(Guid DistrictId, string? LanguageCode = null) : IRequest<DistrictDto?>;

public record ListDistrictsQuery(
    Guid ProvinceId,
    string? LanguageCode = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<DistrictDto>>;

public record GetWardQuery(Guid WardId, string? LanguageCode = null) : IRequest<WardDto?>;

public record ListWardsQuery(
    Guid DistrictId,
    string? LanguageCode = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<WardDto>>;

// ─── Language Queries ───────────────────────────────────────────────────────

public record GetLanguageQuery(Guid LanguageId) : IRequest<SupportedLanguageDto?>;

public record ListLanguagesQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<SupportedLanguageDto>>;
