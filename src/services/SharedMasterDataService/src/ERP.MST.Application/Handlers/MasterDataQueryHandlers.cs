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
