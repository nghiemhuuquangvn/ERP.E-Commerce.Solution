using ERP.MST.Application.DTOs;
using ERP.MST.Application.Interfaces;
using ERP.MST.Application.Queries;
using ERP.MST.Domain.Entities;
using MediatR;

namespace ERP.MST.Application.Handlers;

// ─── Department Query Handlers ─────────────────────────────────────────────

public class GetDepartmentQueryHandler : IRequestHandler<GetDepartmentQuery, DepartmentDto?>
{
    private readonly IDepartmentRepository _departmentRepository;

    public GetDepartmentQueryHandler(IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public async Task<DepartmentDto?> Handle(GetDepartmentQuery request, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken);
        if (department is null)
            return null;

        if (string.IsNullOrEmpty(request.LanguageCode))
            return CreateDepartmentCommandHandler.MapToDto(department);

        var translation = await _departmentRepository.GetTranslationAsync(department.Id, request.LanguageCode, cancellationToken);
        if (translation is null)
            return CreateDepartmentCommandHandler.MapToDto(department);

        return new DepartmentDto(
            department.Id,
            translation.Name,
            department.Code,
            department.ManagerId,
            department.ParentDepartmentId,
            department.DivisionId,
            request.LanguageCode,
            department.CreatedAt,
            department.UpdatedAt);
    }
}

public class ListDepartmentsQueryHandler : IRequestHandler<ListDepartmentsQuery, PagedResult<DepartmentDto>>
{
    private readonly IDepartmentRepository _departmentRepository;

    public ListDepartmentsQueryHandler(IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public async Task<PagedResult<DepartmentDto>> Handle(ListDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _departmentRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        var dtos = new List<DepartmentDto>(items.Count);
        foreach (var department in items)
            dtos.Add(await MapWithTranslationAsync(department, request.LanguageCode, cancellationToken));

        return new PagedResult<DepartmentDto>(dtos, totalCount, request.Page, request.PageSize);
    }

    private async Task<DepartmentDto> MapWithTranslationAsync(Department department, string? languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(languageCode))
            return CreateDepartmentCommandHandler.MapToDto(department);

        var translation = await _departmentRepository.GetTranslationAsync(department.Id, languageCode, cancellationToken);
        if (translation is null)
            return CreateDepartmentCommandHandler.MapToDto(department);

        return new DepartmentDto(
            department.Id,
            translation.Name,
            department.Code,
            department.ManagerId,
            department.ParentDepartmentId,
            department.DivisionId,
            languageCode,
            department.CreatedAt,
            department.UpdatedAt);
    }
}

// ─── Position Query Handlers ───────────────────────────────────────────────

public class GetPositionQueryHandler : IRequestHandler<GetPositionQuery, PositionDto?>
{
    private readonly IPositionRepository _positionRepository;

    public GetPositionQueryHandler(IPositionRepository positionRepository)
    {
        _positionRepository = positionRepository;
    }

    public async Task<PositionDto?> Handle(GetPositionQuery request, CancellationToken cancellationToken)
    {
        var position = await _positionRepository.GetByIdAsync(request.PositionId, cancellationToken);
        if (position is null)
            return null;

        if (string.IsNullOrEmpty(request.LanguageCode))
            return CreatePositionCommandHandler.MapToDto(position);

        var translation = await _positionRepository.GetTranslationAsync(position.Id, request.LanguageCode, cancellationToken);
        if (translation is null)
            return CreatePositionCommandHandler.MapToDto(position);

        return new PositionDto(
            position.Id,
            translation.Title,
            position.Code,
            translation.Description ?? position.Description,
            position.DepartmentId,
            position.IsActive,
            request.LanguageCode,
            position.CreatedAt,
            position.UpdatedAt);
    }
}

public class ListPositionsQueryHandler : IRequestHandler<ListPositionsQuery, PagedResult<PositionDto>>
{
    private readonly IPositionRepository _positionRepository;

    public ListPositionsQueryHandler(IPositionRepository positionRepository)
    {
        _positionRepository = positionRepository;
    }

    public async Task<PagedResult<PositionDto>> Handle(ListPositionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _positionRepository.ListAsync(request.DepartmentId, request.Page, request.PageSize, cancellationToken);

        var dtos = new List<PositionDto>(items.Count);
        foreach (var position in items)
            dtos.Add(await MapWithTranslationAsync(position, request.LanguageCode, cancellationToken));

        return new PagedResult<PositionDto>(dtos, totalCount, request.Page, request.PageSize);
    }

    private async Task<PositionDto> MapWithTranslationAsync(Position position, string? languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(languageCode))
            return CreatePositionCommandHandler.MapToDto(position);

        var translation = await _positionRepository.GetTranslationAsync(position.Id, languageCode, cancellationToken);
        if (translation is null)
            return CreatePositionCommandHandler.MapToDto(position);

        return new PositionDto(
            position.Id,
            translation.Title,
            position.Code,
            translation.Description ?? position.Description,
            position.DepartmentId,
            position.IsActive,
            languageCode,
            position.CreatedAt,
            position.UpdatedAt);
    }
}

// ─── Employee Query Handlers ───────────────────────────────────────────────

public class GetEmployeeQueryHandler : IRequestHandler<GetEmployeeQuery, EmployeeDto?>
{
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeeQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<EmployeeDto?> Handle(GetEmployeeQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
        return employee is null ? null : HireEmployeeCommandHandler.MapToDto(employee);
    }
}

public class ListEmployeesQueryHandler : IRequestHandler<ListEmployeesQuery, PagedResult<EmployeeDto>>
{
    private readonly IEmployeeRepository _employeeRepository;

    public ListEmployeesQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<PagedResult<EmployeeDto>> Handle(ListEmployeesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _employeeRepository.ListAsync(
            request.DepartmentId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(HireEmployeeCommandHandler.MapToDto).ToList();
        return new PagedResult<EmployeeDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Unit Of Measure Query Handlers ────────────────────────────────────────

public class GetUnitOfMeasureQueryHandler : IRequestHandler<GetUnitOfMeasureQuery, UnitOfMeasureDto?>
{
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;

    public GetUnitOfMeasureQueryHandler(IUnitOfMeasureRepository unitOfMeasureRepository)
    {
        _unitOfMeasureRepository = unitOfMeasureRepository;
    }

    public async Task<UnitOfMeasureDto?> Handle(GetUnitOfMeasureQuery request, CancellationToken cancellationToken)
    {
        var unitOfMeasure = await _unitOfMeasureRepository.GetByIdAsync(request.UnitOfMeasureId, cancellationToken);
        if (unitOfMeasure is null)
            return null;

        if (string.IsNullOrEmpty(request.LanguageCode))
            return CreateUnitOfMeasureCommandHandler.MapToDto(unitOfMeasure);

        var translation = await _unitOfMeasureRepository.GetTranslationAsync(unitOfMeasure.Id, request.LanguageCode, cancellationToken);
        if (translation is null)
            return CreateUnitOfMeasureCommandHandler.MapToDto(unitOfMeasure);

        return new UnitOfMeasureDto(
            unitOfMeasure.Id,
            unitOfMeasure.Code,
            translation.Name,
            translation.Symbol,
            ((Domain.Enums.UnitType)unitOfMeasure.UnitType).ToString(),
            unitOfMeasure.BaseUnitId,
            unitOfMeasure.ConversionFactor,
            request.LanguageCode,
            unitOfMeasure.CreatedAt,
            unitOfMeasure.UpdatedAt);
    }
}

public class ListUnitsOfMeasureQueryHandler : IRequestHandler<ListUnitsOfMeasureQuery, PagedResult<UnitOfMeasureDto>>
{
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;

    public ListUnitsOfMeasureQueryHandler(IUnitOfMeasureRepository unitOfMeasureRepository)
    {
        _unitOfMeasureRepository = unitOfMeasureRepository;
    }

    public async Task<PagedResult<UnitOfMeasureDto>> Handle(ListUnitsOfMeasureQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfMeasureRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        var dtos = new List<UnitOfMeasureDto>(items.Count);
        foreach (var unitOfMeasure in items)
            dtos.Add(await MapWithTranslationAsync(unitOfMeasure, request.LanguageCode, cancellationToken));

        return new PagedResult<UnitOfMeasureDto>(dtos, totalCount, request.Page, request.PageSize);
    }

    private async Task<UnitOfMeasureDto> MapWithTranslationAsync(UnitOfMeasure unitOfMeasure, string? languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(languageCode))
            return CreateUnitOfMeasureCommandHandler.MapToDto(unitOfMeasure);

        var translation = await _unitOfMeasureRepository.GetTranslationAsync(unitOfMeasure.Id, languageCode, cancellationToken);
        if (translation is null)
            return CreateUnitOfMeasureCommandHandler.MapToDto(unitOfMeasure);

        return new UnitOfMeasureDto(
            unitOfMeasure.Id,
            unitOfMeasure.Code,
            translation.Name,
            translation.Symbol,
            ((Domain.Enums.UnitType)unitOfMeasure.UnitType).ToString(),
            unitOfMeasure.BaseUnitId,
            unitOfMeasure.ConversionFactor,
            languageCode,
            unitOfMeasure.CreatedAt,
            unitOfMeasure.UpdatedAt);
    }
}

// ─── Geography Query Handlers ───────────────────────────────────────────────

public class GetCountryQueryHandler : IRequestHandler<GetCountryQuery, CountryDto?>
{
    private readonly ICountryRepository _countryRepository;

    public GetCountryQueryHandler(ICountryRepository countryRepository)
    {
        _countryRepository = countryRepository;
    }

    public async Task<CountryDto?> Handle(GetCountryQuery request, CancellationToken cancellationToken)
    {
        var country = await _countryRepository.GetByIdAsync(request.CountryId, cancellationToken);
        if (country is null)
            return null;

        if (string.IsNullOrEmpty(request.LanguageCode))
            return CreateCountryCommandHandler.MapToDto(country);

        var translation = await _countryRepository.GetTranslationAsync(country.Id, request.LanguageCode, cancellationToken);
        if (translation is null)
            return CreateCountryCommandHandler.MapToDto(country);

        return new CountryDto(country.Id, country.Code, translation.Name, country.PhoneCode, request.LanguageCode, country.CreatedAt, country.UpdatedAt);
    }
}

public class ListCountriesQueryHandler : IRequestHandler<ListCountriesQuery, PagedResult<CountryDto>>
{
    private readonly ICountryRepository _countryRepository;

    public ListCountriesQueryHandler(ICountryRepository countryRepository)
    {
        _countryRepository = countryRepository;
    }

    public async Task<PagedResult<CountryDto>> Handle(ListCountriesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _countryRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        var dtos = new List<CountryDto>(items.Count);
        foreach (var country in items)
            dtos.Add(await MapWithTranslationAsync(country, request.LanguageCode, cancellationToken));

        return new PagedResult<CountryDto>(dtos, totalCount, request.Page, request.PageSize);
    }

    private async Task<CountryDto> MapWithTranslationAsync(Country country, string? languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(languageCode))
            return CreateCountryCommandHandler.MapToDto(country);

        var translation = await _countryRepository.GetTranslationAsync(country.Id, languageCode, cancellationToken);
        if (translation is null)
            return CreateCountryCommandHandler.MapToDto(country);

        return new CountryDto(country.Id, country.Code, translation.Name, country.PhoneCode, languageCode, country.CreatedAt, country.UpdatedAt);
    }
}

public class GetProvinceQueryHandler : IRequestHandler<GetProvinceQuery, ProvinceDto?>
{
    private readonly IProvinceRepository _provinceRepository;

    public GetProvinceQueryHandler(IProvinceRepository provinceRepository)
    {
        _provinceRepository = provinceRepository;
    }

    public async Task<ProvinceDto?> Handle(GetProvinceQuery request, CancellationToken cancellationToken)
    {
        var province = await _provinceRepository.GetByIdAsync(request.ProvinceId, cancellationToken);
        if (province is null)
            return null;

        if (string.IsNullOrEmpty(request.LanguageCode))
            return CreateProvinceCommandHandler.MapToDto(province);

        var translation = await _provinceRepository.GetTranslationAsync(province.Id, request.LanguageCode, cancellationToken);
        if (translation is null)
            return CreateProvinceCommandHandler.MapToDto(province);

        return new ProvinceDto(province.Id, province.CountryId, province.Code, translation.Name, request.LanguageCode, province.CreatedAt, province.UpdatedAt);
    }
}

public class ListProvincesQueryHandler : IRequestHandler<ListProvincesQuery, PagedResult<ProvinceDto>>
{
    private readonly IProvinceRepository _provinceRepository;

    public ListProvincesQueryHandler(IProvinceRepository provinceRepository)
    {
        _provinceRepository = provinceRepository;
    }

    public async Task<PagedResult<ProvinceDto>> Handle(ListProvincesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _provinceRepository.ListAsync(request.CountryId, request.Page, request.PageSize, cancellationToken);

        var dtos = new List<ProvinceDto>(items.Count);
        foreach (var province in items)
            dtos.Add(await MapWithTranslationAsync(province, request.LanguageCode, cancellationToken));

        return new PagedResult<ProvinceDto>(dtos, totalCount, request.Page, request.PageSize);
    }

    private async Task<ProvinceDto> MapWithTranslationAsync(Province province, string? languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(languageCode))
            return CreateProvinceCommandHandler.MapToDto(province);

        var translation = await _provinceRepository.GetTranslationAsync(province.Id, languageCode, cancellationToken);
        if (translation is null)
            return CreateProvinceCommandHandler.MapToDto(province);

        return new ProvinceDto(province.Id, province.CountryId, province.Code, translation.Name, languageCode, province.CreatedAt, province.UpdatedAt);
    }
}

public class GetDistrictQueryHandler : IRequestHandler<GetDistrictQuery, DistrictDto?>
{
    private readonly IDistrictRepository _districtRepository;

    public GetDistrictQueryHandler(IDistrictRepository districtRepository)
    {
        _districtRepository = districtRepository;
    }

    public async Task<DistrictDto?> Handle(GetDistrictQuery request, CancellationToken cancellationToken)
    {
        var district = await _districtRepository.GetByIdAsync(request.DistrictId, cancellationToken);
        if (district is null)
            return null;

        if (string.IsNullOrEmpty(request.LanguageCode))
            return CreateDistrictCommandHandler.MapToDto(district);

        var translation = await _districtRepository.GetTranslationAsync(district.Id, request.LanguageCode, cancellationToken);
        if (translation is null)
            return CreateDistrictCommandHandler.MapToDto(district);

        return new DistrictDto(district.Id, district.ProvinceId, district.Code, translation.Name, request.LanguageCode, district.CreatedAt, district.UpdatedAt);
    }
}

public class ListDistrictsQueryHandler : IRequestHandler<ListDistrictsQuery, PagedResult<DistrictDto>>
{
    private readonly IDistrictRepository _districtRepository;

    public ListDistrictsQueryHandler(IDistrictRepository districtRepository)
    {
        _districtRepository = districtRepository;
    }

    public async Task<PagedResult<DistrictDto>> Handle(ListDistrictsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _districtRepository.ListAsync(request.ProvinceId, request.Page, request.PageSize, cancellationToken);

        var dtos = new List<DistrictDto>(items.Count);
        foreach (var district in items)
            dtos.Add(await MapWithTranslationAsync(district, request.LanguageCode, cancellationToken));

        return new PagedResult<DistrictDto>(dtos, totalCount, request.Page, request.PageSize);
    }

    private async Task<DistrictDto> MapWithTranslationAsync(District district, string? languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(languageCode))
            return CreateDistrictCommandHandler.MapToDto(district);

        var translation = await _districtRepository.GetTranslationAsync(district.Id, languageCode, cancellationToken);
        if (translation is null)
            return CreateDistrictCommandHandler.MapToDto(district);

        return new DistrictDto(district.Id, district.ProvinceId, district.Code, translation.Name, languageCode, district.CreatedAt, district.UpdatedAt);
    }
}

public class GetWardQueryHandler : IRequestHandler<GetWardQuery, WardDto?>
{
    private readonly IWardRepository _wardRepository;

    public GetWardQueryHandler(IWardRepository wardRepository)
    {
        _wardRepository = wardRepository;
    }

    public async Task<WardDto?> Handle(GetWardQuery request, CancellationToken cancellationToken)
    {
        var ward = await _wardRepository.GetByIdAsync(request.WardId, cancellationToken);
        if (ward is null)
            return null;

        if (string.IsNullOrEmpty(request.LanguageCode))
            return CreateWardCommandHandler.MapToDto(ward);

        var translation = await _wardRepository.GetTranslationAsync(ward.Id, request.LanguageCode, cancellationToken);
        if (translation is null)
            return CreateWardCommandHandler.MapToDto(ward);

        return new WardDto(ward.Id, ward.DistrictId, ward.Code, translation.Name, request.LanguageCode, ward.CreatedAt, ward.UpdatedAt);
    }
}

public class ListWardsQueryHandler : IRequestHandler<ListWardsQuery, PagedResult<WardDto>>
{
    private readonly IWardRepository _wardRepository;

    public ListWardsQueryHandler(IWardRepository wardRepository)
    {
        _wardRepository = wardRepository;
    }

    public async Task<PagedResult<WardDto>> Handle(ListWardsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _wardRepository.ListAsync(request.DistrictId, request.Page, request.PageSize, cancellationToken);

        var dtos = new List<WardDto>(items.Count);
        foreach (var ward in items)
            dtos.Add(await MapWithTranslationAsync(ward, request.LanguageCode, cancellationToken));

        return new PagedResult<WardDto>(dtos, totalCount, request.Page, request.PageSize);
    }

    private async Task<WardDto> MapWithTranslationAsync(Ward ward, string? languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(languageCode))
            return CreateWardCommandHandler.MapToDto(ward);

        var translation = await _wardRepository.GetTranslationAsync(ward.Id, languageCode, cancellationToken);
        if (translation is null)
            return CreateWardCommandHandler.MapToDto(ward);

        return new WardDto(ward.Id, ward.DistrictId, ward.Code, translation.Name, languageCode, ward.CreatedAt, ward.UpdatedAt);
    }
}

// ─── Language Query Handlers ────────────────────────────────────────────────

public class GetLanguageQueryHandler : IRequestHandler<GetLanguageQuery, SupportedLanguageDto?>
{
    private readonly ISupportedLanguageRepository _languageRepository;

    public GetLanguageQueryHandler(ISupportedLanguageRepository languageRepository)
    {
        _languageRepository = languageRepository;
    }

    public async Task<SupportedLanguageDto?> Handle(GetLanguageQuery request, CancellationToken cancellationToken)
    {
        var language = await _languageRepository.GetByIdAsync(request.LanguageId, cancellationToken);
        return language is null ? null : CreateLanguageCommandHandler.MapToDto(language);
    }
}

public class ListLanguagesQueryHandler : IRequestHandler<ListLanguagesQuery, PagedResult<SupportedLanguageDto>>
{
    private readonly ISupportedLanguageRepository _languageRepository;

    public ListLanguagesQueryHandler(ISupportedLanguageRepository languageRepository)
    {
        _languageRepository = languageRepository;
    }

    public async Task<PagedResult<SupportedLanguageDto>> Handle(ListLanguagesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _languageRepository.ListAsync(request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateLanguageCommandHandler.MapToDto).ToList();
        return new PagedResult<SupportedLanguageDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Company Query Handlers ────────────────────────────────────────────────

public class GetCompanyQueryHandler : IRequestHandler<GetCompanyQuery, CompanyDto?>
{
    private readonly ICompanyRepository _companyRepository;

    public GetCompanyQueryHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<CompanyDto?> Handle(GetCompanyQuery request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);
        return company is null ? null : CreateCompanyCommandHandler.MapToDto(company);
    }
}

public class ListCompaniesQueryHandler : IRequestHandler<ListCompaniesQuery, PagedResult<CompanyDto>>
{
    private readonly ICompanyRepository _companyRepository;

    public ListCompaniesQueryHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<PagedResult<CompanyDto>> Handle(ListCompaniesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _companyRepository.ListAsync(request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateCompanyCommandHandler.MapToDto).ToList();
        return new PagedResult<CompanyDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Branch Query Handlers ─────────────────────────────────────────────────

public class GetBranchQueryHandler : IRequestHandler<GetBranchQuery, BranchDto?>
{
    private readonly IBranchRepository _branchRepository;

    public GetBranchQueryHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<BranchDto?> Handle(GetBranchQuery request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken);
        return branch is null ? null : CreateBranchCommandHandler.MapToDto(branch);
    }
}

public class ListBranchesQueryHandler : IRequestHandler<ListBranchesQuery, PagedResult<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;

    public ListBranchesQueryHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<PagedResult<BranchDto>> Handle(ListBranchesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _branchRepository.ListAsync(request.CompanyId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateBranchCommandHandler.MapToDto).ToList();
        return new PagedResult<BranchDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Division Query Handlers ───────────────────────────────────────────────

public class GetDivisionQueryHandler : IRequestHandler<GetDivisionQuery, DivisionDto?>
{
    private readonly IDivisionRepository _divisionRepository;

    public GetDivisionQueryHandler(IDivisionRepository divisionRepository)
    {
        _divisionRepository = divisionRepository;
    }

    public async Task<DivisionDto?> Handle(GetDivisionQuery request, CancellationToken cancellationToken)
    {
        var division = await _divisionRepository.GetByIdAsync(request.DivisionId, cancellationToken);
        return division is null ? null : CreateDivisionCommandHandler.MapToDto(division);
    }
}

public class ListDivisionsQueryHandler : IRequestHandler<ListDivisionsQuery, PagedResult<DivisionDto>>
{
    private readonly IDivisionRepository _divisionRepository;

    public ListDivisionsQueryHandler(IDivisionRepository divisionRepository)
    {
        _divisionRepository = divisionRepository;
    }

    public async Task<PagedResult<DivisionDto>> Handle(ListDivisionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _divisionRepository.ListAsync(request.BranchId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateDivisionCommandHandler.MapToDto).ToList();
        return new PagedResult<DivisionDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Team Query Handlers ───────────────────────────────────────────────────

public class GetTeamQueryHandler : IRequestHandler<GetTeamQuery, TeamDto?>
{
    private readonly ITeamRepository _teamRepository;

    public GetTeamQueryHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<TeamDto?> Handle(GetTeamQuery request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken);
        return team is null ? null : CreateTeamCommandHandler.MapToDto(team);
    }
}

public class ListTeamsQueryHandler : IRequestHandler<ListTeamsQuery, PagedResult<TeamDto>>
{
    private readonly ITeamRepository _teamRepository;

    public ListTeamsQueryHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<PagedResult<TeamDto>> Handle(ListTeamsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _teamRepository.ListAsync(request.DepartmentId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateTeamCommandHandler.MapToDto).ToList();
        return new PagedResult<TeamDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Business Unit Query Handlers ──────────────────────────────────────────

public class GetBusinessUnitQueryHandler : IRequestHandler<GetBusinessUnitQuery, BusinessUnitDto?>
{
    private readonly IBusinessUnitRepository _businessUnitRepository;

    public GetBusinessUnitQueryHandler(IBusinessUnitRepository businessUnitRepository)
    {
        _businessUnitRepository = businessUnitRepository;
    }

    public async Task<BusinessUnitDto?> Handle(GetBusinessUnitQuery request, CancellationToken cancellationToken)
    {
        var businessUnit = await _businessUnitRepository.GetByIdAsync(request.BusinessUnitId, cancellationToken);
        return businessUnit is null ? null : CreateBusinessUnitCommandHandler.MapToDto(businessUnit);
    }
}

public class ListBusinessUnitsQueryHandler : IRequestHandler<ListBusinessUnitsQuery, PagedResult<BusinessUnitDto>>
{
    private readonly IBusinessUnitRepository _businessUnitRepository;

    public ListBusinessUnitsQueryHandler(IBusinessUnitRepository businessUnitRepository)
    {
        _businessUnitRepository = businessUnitRepository;
    }

    public async Task<PagedResult<BusinessUnitDto>> Handle(ListBusinessUnitsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _businessUnitRepository.ListAsync(request.CompanyId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateBusinessUnitCommandHandler.MapToDto).ToList();
        return new PagedResult<BusinessUnitDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Cost Center Query Handlers ────────────────────────────────────────────

public class GetCostCenterQueryHandler : IRequestHandler<GetCostCenterQuery, CostCenterDto?>
{
    private readonly ICostCenterRepository _costCenterRepository;

    public GetCostCenterQueryHandler(ICostCenterRepository costCenterRepository)
    {
        _costCenterRepository = costCenterRepository;
    }

    public async Task<CostCenterDto?> Handle(GetCostCenterQuery request, CancellationToken cancellationToken)
    {
        var costCenter = await _costCenterRepository.GetByIdAsync(request.CostCenterId, cancellationToken);
        return costCenter is null ? null : CreateCostCenterCommandHandler.MapToDto(costCenter);
    }
}

public class ListCostCentersQueryHandler : IRequestHandler<ListCostCentersQuery, PagedResult<CostCenterDto>>
{
    private readonly ICostCenterRepository _costCenterRepository;

    public ListCostCentersQueryHandler(ICostCenterRepository costCenterRepository)
    {
        _costCenterRepository = costCenterRepository;
    }

    public async Task<PagedResult<CostCenterDto>> Handle(ListCostCentersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _costCenterRepository.ListAsync(request.CompanyId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateCostCenterCommandHandler.MapToDto).ToList();
        return new PagedResult<CostCenterDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Profit Center Query Handlers ──────────────────────────────────────────

public class GetProfitCenterQueryHandler : IRequestHandler<GetProfitCenterQuery, ProfitCenterDto?>
{
    private readonly IProfitCenterRepository _profitCenterRepository;

    public GetProfitCenterQueryHandler(IProfitCenterRepository profitCenterRepository)
    {
        _profitCenterRepository = profitCenterRepository;
    }

    public async Task<ProfitCenterDto?> Handle(GetProfitCenterQuery request, CancellationToken cancellationToken)
    {
        var profitCenter = await _profitCenterRepository.GetByIdAsync(request.ProfitCenterId, cancellationToken);
        return profitCenter is null ? null : CreateProfitCenterCommandHandler.MapToDto(profitCenter);
    }
}

public class ListProfitCentersQueryHandler : IRequestHandler<ListProfitCentersQuery, PagedResult<ProfitCenterDto>>
{
    private readonly IProfitCenterRepository _profitCenterRepository;

    public ListProfitCentersQueryHandler(IProfitCenterRepository profitCenterRepository)
    {
        _profitCenterRepository = profitCenterRepository;
    }

    public async Task<PagedResult<ProfitCenterDto>> Handle(ListProfitCentersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _profitCenterRepository.ListAsync(request.CompanyId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateProfitCenterCommandHandler.MapToDto).ToList();
        return new PagedResult<ProfitCenterDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Organization Calendar Query Handlers ──────────────────────────────────

public class GetOrganizationCalendarQueryHandler : IRequestHandler<GetOrganizationCalendarQuery, OrganizationCalendarDto?>
{
    private readonly IOrganizationCalendarRepository _calendarRepository;

    public GetOrganizationCalendarQueryHandler(IOrganizationCalendarRepository calendarRepository)
    {
        _calendarRepository = calendarRepository;
    }

    public async Task<OrganizationCalendarDto?> Handle(GetOrganizationCalendarQuery request, CancellationToken cancellationToken)
    {
        var calendar = await _calendarRepository.GetByIdAsync(request.CalendarId, cancellationToken);
        return calendar is null ? null : CreateOrganizationCalendarCommandHandler.MapToDto(calendar);
    }
}

public class ListOrganizationCalendarsQueryHandler : IRequestHandler<ListOrganizationCalendarsQuery, PagedResult<OrganizationCalendarDto>>
{
    private readonly IOrganizationCalendarRepository _calendarRepository;

    public ListOrganizationCalendarsQueryHandler(IOrganizationCalendarRepository calendarRepository)
    {
        _calendarRepository = calendarRepository;
    }

    public async Task<PagedResult<OrganizationCalendarDto>> Handle(ListOrganizationCalendarsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _calendarRepository.ListAsync(request.CompanyId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateOrganizationCalendarCommandHandler.MapToDto).ToList();
        return new PagedResult<OrganizationCalendarDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Holiday Query Handlers ────────────────────────────────────────────────

public class GetHolidayQueryHandler : IRequestHandler<GetHolidayQuery, HolidayDto?>
{
    private readonly IHolidayRepository _holidayRepository;

    public GetHolidayQueryHandler(IHolidayRepository holidayRepository)
    {
        _holidayRepository = holidayRepository;
    }

    public async Task<HolidayDto?> Handle(GetHolidayQuery request, CancellationToken cancellationToken)
    {
        var holiday = await _holidayRepository.GetByIdAsync(request.HolidayId, cancellationToken);
        return holiday is null ? null : CreateHolidayCommandHandler.MapToDto(holiday);
    }
}

public class ListHolidaysQueryHandler : IRequestHandler<ListHolidaysQuery, PagedResult<HolidayDto>>
{
    private readonly IHolidayRepository _holidayRepository;

    public ListHolidaysQueryHandler(IHolidayRepository holidayRepository)
    {
        _holidayRepository = holidayRepository;
    }

    public async Task<PagedResult<HolidayDto>> Handle(ListHolidaysQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _holidayRepository.ListAsync(request.CalendarId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateHolidayCommandHandler.MapToDto).ToList();
        return new PagedResult<HolidayDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Employee Team Assignment Query Handlers ───────────────────────────────

public class GetEmployeeTeamAssignmentQueryHandler : IRequestHandler<GetEmployeeTeamAssignmentQuery, EmployeeTeamAssignmentDto?>
{
    private readonly IEmployeeTeamAssignmentRepository _assignmentRepository;

    public GetEmployeeTeamAssignmentQueryHandler(IEmployeeTeamAssignmentRepository assignmentRepository)
    {
        _assignmentRepository = assignmentRepository;
    }

    public async Task<EmployeeTeamAssignmentDto?> Handle(GetEmployeeTeamAssignmentQuery request, CancellationToken cancellationToken)
    {
        var assignment = await _assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        return assignment is null ? null : CreateEmployeeTeamAssignmentCommandHandler.MapToDto(assignment);
    }
}

public class ListEmployeeTeamAssignmentsQueryHandler : IRequestHandler<ListEmployeeTeamAssignmentsQuery, PagedResult<EmployeeTeamAssignmentDto>>
{
    private readonly IEmployeeTeamAssignmentRepository _assignmentRepository;

    public ListEmployeeTeamAssignmentsQueryHandler(IEmployeeTeamAssignmentRepository assignmentRepository)
    {
        _assignmentRepository = assignmentRepository;
    }

    public async Task<PagedResult<EmployeeTeamAssignmentDto>> Handle(ListEmployeeTeamAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _assignmentRepository.ListAsync(request.EmployeeId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateEmployeeTeamAssignmentCommandHandler.MapToDto).ToList();
        return new PagedResult<EmployeeTeamAssignmentDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Currency Query Handlers ───────────────────────────────────────────────

public class GetCurrencyQueryHandler : IRequestHandler<GetCurrencyQuery, CurrencyDto?>
{
    private readonly ICurrencyRepository _currencyRepository;

    public GetCurrencyQueryHandler(ICurrencyRepository currencyRepository)
    {
        _currencyRepository = currencyRepository;
    }

    public async Task<CurrencyDto?> Handle(GetCurrencyQuery request, CancellationToken cancellationToken)
    {
        var currency = await _currencyRepository.GetByIdAsync(request.CurrencyId, cancellationToken);
        return currency is null ? null : CreateCurrencyCommandHandler.MapToDto(currency);
    }
}

public class ListCurrenciesQueryHandler : IRequestHandler<ListCurrenciesQuery, PagedResult<CurrencyDto>>
{
    private readonly ICurrencyRepository _currencyRepository;

    public ListCurrenciesQueryHandler(ICurrencyRepository currencyRepository)
    {
        _currencyRepository = currencyRepository;
    }

    public async Task<PagedResult<CurrencyDto>> Handle(ListCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _currencyRepository.ListAsync(request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateCurrencyCommandHandler.MapToDto).ToList();
        return new PagedResult<CurrencyDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

// ─── Exchange Rate Query Handlers ──────────────────────────────────────────

public class GetExchangeRateQueryHandler : IRequestHandler<GetExchangeRateQuery, ExchangeRateDto?>
{
    private readonly IExchangeRateRepository _exchangeRateRepository;

    public GetExchangeRateQueryHandler(IExchangeRateRepository exchangeRateRepository)
    {
        _exchangeRateRepository = exchangeRateRepository;
    }

    public async Task<ExchangeRateDto?> Handle(GetExchangeRateQuery request, CancellationToken cancellationToken)
    {
        var exchangeRate = await _exchangeRateRepository.GetByIdAsync(request.ExchangeRateId, cancellationToken);
        return exchangeRate is null ? null : CreateExchangeRateCommandHandler.MapToDto(exchangeRate);
    }
}

public class ListExchangeRatesQueryHandler : IRequestHandler<ListExchangeRatesQuery, PagedResult<ExchangeRateDto>>
{
    private readonly IExchangeRateRepository _exchangeRateRepository;

    public ListExchangeRatesQueryHandler(IExchangeRateRepository exchangeRateRepository)
    {
        _exchangeRateRepository = exchangeRateRepository;
    }

    public async Task<PagedResult<ExchangeRateDto>> Handle(ListExchangeRatesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _exchangeRateRepository.ListAsync(
            request.FromCurrencyId, request.ToCurrencyId, request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreateExchangeRateCommandHandler.MapToDto).ToList();
        return new PagedResult<ExchangeRateDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}

public class GetEffectiveExchangeRateQueryHandler : IRequestHandler<GetEffectiveExchangeRateQuery, ExchangeRateDto?>
{
    private readonly IExchangeRateRepository _exchangeRateRepository;

    public GetEffectiveExchangeRateQueryHandler(IExchangeRateRepository exchangeRateRepository)
    {
        _exchangeRateRepository = exchangeRateRepository;
    }

    public async Task<ExchangeRateDto?> Handle(GetEffectiveExchangeRateQuery request, CancellationToken cancellationToken)
    {
        var exchangeRate = await _exchangeRateRepository.GetEffectiveRateAsync(
            request.FromCurrencyId, request.ToCurrencyId, request.AsOfDate, cancellationToken);
        return exchangeRate is null ? null : CreateExchangeRateCommandHandler.MapToDto(exchangeRate);
    }
}

// ─── Payment Term Query Handlers ───────────────────────────────────────────

public class GetPaymentTermQueryHandler : IRequestHandler<GetPaymentTermQuery, PaymentTermDto?>
{
    private readonly IPaymentTermRepository _paymentTermRepository;

    public GetPaymentTermQueryHandler(IPaymentTermRepository paymentTermRepository)
    {
        _paymentTermRepository = paymentTermRepository;
    }

    public async Task<PaymentTermDto?> Handle(GetPaymentTermQuery request, CancellationToken cancellationToken)
    {
        var paymentTerm = await _paymentTermRepository.GetByIdAsync(request.PaymentTermId, cancellationToken);
        return paymentTerm is null ? null : CreatePaymentTermCommandHandler.MapToDto(paymentTerm);
    }
}

public class ListPaymentTermsQueryHandler : IRequestHandler<ListPaymentTermsQuery, PagedResult<PaymentTermDto>>
{
    private readonly IPaymentTermRepository _paymentTermRepository;

    public ListPaymentTermsQueryHandler(IPaymentTermRepository paymentTermRepository)
    {
        _paymentTermRepository = paymentTermRepository;
    }

    public async Task<PagedResult<PaymentTermDto>> Handle(ListPaymentTermsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _paymentTermRepository.ListAsync(request.Page, request.PageSize, cancellationToken);
        var dtos = items.Select(CreatePaymentTermCommandHandler.MapToDto).ToList();
        return new PagedResult<PaymentTermDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
