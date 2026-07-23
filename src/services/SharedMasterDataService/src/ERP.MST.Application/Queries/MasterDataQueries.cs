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

// ─── Company Queries ────────────────────────────────────────────────────────

public record GetCompanyQuery(Guid CompanyId) : IRequest<CompanyDto?>;

public record ListCompaniesQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<CompanyDto>>;

// ─── Branch Queries ─────────────────────────────────────────────────────────

public record GetBranchQuery(Guid BranchId) : IRequest<BranchDto?>;

public record ListBranchesQuery(
    Guid? CompanyId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<BranchDto>>;

// ─── Division Queries ───────────────────────────────────────────────────────

public record GetDivisionQuery(Guid DivisionId) : IRequest<DivisionDto?>;

public record ListDivisionsQuery(
    Guid? BranchId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<DivisionDto>>;

// ─── Team Queries ───────────────────────────────────────────────────────────

public record GetTeamQuery(Guid TeamId) : IRequest<TeamDto?>;

public record ListTeamsQuery(
    Guid? DepartmentId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<TeamDto>>;

// ─── Business Unit Queries ──────────────────────────────────────────────────

public record GetBusinessUnitQuery(Guid BusinessUnitId) : IRequest<BusinessUnitDto?>;

public record ListBusinessUnitsQuery(
    Guid? CompanyId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<BusinessUnitDto>>;

// ─── Cost Center Queries ────────────────────────────────────────────────────

public record GetCostCenterQuery(Guid CostCenterId) : IRequest<CostCenterDto?>;

public record ListCostCentersQuery(
    Guid? CompanyId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<CostCenterDto>>;

// ─── Profit Center Queries ──────────────────────────────────────────────────

public record GetProfitCenterQuery(Guid ProfitCenterId) : IRequest<ProfitCenterDto?>;

public record ListProfitCentersQuery(
    Guid? CompanyId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ProfitCenterDto>>;

// ─── Organization Calendar Queries ──────────────────────────────────────────

public record GetOrganizationCalendarQuery(Guid CalendarId) : IRequest<OrganizationCalendarDto?>;

public record ListOrganizationCalendarsQuery(
    Guid? CompanyId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<OrganizationCalendarDto>>;

// ─── Holiday Queries ─────────────────────────────────────────────────────────

public record GetHolidayQuery(Guid HolidayId) : IRequest<HolidayDto?>;

public record ListHolidaysQuery(
    Guid CalendarId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<HolidayDto>>;

// ─── Employee Team Assignment Queries ───────────────────────────────────────

public record GetEmployeeTeamAssignmentQuery(Guid AssignmentId) : IRequest<EmployeeTeamAssignmentDto?>;

public record ListEmployeeTeamAssignmentsQuery(
    Guid EmployeeId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<EmployeeTeamAssignmentDto>>;
