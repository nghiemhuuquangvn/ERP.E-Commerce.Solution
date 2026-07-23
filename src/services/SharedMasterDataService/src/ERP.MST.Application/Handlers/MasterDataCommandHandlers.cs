using ERP.MST.Application.Commands;
using ERP.MST.Application.DTOs;
using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.MST.Application.Handlers;

// ─── Department Command Handlers ───────────────────────────────────────────

public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<CreateDepartmentCommandHandler> _logger;

    public CreateDepartmentCommandHandler(
        IDepartmentRepository departmentRepository,
        ILogger<CreateDepartmentCommandHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _departmentRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A department with code '{codeUpper}' already exists.");

        var department = Department.Create(request.Name, codeUpper, request.ManagerId, request.ParentDepartmentId);
        _departmentRepository.Add(department);
        await _departmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Department created: {DepartmentId} ({Code})", department.Id, department.Code);

        return MapToDto(department);
    }

    internal static DepartmentDto MapToDto(Department d, string? languageCode = null) =>
        new(d.Id, d.DepartmentName, d.Code, d.ManagerId, d.ParentDepartmentId, d.DivisionId, languageCode, d.CreatedAt, d.UpdatedAt);
}

public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<UpdateDepartmentCommandHandler> _logger;

    public UpdateDepartmentCommandHandler(
        IDepartmentRepository departmentRepository,
        ILogger<UpdateDepartmentCommandHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Department {request.DepartmentId} not found.");

        department.Update(request.Name, request.Code.ToUpperInvariant(), request.ManagerId);
        _departmentRepository.Update(department);
        await _departmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Department updated: {DepartmentId}", department.Id);

        return CreateDepartmentCommandHandler.MapToDto(department);
    }
}

public class SetDepartmentDivisionCommandHandler : IRequestHandler<SetDepartmentDivisionCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<SetDepartmentDivisionCommandHandler> _logger;

    public SetDepartmentDivisionCommandHandler(
        IDepartmentRepository departmentRepository,
        ILogger<SetDepartmentDivisionCommandHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<DepartmentDto> Handle(SetDepartmentDivisionCommand request, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Department {request.DepartmentId} not found.");

        department.SetDivision(request.DivisionId);
        _departmentRepository.Update(department);
        await _departmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Department {DepartmentId} division set to {DivisionId}", department.Id, request.DivisionId);

        return CreateDepartmentCommandHandler.MapToDto(department);
    }
}

// ─── Position Command Handlers ─────────────────────────────────────────────

public class CreatePositionCommandHandler : IRequestHandler<CreatePositionCommand, PositionDto>
{
    private readonly IPositionRepository _positionRepository;
    private readonly ILogger<CreatePositionCommandHandler> _logger;

    public CreatePositionCommandHandler(
        IPositionRepository positionRepository,
        ILogger<CreatePositionCommandHandler> logger)
    {
        _positionRepository = positionRepository;
        _logger = logger;
    }

    public async Task<PositionDto> Handle(CreatePositionCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _positionRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A position with code '{codeUpper}' already exists.");

        var position = Position.Create(request.Title, codeUpper, request.Description, request.DepartmentId);
        _positionRepository.Add(position);
        await _positionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Position created: {PositionId} ({Code})", position.Id, position.Code);

        return MapToDto(position);
    }

    internal static PositionDto MapToDto(Position p, string? languageCode = null) =>
        new(p.Id, p.Title, p.Code, p.Description, p.DepartmentId, p.IsActive, languageCode, p.CreatedAt, p.UpdatedAt);
}

public class UpdatePositionCommandHandler : IRequestHandler<UpdatePositionCommand, PositionDto>
{
    private readonly IPositionRepository _positionRepository;
    private readonly ILogger<UpdatePositionCommandHandler> _logger;

    public UpdatePositionCommandHandler(
        IPositionRepository positionRepository,
        ILogger<UpdatePositionCommandHandler> logger)
    {
        _positionRepository = positionRepository;
        _logger = logger;
    }

    public async Task<PositionDto> Handle(UpdatePositionCommand request, CancellationToken cancellationToken)
    {
        var position = await _positionRepository.GetByIdAsync(request.PositionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Position {request.PositionId} not found.");

        position.Update(request.Title, request.Description);
        _positionRepository.Update(position);
        await _positionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Position updated: {PositionId}", position.Id);

        return CreatePositionCommandHandler.MapToDto(position);
    }
}

public class DeactivatePositionCommandHandler : IRequestHandler<DeactivatePositionCommand, PositionDto>
{
    private readonly IPositionRepository _positionRepository;
    private readonly ILogger<DeactivatePositionCommandHandler> _logger;

    public DeactivatePositionCommandHandler(
        IPositionRepository positionRepository,
        ILogger<DeactivatePositionCommandHandler> logger)
    {
        _positionRepository = positionRepository;
        _logger = logger;
    }

    public async Task<PositionDto> Handle(DeactivatePositionCommand request, CancellationToken cancellationToken)
    {
        var position = await _positionRepository.GetByIdAsync(request.PositionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Position {request.PositionId} not found.");

        position.Deactivate();
        _positionRepository.Update(position);
        await _positionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Position deactivated: {PositionId}", position.Id);

        return CreatePositionCommandHandler.MapToDto(position);
    }
}

public class ActivatePositionCommandHandler : IRequestHandler<ActivatePositionCommand, PositionDto>
{
    private readonly IPositionRepository _positionRepository;
    private readonly ILogger<ActivatePositionCommandHandler> _logger;

    public ActivatePositionCommandHandler(
        IPositionRepository positionRepository,
        ILogger<ActivatePositionCommandHandler> logger)
    {
        _positionRepository = positionRepository;
        _logger = logger;
    }

    public async Task<PositionDto> Handle(ActivatePositionCommand request, CancellationToken cancellationToken)
    {
        var position = await _positionRepository.GetByIdAsync(request.PositionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Position {request.PositionId} not found.");

        position.Activate();
        _positionRepository.Update(position);
        await _positionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Position activated: {PositionId}", position.Id);

        return CreatePositionCommandHandler.MapToDto(position);
    }
}

// ─── Employee Command Handlers ─────────────────────────────────────────────

public class HireEmployeeCommandHandler : IRequestHandler<HireEmployeeCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<HireEmployeeCommandHandler> _logger;

    public HireEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        ILogger<HireEmployeeCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(HireEmployeeCommand request, CancellationToken cancellationToken)
    {
        _ = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Department {request.DepartmentId} not found.");

        if (!string.IsNullOrEmpty(request.Email) &&
            await _employeeRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            throw new InvalidOperationException($"An employee with email '{request.Email}' already exists.");

        var year = request.DateOfHire.Year;
        var random4 = Random.Shared.Next(1000, 9999);
        var employeeNumber = $"EMP-{year}-{random4:D4}";

        var employee = Employee.Create(
            employeeNumber,
            request.FirstName,
            request.LastName,
            request.DepartmentId,
            request.DateOfHire,
            request.EmploymentType,
            request.PositionId);

        employee.Email = request.Email;
        employee.Phone = request.Phone;

        employee.RaiseHiredEvent();

        _employeeRepository.Add(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee hired: {EmployeeId} ({EmployeeNumber})", employee.Id, employee.EmployeeNumber);

        return MapToDto(employee);
    }

    internal static EmployeeDto MapToDto(Employee e) =>
        new(e.Id,
            e.EmployeeNumber,
            e.FirstName,
            e.LastName,
            e.FullName,
            e.Email,
            e.Phone,
            e.DateOfHire,
            e.DepartmentId,
            e.PositionId,
            ((EmploymentStatus)e.EmploymentStatus).ToString(),
            ((EmploymentType)e.EmploymentType).ToString(),
            e.CreatedAt,
            e.UpdatedAt);
}

public class TerminateEmployeeCommandHandler : IRequestHandler<TerminateEmployeeCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<TerminateEmployeeCommandHandler> _logger;

    public TerminateEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        ILogger<TerminateEmployeeCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(TerminateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {request.EmployeeId} not found.");

        employee.Terminate(request.Reason);
        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee terminated: {EmployeeId}", employee.Id);

        return HireEmployeeCommandHandler.MapToDto(employee);
    }
}

public class UpdateEmployeeProfileCommandHandler : IRequestHandler<UpdateEmployeeProfileCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<UpdateEmployeeProfileCommandHandler> _logger;

    public UpdateEmployeeProfileCommandHandler(
        IEmployeeRepository employeeRepository,
        ILogger<UpdateEmployeeProfileCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(UpdateEmployeeProfileCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {request.EmployeeId} not found.");

        if (!string.IsNullOrEmpty(request.Email) &&
            request.Email != employee.Email &&
            await _employeeRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            throw new InvalidOperationException($"An employee with email '{request.Email}' already exists.");

        employee.UpdateProfile(request.Email, request.Phone);
        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee profile updated: {EmployeeId}", employee.Id);

        return HireEmployeeCommandHandler.MapToDto(employee);
    }
}

public class TransferEmployeeCommandHandler : IRequestHandler<TransferEmployeeCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<TransferEmployeeCommandHandler> _logger;

    public TransferEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        ILogger<TransferEmployeeCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(TransferEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {request.EmployeeId} not found.");

        _ = await _departmentRepository.GetByIdAsync(request.NewDepartmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Department {request.NewDepartmentId} not found.");

        if (employee.DepartmentId == request.NewDepartmentId)
            throw new InvalidOperationException("Employee is already in the specified department.");

        employee.Transfer(request.NewDepartmentId);
        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee {EmployeeId} transferred to department {DepartmentId}",
            employee.Id, request.NewDepartmentId);

        return HireEmployeeCommandHandler.MapToDto(employee);
    }
}

public class SetEmployeeOnLeaveCommandHandler : IRequestHandler<SetEmployeeOnLeaveCommand, Unit>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<SetEmployeeOnLeaveCommandHandler> _logger;

    public SetEmployeeOnLeaveCommandHandler(IEmployeeRepository employeeRepository, ILogger<SetEmployeeOnLeaveCommandHandler> logger)
    {
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    public async Task<Unit> Handle(SetEmployeeOnLeaveCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("Received LeaveApproved for unknown employee {EmployeeId} — ignoring.", request.EmployeeId);
            return Unit.Value;
        }

        employee.SetOnLeave();
        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee {EmployeeId} set to OnLeave (approved by HRM)", employee.Id);
        return Unit.Value;
    }
}

// ─── Unit Of Measure Command Handlers ──────────────────────────────────────

public class CreateUnitOfMeasureCommandHandler : IRequestHandler<CreateUnitOfMeasureCommand, UnitOfMeasureDto>
{
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;
    private readonly ILogger<CreateUnitOfMeasureCommandHandler> _logger;

    public CreateUnitOfMeasureCommandHandler(
        IUnitOfMeasureRepository unitOfMeasureRepository,
        ILogger<CreateUnitOfMeasureCommandHandler> logger)
    {
        _unitOfMeasureRepository = unitOfMeasureRepository;
        _logger = logger;
    }

    public async Task<UnitOfMeasureDto> Handle(CreateUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _unitOfMeasureRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A unit of measure with code '{codeUpper}' already exists.");

        var unitOfMeasure = UnitOfMeasure.Create(
            codeUpper,
            request.Name,
            request.Symbol,
            request.UnitType,
            request.BaseUnitId,
            request.ConversionFactor);

        _unitOfMeasureRepository.Add(unitOfMeasure);
        await _unitOfMeasureRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unit of measure created: {UnitOfMeasureId} ({Code})", unitOfMeasure.Id, unitOfMeasure.Code);

        return MapToDto(unitOfMeasure);
    }

    internal static UnitOfMeasureDto MapToDto(UnitOfMeasure u, string? languageCode = null) =>
        new(u.Id, u.Code, u.Name, u.Symbol, ((UnitType)u.UnitType).ToString(), u.BaseUnitId, u.ConversionFactor, languageCode, u.CreatedAt, u.UpdatedAt);
}

public class UpdateUnitOfMeasureCommandHandler : IRequestHandler<UpdateUnitOfMeasureCommand, UnitOfMeasureDto>
{
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;
    private readonly ILogger<UpdateUnitOfMeasureCommandHandler> _logger;

    public UpdateUnitOfMeasureCommandHandler(
        IUnitOfMeasureRepository unitOfMeasureRepository,
        ILogger<UpdateUnitOfMeasureCommandHandler> logger)
    {
        _unitOfMeasureRepository = unitOfMeasureRepository;
        _logger = logger;
    }

    public async Task<UnitOfMeasureDto> Handle(UpdateUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var unitOfMeasure = await _unitOfMeasureRepository.GetByIdAsync(request.UnitOfMeasureId, cancellationToken)
            ?? throw new KeyNotFoundException($"Unit of measure {request.UnitOfMeasureId} not found.");

        unitOfMeasure.Update(request.Name, request.Symbol, request.ConversionFactor);
        _unitOfMeasureRepository.Update(unitOfMeasure);
        await _unitOfMeasureRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unit of measure updated: {UnitOfMeasureId}", unitOfMeasure.Id);

        return CreateUnitOfMeasureCommandHandler.MapToDto(unitOfMeasure);
    }
}

// ─── Geography Command Handlers ────────────────────────────────────────────

public class CreateCountryCommandHandler : IRequestHandler<CreateCountryCommand, CountryDto>
{
    private readonly ICountryRepository _countryRepository;
    private readonly ILogger<CreateCountryCommandHandler> _logger;

    public CreateCountryCommandHandler(
        ICountryRepository countryRepository,
        ILogger<CreateCountryCommandHandler> logger)
    {
        _countryRepository = countryRepository;
        _logger = logger;
    }

    public async Task<CountryDto> Handle(CreateCountryCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _countryRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A country with code '{codeUpper}' already exists.");

        var country = Country.Create(codeUpper, request.Name, request.PhoneCode);

        _countryRepository.Add(country);
        await _countryRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Country created: {CountryId} ({Code})", country.Id, country.Code);

        return MapToDto(country);
    }

    internal static CountryDto MapToDto(Country c, string? languageCode = null) =>
        new(c.Id, c.Code, c.Name, c.PhoneCode, languageCode, c.CreatedAt, c.UpdatedAt);
}

public class CreateProvinceCommandHandler : IRequestHandler<CreateProvinceCommand, ProvinceDto>
{
    private readonly IProvinceRepository _provinceRepository;
    private readonly ICountryRepository _countryRepository;
    private readonly ILogger<CreateProvinceCommandHandler> _logger;

    public CreateProvinceCommandHandler(
        IProvinceRepository provinceRepository,
        ICountryRepository countryRepository,
        ILogger<CreateProvinceCommandHandler> logger)
    {
        _provinceRepository = provinceRepository;
        _countryRepository = countryRepository;
        _logger = logger;
    }

    public async Task<ProvinceDto> Handle(CreateProvinceCommand request, CancellationToken cancellationToken)
    {
        _ = await _countryRepository.GetByIdAsync(request.CountryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Country {request.CountryId} not found.");

        var province = Province.Create(request.CountryId, request.Code.ToUpperInvariant(), request.Name);

        _provinceRepository.Add(province);
        await _provinceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Province created: {ProvinceId} ({Code})", province.Id, province.Code);

        return MapToDto(province);
    }

    internal static ProvinceDto MapToDto(Province p, string? languageCode = null) =>
        new(p.Id, p.CountryId, p.Code, p.Name, languageCode, p.CreatedAt, p.UpdatedAt);
}

public class CreateDistrictCommandHandler : IRequestHandler<CreateDistrictCommand, DistrictDto>
{
    private readonly IDistrictRepository _districtRepository;
    private readonly IProvinceRepository _provinceRepository;
    private readonly ILogger<CreateDistrictCommandHandler> _logger;

    public CreateDistrictCommandHandler(
        IDistrictRepository districtRepository,
        IProvinceRepository provinceRepository,
        ILogger<CreateDistrictCommandHandler> logger)
    {
        _districtRepository = districtRepository;
        _provinceRepository = provinceRepository;
        _logger = logger;
    }

    public async Task<DistrictDto> Handle(CreateDistrictCommand request, CancellationToken cancellationToken)
    {
        _ = await _provinceRepository.GetByIdAsync(request.ProvinceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Province {request.ProvinceId} not found.");

        var district = District.Create(request.ProvinceId, request.Code.ToUpperInvariant(), request.Name);

        _districtRepository.Add(district);
        await _districtRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("District created: {DistrictId} ({Code})", district.Id, district.Code);

        return MapToDto(district);
    }

    internal static DistrictDto MapToDto(District d, string? languageCode = null) =>
        new(d.Id, d.ProvinceId, d.Code, d.Name, languageCode, d.CreatedAt, d.UpdatedAt);
}

public class CreateWardCommandHandler : IRequestHandler<CreateWardCommand, WardDto>
{
    private readonly IWardRepository _wardRepository;
    private readonly IDistrictRepository _districtRepository;
    private readonly ILogger<CreateWardCommandHandler> _logger;

    public CreateWardCommandHandler(
        IWardRepository wardRepository,
        IDistrictRepository districtRepository,
        ILogger<CreateWardCommandHandler> logger)
    {
        _wardRepository = wardRepository;
        _districtRepository = districtRepository;
        _logger = logger;
    }

    public async Task<WardDto> Handle(CreateWardCommand request, CancellationToken cancellationToken)
    {
        _ = await _districtRepository.GetByIdAsync(request.DistrictId, cancellationToken)
            ?? throw new KeyNotFoundException($"District {request.DistrictId} not found.");

        var ward = Ward.Create(request.DistrictId, request.Code.ToUpperInvariant(), request.Name);

        _wardRepository.Add(ward);
        await _wardRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Ward created: {WardId} ({Code})", ward.Id, ward.Code);

        return MapToDto(ward);
    }

    internal static WardDto MapToDto(Ward w, string? languageCode = null) =>
        new(w.Id, w.DistrictId, w.Code, w.Name, languageCode, w.CreatedAt, w.UpdatedAt);
}

// ─── Language Command Handlers ─────────────────────────────────────────────

public class CreateLanguageCommandHandler : IRequestHandler<CreateLanguageCommand, SupportedLanguageDto>
{
    private readonly ISupportedLanguageRepository _languageRepository;
    private readonly ILogger<CreateLanguageCommandHandler> _logger;

    public CreateLanguageCommandHandler(
        ISupportedLanguageRepository languageRepository,
        ILogger<CreateLanguageCommandHandler> logger)
    {
        _languageRepository = languageRepository;
        _logger = logger;
    }

    public async Task<SupportedLanguageDto> Handle(CreateLanguageCommand request, CancellationToken cancellationToken)
    {
        if (await _languageRepository.ExistsByCodeAsync(request.Code, cancellationToken))
            throw new InvalidOperationException($"A language with code '{request.Code}' already exists.");

        var language = SupportedLanguage.Create(request.Code, request.Name, request.NativeName, request.IsDefault, request.SortOrder);

        _languageRepository.Add(language);
        await _languageRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Language created: {LanguageId} ({Code})", language.Id, language.Code);

        return MapToDto(language);
    }

    internal static SupportedLanguageDto MapToDto(SupportedLanguage l) =>
        new(l.Id, l.Code, l.Name, l.NativeName, l.IsDefault, l.IsActive, l.SortOrder, l.CreatedAt, l.UpdatedAt);
}

// ─── Company Command Handlers ──────────────────────────────────────────────

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, CompanyDto>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogger<CreateCompanyCommandHandler> _logger;

    public CreateCompanyCommandHandler(
        ICompanyRepository companyRepository,
        ILogger<CreateCompanyCommandHandler> logger)
    {
        _companyRepository = companyRepository;
        _logger = logger;
    }

    public async Task<CompanyDto> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _companyRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A company with code '{codeUpper}' already exists.");

        var company = Company.Create(request.Name, codeUpper, request.TaxCode, request.LegalAddress, request.CurrencyCode);
        _companyRepository.Add(company);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company created: {CompanyId} ({Code})", company.Id, company.Code);

        return MapToDto(company);
    }

    internal static CompanyDto MapToDto(Company c) =>
        new(c.Id, c.Name, c.Code, c.TaxCode, c.LegalAddress, c.CurrencyCode, c.IsActive, c.CreatedAt, c.UpdatedAt);
}

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, CompanyDto>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogger<UpdateCompanyCommandHandler> _logger;

    public UpdateCompanyCommandHandler(
        ICompanyRepository companyRepository,
        ILogger<UpdateCompanyCommandHandler> logger)
    {
        _companyRepository = companyRepository;
        _logger = logger;
    }

    public async Task<CompanyDto> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Company {request.CompanyId} not found.");

        company.Update(request.Name, request.Code.ToUpperInvariant(), request.TaxCode, request.LegalAddress, request.CurrencyCode);
        _companyRepository.Update(company);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company updated: {CompanyId}", company.Id);

        return CreateCompanyCommandHandler.MapToDto(company);
    }
}

public class ActivateCompanyCommandHandler : IRequestHandler<ActivateCompanyCommand, CompanyDto>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogger<ActivateCompanyCommandHandler> _logger;

    public ActivateCompanyCommandHandler(
        ICompanyRepository companyRepository,
        ILogger<ActivateCompanyCommandHandler> logger)
    {
        _companyRepository = companyRepository;
        _logger = logger;
    }

    public async Task<CompanyDto> Handle(ActivateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Company {request.CompanyId} not found.");

        company.Activate();
        _companyRepository.Update(company);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company activated: {CompanyId}", company.Id);

        return CreateCompanyCommandHandler.MapToDto(company);
    }
}

public class DeactivateCompanyCommandHandler : IRequestHandler<DeactivateCompanyCommand, CompanyDto>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogger<DeactivateCompanyCommandHandler> _logger;

    public DeactivateCompanyCommandHandler(
        ICompanyRepository companyRepository,
        ILogger<DeactivateCompanyCommandHandler> logger)
    {
        _companyRepository = companyRepository;
        _logger = logger;
    }

    public async Task<CompanyDto> Handle(DeactivateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Company {request.CompanyId} not found.");

        company.Deactivate();
        _companyRepository.Update(company);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company deactivated: {CompanyId}", company.Id);

        return CreateCompanyCommandHandler.MapToDto(company);
    }
}

// ─── Branch Command Handlers ───────────────────────────────────────────────

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, BranchDto>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogger<CreateBranchCommandHandler> _logger;

    public CreateBranchCommandHandler(
        IBranchRepository branchRepository,
        ILogger<CreateBranchCommandHandler> logger)
    {
        _branchRepository = branchRepository;
        _logger = logger;
    }

    public async Task<BranchDto> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _branchRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A branch with code '{codeUpper}' already exists.");

        var branch = Branch.Create(request.CompanyId, request.Name, codeUpper, request.Address, request.WardId, request.ManagerEmployeeId);
        _branchRepository.Add(branch);
        await _branchRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Branch created: {BranchId} ({Code})", branch.Id, branch.Code);

        return MapToDto(branch);
    }

    internal static BranchDto MapToDto(Branch b) =>
        new(b.Id, b.CompanyId, b.Name, b.Code, b.Address, b.WardId, b.ManagerEmployeeId, b.IsActive, b.CreatedAt, b.UpdatedAt);
}

public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, BranchDto>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogger<UpdateBranchCommandHandler> _logger;

    public UpdateBranchCommandHandler(
        IBranchRepository branchRepository,
        ILogger<UpdateBranchCommandHandler> logger)
    {
        _branchRepository = branchRepository;
        _logger = logger;
    }

    public async Task<BranchDto> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Branch {request.BranchId} not found.");

        branch.Update(request.Name, request.Code.ToUpperInvariant(), request.Address, request.WardId, request.ManagerEmployeeId);
        _branchRepository.Update(branch);
        await _branchRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Branch updated: {BranchId}", branch.Id);

        return CreateBranchCommandHandler.MapToDto(branch);
    }
}

public class ActivateBranchCommandHandler : IRequestHandler<ActivateBranchCommand, BranchDto>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogger<ActivateBranchCommandHandler> _logger;

    public ActivateBranchCommandHandler(
        IBranchRepository branchRepository,
        ILogger<ActivateBranchCommandHandler> logger)
    {
        _branchRepository = branchRepository;
        _logger = logger;
    }

    public async Task<BranchDto> Handle(ActivateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Branch {request.BranchId} not found.");

        branch.Activate();
        _branchRepository.Update(branch);
        await _branchRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Branch activated: {BranchId}", branch.Id);

        return CreateBranchCommandHandler.MapToDto(branch);
    }
}

public class DeactivateBranchCommandHandler : IRequestHandler<DeactivateBranchCommand, BranchDto>
{
    private readonly IBranchRepository _branchRepository;
    private readonly ILogger<DeactivateBranchCommandHandler> _logger;

    public DeactivateBranchCommandHandler(
        IBranchRepository branchRepository,
        ILogger<DeactivateBranchCommandHandler> logger)
    {
        _branchRepository = branchRepository;
        _logger = logger;
    }

    public async Task<BranchDto> Handle(DeactivateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Branch {request.BranchId} not found.");

        branch.Deactivate();
        _branchRepository.Update(branch);
        await _branchRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Branch deactivated: {BranchId}", branch.Id);

        return CreateBranchCommandHandler.MapToDto(branch);
    }
}

// ─── Division Command Handlers ─────────────────────────────────────────────

public class CreateDivisionCommandHandler : IRequestHandler<CreateDivisionCommand, DivisionDto>
{
    private readonly IDivisionRepository _divisionRepository;
    private readonly ILogger<CreateDivisionCommandHandler> _logger;

    public CreateDivisionCommandHandler(
        IDivisionRepository divisionRepository,
        ILogger<CreateDivisionCommandHandler> logger)
    {
        _divisionRepository = divisionRepository;
        _logger = logger;
    }

    public async Task<DivisionDto> Handle(CreateDivisionCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _divisionRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A division with code '{codeUpper}' already exists.");

        var division = Division.Create(request.BranchId, request.Name, codeUpper, request.ParentDivisionId);
        _divisionRepository.Add(division);
        await _divisionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Division created: {DivisionId} ({Code})", division.Id, division.Code);

        return MapToDto(division);
    }

    internal static DivisionDto MapToDto(Division d) =>
        new(d.Id, d.BranchId, d.Name, d.Code, d.ParentDivisionId, d.IsActive, d.CreatedAt, d.UpdatedAt);
}

public class UpdateDivisionCommandHandler : IRequestHandler<UpdateDivisionCommand, DivisionDto>
{
    private readonly IDivisionRepository _divisionRepository;
    private readonly ILogger<UpdateDivisionCommandHandler> _logger;

    public UpdateDivisionCommandHandler(
        IDivisionRepository divisionRepository,
        ILogger<UpdateDivisionCommandHandler> logger)
    {
        _divisionRepository = divisionRepository;
        _logger = logger;
    }

    public async Task<DivisionDto> Handle(UpdateDivisionCommand request, CancellationToken cancellationToken)
    {
        var division = await _divisionRepository.GetByIdAsync(request.DivisionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Division {request.DivisionId} not found.");

        division.Update(request.Name, request.Code.ToUpperInvariant(), request.ParentDivisionId);
        _divisionRepository.Update(division);
        await _divisionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Division updated: {DivisionId}", division.Id);

        return CreateDivisionCommandHandler.MapToDto(division);
    }
}

public class ActivateDivisionCommandHandler : IRequestHandler<ActivateDivisionCommand, DivisionDto>
{
    private readonly IDivisionRepository _divisionRepository;
    private readonly ILogger<ActivateDivisionCommandHandler> _logger;

    public ActivateDivisionCommandHandler(
        IDivisionRepository divisionRepository,
        ILogger<ActivateDivisionCommandHandler> logger)
    {
        _divisionRepository = divisionRepository;
        _logger = logger;
    }

    public async Task<DivisionDto> Handle(ActivateDivisionCommand request, CancellationToken cancellationToken)
    {
        var division = await _divisionRepository.GetByIdAsync(request.DivisionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Division {request.DivisionId} not found.");

        division.Activate();
        _divisionRepository.Update(division);
        await _divisionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Division activated: {DivisionId}", division.Id);

        return CreateDivisionCommandHandler.MapToDto(division);
    }
}

public class DeactivateDivisionCommandHandler : IRequestHandler<DeactivateDivisionCommand, DivisionDto>
{
    private readonly IDivisionRepository _divisionRepository;
    private readonly ILogger<DeactivateDivisionCommandHandler> _logger;

    public DeactivateDivisionCommandHandler(
        IDivisionRepository divisionRepository,
        ILogger<DeactivateDivisionCommandHandler> logger)
    {
        _divisionRepository = divisionRepository;
        _logger = logger;
    }

    public async Task<DivisionDto> Handle(DeactivateDivisionCommand request, CancellationToken cancellationToken)
    {
        var division = await _divisionRepository.GetByIdAsync(request.DivisionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Division {request.DivisionId} not found.");

        division.Deactivate();
        _divisionRepository.Update(division);
        await _divisionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Division deactivated: {DivisionId}", division.Id);

        return CreateDivisionCommandHandler.MapToDto(division);
    }
}

// ─── Team Command Handlers ─────────────────────────────────────────────────

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ILogger<CreateTeamCommandHandler> _logger;

    public CreateTeamCommandHandler(
        ITeamRepository teamRepository,
        ILogger<CreateTeamCommandHandler> logger)
    {
        _teamRepository = teamRepository;
        _logger = logger;
    }

    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _teamRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A team with code '{codeUpper}' already exists.");

        var team = Team.Create(request.DepartmentId, request.Name, codeUpper, request.LeaderEmployeeId);
        _teamRepository.Add(team);
        await _teamRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team created: {TeamId} ({Code})", team.Id, team.Code);

        return MapToDto(team);
    }

    internal static TeamDto MapToDto(Team t) =>
        new(t.Id, t.DepartmentId, t.Name, t.Code, t.LeaderEmployeeId, t.IsActive, t.CreatedAt, t.UpdatedAt);
}

public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ILogger<UpdateTeamCommandHandler> _logger;

    public UpdateTeamCommandHandler(
        ITeamRepository teamRepository,
        ILogger<UpdateTeamCommandHandler> logger)
    {
        _teamRepository = teamRepository;
        _logger = logger;
    }

    public async Task<TeamDto> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team {request.TeamId} not found.");

        team.Update(request.Name, request.Code.ToUpperInvariant(), request.LeaderEmployeeId);
        _teamRepository.Update(team);
        await _teamRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team updated: {TeamId}", team.Id);

        return CreateTeamCommandHandler.MapToDto(team);
    }
}

public class ActivateTeamCommandHandler : IRequestHandler<ActivateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ILogger<ActivateTeamCommandHandler> _logger;

    public ActivateTeamCommandHandler(
        ITeamRepository teamRepository,
        ILogger<ActivateTeamCommandHandler> logger)
    {
        _teamRepository = teamRepository;
        _logger = logger;
    }

    public async Task<TeamDto> Handle(ActivateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team {request.TeamId} not found.");

        team.Activate();
        _teamRepository.Update(team);
        await _teamRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team activated: {TeamId}", team.Id);

        return CreateTeamCommandHandler.MapToDto(team);
    }
}

public class DeactivateTeamCommandHandler : IRequestHandler<DeactivateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ILogger<DeactivateTeamCommandHandler> _logger;

    public DeactivateTeamCommandHandler(
        ITeamRepository teamRepository,
        ILogger<DeactivateTeamCommandHandler> logger)
    {
        _teamRepository = teamRepository;
        _logger = logger;
    }

    public async Task<TeamDto> Handle(DeactivateTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team {request.TeamId} not found.");

        team.Deactivate();
        _teamRepository.Update(team);
        await _teamRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team deactivated: {TeamId}", team.Id);

        return CreateTeamCommandHandler.MapToDto(team);
    }
}

// ─── Business Unit Command Handlers ────────────────────────────────────────

public class CreateBusinessUnitCommandHandler : IRequestHandler<CreateBusinessUnitCommand, BusinessUnitDto>
{
    private readonly IBusinessUnitRepository _businessUnitRepository;
    private readonly ILogger<CreateBusinessUnitCommandHandler> _logger;

    public CreateBusinessUnitCommandHandler(
        IBusinessUnitRepository businessUnitRepository,
        ILogger<CreateBusinessUnitCommandHandler> logger)
    {
        _businessUnitRepository = businessUnitRepository;
        _logger = logger;
    }

    public async Task<BusinessUnitDto> Handle(CreateBusinessUnitCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _businessUnitRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A business unit with code '{codeUpper}' already exists.");

        var businessUnit = BusinessUnit.Create(request.CompanyId, request.Name, codeUpper);
        _businessUnitRepository.Add(businessUnit);
        await _businessUnitRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business unit created: {BusinessUnitId} ({Code})", businessUnit.Id, businessUnit.Code);

        return MapToDto(businessUnit);
    }

    internal static BusinessUnitDto MapToDto(BusinessUnit b) =>
        new(b.Id, b.CompanyId, b.Name, b.Code, b.IsActive, b.CreatedAt, b.UpdatedAt);
}

public class UpdateBusinessUnitCommandHandler : IRequestHandler<UpdateBusinessUnitCommand, BusinessUnitDto>
{
    private readonly IBusinessUnitRepository _businessUnitRepository;
    private readonly ILogger<UpdateBusinessUnitCommandHandler> _logger;

    public UpdateBusinessUnitCommandHandler(
        IBusinessUnitRepository businessUnitRepository,
        ILogger<UpdateBusinessUnitCommandHandler> logger)
    {
        _businessUnitRepository = businessUnitRepository;
        _logger = logger;
    }

    public async Task<BusinessUnitDto> Handle(UpdateBusinessUnitCommand request, CancellationToken cancellationToken)
    {
        var businessUnit = await _businessUnitRepository.GetByIdAsync(request.BusinessUnitId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business unit {request.BusinessUnitId} not found.");

        businessUnit.Update(request.Name, request.Code.ToUpperInvariant());
        _businessUnitRepository.Update(businessUnit);
        await _businessUnitRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business unit updated: {BusinessUnitId}", businessUnit.Id);

        return CreateBusinessUnitCommandHandler.MapToDto(businessUnit);
    }
}

public class ActivateBusinessUnitCommandHandler : IRequestHandler<ActivateBusinessUnitCommand, BusinessUnitDto>
{
    private readonly IBusinessUnitRepository _businessUnitRepository;
    private readonly ILogger<ActivateBusinessUnitCommandHandler> _logger;

    public ActivateBusinessUnitCommandHandler(
        IBusinessUnitRepository businessUnitRepository,
        ILogger<ActivateBusinessUnitCommandHandler> logger)
    {
        _businessUnitRepository = businessUnitRepository;
        _logger = logger;
    }

    public async Task<BusinessUnitDto> Handle(ActivateBusinessUnitCommand request, CancellationToken cancellationToken)
    {
        var businessUnit = await _businessUnitRepository.GetByIdAsync(request.BusinessUnitId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business unit {request.BusinessUnitId} not found.");

        businessUnit.Activate();
        _businessUnitRepository.Update(businessUnit);
        await _businessUnitRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business unit activated: {BusinessUnitId}", businessUnit.Id);

        return CreateBusinessUnitCommandHandler.MapToDto(businessUnit);
    }
}

public class DeactivateBusinessUnitCommandHandler : IRequestHandler<DeactivateBusinessUnitCommand, BusinessUnitDto>
{
    private readonly IBusinessUnitRepository _businessUnitRepository;
    private readonly ILogger<DeactivateBusinessUnitCommandHandler> _logger;

    public DeactivateBusinessUnitCommandHandler(
        IBusinessUnitRepository businessUnitRepository,
        ILogger<DeactivateBusinessUnitCommandHandler> logger)
    {
        _businessUnitRepository = businessUnitRepository;
        _logger = logger;
    }

    public async Task<BusinessUnitDto> Handle(DeactivateBusinessUnitCommand request, CancellationToken cancellationToken)
    {
        var businessUnit = await _businessUnitRepository.GetByIdAsync(request.BusinessUnitId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business unit {request.BusinessUnitId} not found.");

        businessUnit.Deactivate();
        _businessUnitRepository.Update(businessUnit);
        await _businessUnitRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business unit deactivated: {BusinessUnitId}", businessUnit.Id);

        return CreateBusinessUnitCommandHandler.MapToDto(businessUnit);
    }
}

// ─── Cost Center Command Handlers ──────────────────────────────────────────

public class CreateCostCenterCommandHandler : IRequestHandler<CreateCostCenterCommand, CostCenterDto>
{
    private readonly ICostCenterRepository _costCenterRepository;
    private readonly ILogger<CreateCostCenterCommandHandler> _logger;

    public CreateCostCenterCommandHandler(
        ICostCenterRepository costCenterRepository,
        ILogger<CreateCostCenterCommandHandler> logger)
    {
        _costCenterRepository = costCenterRepository;
        _logger = logger;
    }

    public async Task<CostCenterDto> Handle(CreateCostCenterCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _costCenterRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A cost center with code '{codeUpper}' already exists.");

        var costCenter = CostCenter.Create(request.CompanyId, request.Name, codeUpper, request.BudgetAmount, request.DepartmentId);
        _costCenterRepository.Add(costCenter);
        await _costCenterRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cost center created: {CostCenterId} ({Code})", costCenter.Id, costCenter.Code);

        return MapToDto(costCenter);
    }

    internal static CostCenterDto MapToDto(CostCenter c) =>
        new(c.Id, c.CompanyId, c.DepartmentId, c.Name, c.Code, c.BudgetAmount, c.IsActive, c.CreatedAt, c.UpdatedAt);
}

public class UpdateCostCenterCommandHandler : IRequestHandler<UpdateCostCenterCommand, CostCenterDto>
{
    private readonly ICostCenterRepository _costCenterRepository;
    private readonly ILogger<UpdateCostCenterCommandHandler> _logger;

    public UpdateCostCenterCommandHandler(
        ICostCenterRepository costCenterRepository,
        ILogger<UpdateCostCenterCommandHandler> logger)
    {
        _costCenterRepository = costCenterRepository;
        _logger = logger;
    }

    public async Task<CostCenterDto> Handle(UpdateCostCenterCommand request, CancellationToken cancellationToken)
    {
        var costCenter = await _costCenterRepository.GetByIdAsync(request.CostCenterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cost center {request.CostCenterId} not found.");

        costCenter.Update(request.Name, request.Code.ToUpperInvariant(), request.BudgetAmount, request.DepartmentId);
        _costCenterRepository.Update(costCenter);
        await _costCenterRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cost center updated: {CostCenterId}", costCenter.Id);

        return CreateCostCenterCommandHandler.MapToDto(costCenter);
    }
}

public class ActivateCostCenterCommandHandler : IRequestHandler<ActivateCostCenterCommand, CostCenterDto>
{
    private readonly ICostCenterRepository _costCenterRepository;
    private readonly ILogger<ActivateCostCenterCommandHandler> _logger;

    public ActivateCostCenterCommandHandler(
        ICostCenterRepository costCenterRepository,
        ILogger<ActivateCostCenterCommandHandler> logger)
    {
        _costCenterRepository = costCenterRepository;
        _logger = logger;
    }

    public async Task<CostCenterDto> Handle(ActivateCostCenterCommand request, CancellationToken cancellationToken)
    {
        var costCenter = await _costCenterRepository.GetByIdAsync(request.CostCenterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cost center {request.CostCenterId} not found.");

        costCenter.Activate();
        _costCenterRepository.Update(costCenter);
        await _costCenterRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cost center activated: {CostCenterId}", costCenter.Id);

        return CreateCostCenterCommandHandler.MapToDto(costCenter);
    }
}

public class DeactivateCostCenterCommandHandler : IRequestHandler<DeactivateCostCenterCommand, CostCenterDto>
{
    private readonly ICostCenterRepository _costCenterRepository;
    private readonly ILogger<DeactivateCostCenterCommandHandler> _logger;

    public DeactivateCostCenterCommandHandler(
        ICostCenterRepository costCenterRepository,
        ILogger<DeactivateCostCenterCommandHandler> logger)
    {
        _costCenterRepository = costCenterRepository;
        _logger = logger;
    }

    public async Task<CostCenterDto> Handle(DeactivateCostCenterCommand request, CancellationToken cancellationToken)
    {
        var costCenter = await _costCenterRepository.GetByIdAsync(request.CostCenterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cost center {request.CostCenterId} not found.");

        costCenter.Deactivate();
        _costCenterRepository.Update(costCenter);
        await _costCenterRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cost center deactivated: {CostCenterId}", costCenter.Id);

        return CreateCostCenterCommandHandler.MapToDto(costCenter);
    }
}

// ─── Profit Center Command Handlers ────────────────────────────────────────

public class CreateProfitCenterCommandHandler : IRequestHandler<CreateProfitCenterCommand, ProfitCenterDto>
{
    private readonly IProfitCenterRepository _profitCenterRepository;
    private readonly ILogger<CreateProfitCenterCommandHandler> _logger;

    public CreateProfitCenterCommandHandler(
        IProfitCenterRepository profitCenterRepository,
        ILogger<CreateProfitCenterCommandHandler> logger)
    {
        _profitCenterRepository = profitCenterRepository;
        _logger = logger;
    }

    public async Task<ProfitCenterDto> Handle(CreateProfitCenterCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _profitCenterRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A profit center with code '{codeUpper}' already exists.");

        var profitCenter = ProfitCenter.Create(request.CompanyId, request.Name, codeUpper, request.BusinessUnitId);
        _profitCenterRepository.Add(profitCenter);
        await _profitCenterRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profit center created: {ProfitCenterId} ({Code})", profitCenter.Id, profitCenter.Code);

        return MapToDto(profitCenter);
    }

    internal static ProfitCenterDto MapToDto(ProfitCenter p) =>
        new(p.Id, p.CompanyId, p.BusinessUnitId, p.Name, p.Code, p.IsActive, p.CreatedAt, p.UpdatedAt);
}

public class UpdateProfitCenterCommandHandler : IRequestHandler<UpdateProfitCenterCommand, ProfitCenterDto>
{
    private readonly IProfitCenterRepository _profitCenterRepository;
    private readonly ILogger<UpdateProfitCenterCommandHandler> _logger;

    public UpdateProfitCenterCommandHandler(
        IProfitCenterRepository profitCenterRepository,
        ILogger<UpdateProfitCenterCommandHandler> logger)
    {
        _profitCenterRepository = profitCenterRepository;
        _logger = logger;
    }

    public async Task<ProfitCenterDto> Handle(UpdateProfitCenterCommand request, CancellationToken cancellationToken)
    {
        var profitCenter = await _profitCenterRepository.GetByIdAsync(request.ProfitCenterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Profit center {request.ProfitCenterId} not found.");

        profitCenter.Update(request.Name, request.Code.ToUpperInvariant(), request.BusinessUnitId);
        _profitCenterRepository.Update(profitCenter);
        await _profitCenterRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profit center updated: {ProfitCenterId}", profitCenter.Id);

        return CreateProfitCenterCommandHandler.MapToDto(profitCenter);
    }
}

public class ActivateProfitCenterCommandHandler : IRequestHandler<ActivateProfitCenterCommand, ProfitCenterDto>
{
    private readonly IProfitCenterRepository _profitCenterRepository;
    private readonly ILogger<ActivateProfitCenterCommandHandler> _logger;

    public ActivateProfitCenterCommandHandler(
        IProfitCenterRepository profitCenterRepository,
        ILogger<ActivateProfitCenterCommandHandler> logger)
    {
        _profitCenterRepository = profitCenterRepository;
        _logger = logger;
    }

    public async Task<ProfitCenterDto> Handle(ActivateProfitCenterCommand request, CancellationToken cancellationToken)
    {
        var profitCenter = await _profitCenterRepository.GetByIdAsync(request.ProfitCenterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Profit center {request.ProfitCenterId} not found.");

        profitCenter.Activate();
        _profitCenterRepository.Update(profitCenter);
        await _profitCenterRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profit center activated: {ProfitCenterId}", profitCenter.Id);

        return CreateProfitCenterCommandHandler.MapToDto(profitCenter);
    }
}

public class DeactivateProfitCenterCommandHandler : IRequestHandler<DeactivateProfitCenterCommand, ProfitCenterDto>
{
    private readonly IProfitCenterRepository _profitCenterRepository;
    private readonly ILogger<DeactivateProfitCenterCommandHandler> _logger;

    public DeactivateProfitCenterCommandHandler(
        IProfitCenterRepository profitCenterRepository,
        ILogger<DeactivateProfitCenterCommandHandler> logger)
    {
        _profitCenterRepository = profitCenterRepository;
        _logger = logger;
    }

    public async Task<ProfitCenterDto> Handle(DeactivateProfitCenterCommand request, CancellationToken cancellationToken)
    {
        var profitCenter = await _profitCenterRepository.GetByIdAsync(request.ProfitCenterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Profit center {request.ProfitCenterId} not found.");

        profitCenter.Deactivate();
        _profitCenterRepository.Update(profitCenter);
        await _profitCenterRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profit center deactivated: {ProfitCenterId}", profitCenter.Id);

        return CreateProfitCenterCommandHandler.MapToDto(profitCenter);
    }
}

// ─── Organization Calendar Command Handlers ────────────────────────────────

public class CreateOrganizationCalendarCommandHandler : IRequestHandler<CreateOrganizationCalendarCommand, OrganizationCalendarDto>
{
    private readonly IOrganizationCalendarRepository _calendarRepository;
    private readonly ILogger<CreateOrganizationCalendarCommandHandler> _logger;

    public CreateOrganizationCalendarCommandHandler(
        IOrganizationCalendarRepository calendarRepository,
        ILogger<CreateOrganizationCalendarCommandHandler> logger)
    {
        _calendarRepository = calendarRepository;
        _logger = logger;
    }

    public async Task<OrganizationCalendarDto> Handle(CreateOrganizationCalendarCommand request, CancellationToken cancellationToken)
    {
        var calendar = OrganizationCalendar.Create(request.CompanyId, request.Year, request.Name, request.IsDefault);
        _calendarRepository.Add(calendar);
        await _calendarRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization calendar created: {CalendarId} ({Year})", calendar.Id, calendar.Year);

        return MapToDto(calendar);
    }

    internal static OrganizationCalendarDto MapToDto(OrganizationCalendar c) =>
        new(c.Id, c.CompanyId, c.Year, c.Name, c.IsDefault, c.CreatedAt, c.UpdatedAt);
}

public class UpdateOrganizationCalendarCommandHandler : IRequestHandler<UpdateOrganizationCalendarCommand, OrganizationCalendarDto>
{
    private readonly IOrganizationCalendarRepository _calendarRepository;
    private readonly ILogger<UpdateOrganizationCalendarCommandHandler> _logger;

    public UpdateOrganizationCalendarCommandHandler(
        IOrganizationCalendarRepository calendarRepository,
        ILogger<UpdateOrganizationCalendarCommandHandler> logger)
    {
        _calendarRepository = calendarRepository;
        _logger = logger;
    }

    public async Task<OrganizationCalendarDto> Handle(UpdateOrganizationCalendarCommand request, CancellationToken cancellationToken)
    {
        var calendar = await _calendarRepository.GetByIdAsync(request.CalendarId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization calendar {request.CalendarId} not found.");

        calendar.Update(request.Name, request.IsDefault);
        _calendarRepository.Update(calendar);
        await _calendarRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization calendar updated: {CalendarId}", calendar.Id);

        return CreateOrganizationCalendarCommandHandler.MapToDto(calendar);
    }
}

// ─── Holiday Command Handlers ──────────────────────────────────────────────

public class CreateHolidayCommandHandler : IRequestHandler<CreateHolidayCommand, HolidayDto>
{
    private readonly IHolidayRepository _holidayRepository;
    private readonly ILogger<CreateHolidayCommandHandler> _logger;

    public CreateHolidayCommandHandler(
        IHolidayRepository holidayRepository,
        ILogger<CreateHolidayCommandHandler> logger)
    {
        _holidayRepository = holidayRepository;
        _logger = logger;
    }

    public async Task<HolidayDto> Handle(CreateHolidayCommand request, CancellationToken cancellationToken)
    {
        var holiday = Holiday.Create(request.CalendarId, request.Date, request.Name, request.IsRecurringYearly);
        _holidayRepository.Add(holiday);
        await _holidayRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Holiday created: {HolidayId} ({Name})", holiday.Id, holiday.Name);

        return MapToDto(holiday);
    }

    internal static HolidayDto MapToDto(Holiday h) =>
        new(h.Id, h.CalendarId, h.Date, h.Name, h.IsRecurringYearly, h.CreatedAt, h.UpdatedAt);
}

public class UpdateHolidayCommandHandler : IRequestHandler<UpdateHolidayCommand, HolidayDto>
{
    private readonly IHolidayRepository _holidayRepository;
    private readonly ILogger<UpdateHolidayCommandHandler> _logger;

    public UpdateHolidayCommandHandler(
        IHolidayRepository holidayRepository,
        ILogger<UpdateHolidayCommandHandler> logger)
    {
        _holidayRepository = holidayRepository;
        _logger = logger;
    }

    public async Task<HolidayDto> Handle(UpdateHolidayCommand request, CancellationToken cancellationToken)
    {
        var holiday = await _holidayRepository.GetByIdAsync(request.HolidayId, cancellationToken)
            ?? throw new KeyNotFoundException($"Holiday {request.HolidayId} not found.");

        holiday.Update(request.Date, request.Name, request.IsRecurringYearly);
        _holidayRepository.Update(holiday);
        await _holidayRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Holiday updated: {HolidayId}", holiday.Id);

        return CreateHolidayCommandHandler.MapToDto(holiday);
    }
}

// ─── Employee Team Assignment Command Handlers ─────────────────────────────

public class CreateEmployeeTeamAssignmentCommandHandler : IRequestHandler<CreateEmployeeTeamAssignmentCommand, EmployeeTeamAssignmentDto>
{
    private readonly IEmployeeTeamAssignmentRepository _assignmentRepository;
    private readonly ILogger<CreateEmployeeTeamAssignmentCommandHandler> _logger;

    public CreateEmployeeTeamAssignmentCommandHandler(
        IEmployeeTeamAssignmentRepository assignmentRepository,
        ILogger<CreateEmployeeTeamAssignmentCommandHandler> logger)
    {
        _assignmentRepository = assignmentRepository;
        _logger = logger;
    }

    public async Task<EmployeeTeamAssignmentDto> Handle(CreateEmployeeTeamAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = EmployeeTeamAssignment.Create(request.EmployeeId, request.TeamId, request.EffectiveFrom, request.PositionId);
        _assignmentRepository.Add(assignment);
        await _assignmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee team assignment created: {AssignmentId} (Employee {EmployeeId} -> Team {TeamId})",
            assignment.Id, assignment.EmployeeId, assignment.TeamId);

        return MapToDto(assignment);
    }

    internal static EmployeeTeamAssignmentDto MapToDto(EmployeeTeamAssignment a) =>
        new(a.Id, a.EmployeeId, a.TeamId, a.PositionId, a.EffectiveFrom, a.EffectiveTo, a.CreatedAt, a.UpdatedAt);
}

public class EndEmployeeTeamAssignmentCommandHandler : IRequestHandler<EndEmployeeTeamAssignmentCommand, EmployeeTeamAssignmentDto>
{
    private readonly IEmployeeTeamAssignmentRepository _assignmentRepository;
    private readonly ILogger<EndEmployeeTeamAssignmentCommandHandler> _logger;

    public EndEmployeeTeamAssignmentCommandHandler(
        IEmployeeTeamAssignmentRepository assignmentRepository,
        ILogger<EndEmployeeTeamAssignmentCommandHandler> logger)
    {
        _assignmentRepository = assignmentRepository;
        _logger = logger;
    }

    public async Task<EmployeeTeamAssignmentDto> Handle(EndEmployeeTeamAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee team assignment {request.AssignmentId} not found.");

        assignment.End(request.EffectiveTo);
        _assignmentRepository.Update(assignment);
        await _assignmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee team assignment ended: {AssignmentId}", assignment.Id);

        return CreateEmployeeTeamAssignmentCommandHandler.MapToDto(assignment);
    }
}

// ─── Currency Command Handlers ─────────────────────────────────────────────

public class CreateCurrencyCommandHandler : IRequestHandler<CreateCurrencyCommand, CurrencyDto>
{
    private readonly ICurrencyRepository _currencyRepository;
    private readonly ILogger<CreateCurrencyCommandHandler> _logger;

    public CreateCurrencyCommandHandler(
        ICurrencyRepository currencyRepository,
        ILogger<CreateCurrencyCommandHandler> logger)
    {
        _currencyRepository = currencyRepository;
        _logger = logger;
    }

    public async Task<CurrencyDto> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _currencyRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A currency with code '{codeUpper}' already exists.");

        var currency = Currency.Create(codeUpper, request.Name, request.Symbol, request.DecimalPlaces);
        _currencyRepository.Add(currency);
        await _currencyRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Currency created: {CurrencyId} ({Code})", currency.Id, currency.Code);

        return MapToDto(currency);
    }

    internal static CurrencyDto MapToDto(Currency c) =>
        new(c.Id, c.Code, c.Name, c.Symbol, c.DecimalPlaces, c.IsActive, c.CreatedAt, c.UpdatedAt);
}

public class UpdateCurrencyCommandHandler : IRequestHandler<UpdateCurrencyCommand, CurrencyDto>
{
    private readonly ICurrencyRepository _currencyRepository;
    private readonly ILogger<UpdateCurrencyCommandHandler> _logger;

    public UpdateCurrencyCommandHandler(
        ICurrencyRepository currencyRepository,
        ILogger<UpdateCurrencyCommandHandler> logger)
    {
        _currencyRepository = currencyRepository;
        _logger = logger;
    }

    public async Task<CurrencyDto> Handle(UpdateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currency = await _currencyRepository.GetByIdAsync(request.CurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Currency {request.CurrencyId} not found.");

        currency.Update(request.Name, request.Symbol, request.DecimalPlaces);
        _currencyRepository.Update(currency);
        await _currencyRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Currency updated: {CurrencyId}", currency.Id);

        return CreateCurrencyCommandHandler.MapToDto(currency);
    }
}

public class ActivateCurrencyCommandHandler : IRequestHandler<ActivateCurrencyCommand, CurrencyDto>
{
    private readonly ICurrencyRepository _currencyRepository;
    private readonly ILogger<ActivateCurrencyCommandHandler> _logger;

    public ActivateCurrencyCommandHandler(
        ICurrencyRepository currencyRepository,
        ILogger<ActivateCurrencyCommandHandler> logger)
    {
        _currencyRepository = currencyRepository;
        _logger = logger;
    }

    public async Task<CurrencyDto> Handle(ActivateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currency = await _currencyRepository.GetByIdAsync(request.CurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Currency {request.CurrencyId} not found.");

        currency.Activate();
        _currencyRepository.Update(currency);
        await _currencyRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Currency activated: {CurrencyId}", currency.Id);

        return CreateCurrencyCommandHandler.MapToDto(currency);
    }
}

public class DeactivateCurrencyCommandHandler : IRequestHandler<DeactivateCurrencyCommand, CurrencyDto>
{
    private readonly ICurrencyRepository _currencyRepository;
    private readonly ILogger<DeactivateCurrencyCommandHandler> _logger;

    public DeactivateCurrencyCommandHandler(
        ICurrencyRepository currencyRepository,
        ILogger<DeactivateCurrencyCommandHandler> logger)
    {
        _currencyRepository = currencyRepository;
        _logger = logger;
    }

    public async Task<CurrencyDto> Handle(DeactivateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currency = await _currencyRepository.GetByIdAsync(request.CurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Currency {request.CurrencyId} not found.");

        currency.Deactivate();
        _currencyRepository.Update(currency);
        await _currencyRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Currency deactivated: {CurrencyId}", currency.Id);

        return CreateCurrencyCommandHandler.MapToDto(currency);
    }
}

// ─── Exchange Rate Command Handlers ────────────────────────────────────────

public class CreateExchangeRateCommandHandler : IRequestHandler<CreateExchangeRateCommand, ExchangeRateDto>
{
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly ILogger<CreateExchangeRateCommandHandler> _logger;

    public CreateExchangeRateCommandHandler(
        IExchangeRateRepository exchangeRateRepository,
        ICurrencyRepository currencyRepository,
        ILogger<CreateExchangeRateCommandHandler> logger)
    {
        _exchangeRateRepository = exchangeRateRepository;
        _currencyRepository = currencyRepository;
        _logger = logger;
    }

    public async Task<ExchangeRateDto> Handle(CreateExchangeRateCommand request, CancellationToken cancellationToken)
    {
        _ = await _currencyRepository.GetByIdAsync(request.FromCurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Currency {request.FromCurrencyId} not found.");

        _ = await _currencyRepository.GetByIdAsync(request.ToCurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Currency {request.ToCurrencyId} not found.");

        var exchangeRate = ExchangeRate.Create(request.FromCurrencyId, request.ToCurrencyId, request.Rate, request.EffectiveDate);
        _exchangeRateRepository.Add(exchangeRate);
        await _exchangeRateRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Exchange rate created: {ExchangeRateId} ({FromCurrencyId} -> {ToCurrencyId})",
            exchangeRate.Id, exchangeRate.FromCurrencyId, exchangeRate.ToCurrencyId);

        return MapToDto(exchangeRate);
    }

    internal static ExchangeRateDto MapToDto(ExchangeRate e) =>
        new(e.Id, e.FromCurrencyId, e.ToCurrencyId, e.Rate, e.EffectiveDate, e.CreatedAt, e.UpdatedAt);
}

public class UpdateExchangeRateCommandHandler : IRequestHandler<UpdateExchangeRateCommand, ExchangeRateDto>
{
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly ILogger<UpdateExchangeRateCommandHandler> _logger;

    public UpdateExchangeRateCommandHandler(
        IExchangeRateRepository exchangeRateRepository,
        ILogger<UpdateExchangeRateCommandHandler> logger)
    {
        _exchangeRateRepository = exchangeRateRepository;
        _logger = logger;
    }

    public async Task<ExchangeRateDto> Handle(UpdateExchangeRateCommand request, CancellationToken cancellationToken)
    {
        var exchangeRate = await _exchangeRateRepository.GetByIdAsync(request.ExchangeRateId, cancellationToken)
            ?? throw new KeyNotFoundException($"Exchange rate {request.ExchangeRateId} not found.");

        exchangeRate.Update(request.Rate, request.EffectiveDate);
        _exchangeRateRepository.Update(exchangeRate);
        await _exchangeRateRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Exchange rate updated: {ExchangeRateId}", exchangeRate.Id);

        return CreateExchangeRateCommandHandler.MapToDto(exchangeRate);
    }
}

// ─── Payment Term Command Handlers ─────────────────────────────────────────

public class CreatePaymentTermCommandHandler : IRequestHandler<CreatePaymentTermCommand, PaymentTermDto>
{
    private readonly IPaymentTermRepository _paymentTermRepository;
    private readonly ILogger<CreatePaymentTermCommandHandler> _logger;

    public CreatePaymentTermCommandHandler(
        IPaymentTermRepository paymentTermRepository,
        ILogger<CreatePaymentTermCommandHandler> logger)
    {
        _paymentTermRepository = paymentTermRepository;
        _logger = logger;
    }

    public async Task<PaymentTermDto> Handle(CreatePaymentTermCommand request, CancellationToken cancellationToken)
    {
        var codeUpper = request.Code.ToUpperInvariant();

        if (await _paymentTermRepository.ExistsByCodeAsync(codeUpper, cancellationToken))
            throw new InvalidOperationException($"A payment term with code '{codeUpper}' already exists.");

        var paymentTerm = PaymentTerm.Create(codeUpper, request.Name, request.NetDays, request.DepositPercent);
        _paymentTermRepository.Add(paymentTerm);
        await _paymentTermRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment term created: {PaymentTermId} ({Code})", paymentTerm.Id, paymentTerm.Code);

        return MapToDto(paymentTerm);
    }

    internal static PaymentTermDto MapToDto(PaymentTerm p) =>
        new(p.Id, p.Code, p.Name, p.NetDays, p.DepositPercent, p.IsActive, p.CreatedAt, p.UpdatedAt);
}

public class UpdatePaymentTermCommandHandler : IRequestHandler<UpdatePaymentTermCommand, PaymentTermDto>
{
    private readonly IPaymentTermRepository _paymentTermRepository;
    private readonly ILogger<UpdatePaymentTermCommandHandler> _logger;

    public UpdatePaymentTermCommandHandler(
        IPaymentTermRepository paymentTermRepository,
        ILogger<UpdatePaymentTermCommandHandler> logger)
    {
        _paymentTermRepository = paymentTermRepository;
        _logger = logger;
    }

    public async Task<PaymentTermDto> Handle(UpdatePaymentTermCommand request, CancellationToken cancellationToken)
    {
        var paymentTerm = await _paymentTermRepository.GetByIdAsync(request.PaymentTermId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment term {request.PaymentTermId} not found.");

        paymentTerm.Update(request.Name, request.NetDays, request.DepositPercent);
        _paymentTermRepository.Update(paymentTerm);
        await _paymentTermRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment term updated: {PaymentTermId}", paymentTerm.Id);

        return CreatePaymentTermCommandHandler.MapToDto(paymentTerm);
    }
}

public class ActivatePaymentTermCommandHandler : IRequestHandler<ActivatePaymentTermCommand, PaymentTermDto>
{
    private readonly IPaymentTermRepository _paymentTermRepository;
    private readonly ILogger<ActivatePaymentTermCommandHandler> _logger;

    public ActivatePaymentTermCommandHandler(
        IPaymentTermRepository paymentTermRepository,
        ILogger<ActivatePaymentTermCommandHandler> logger)
    {
        _paymentTermRepository = paymentTermRepository;
        _logger = logger;
    }

    public async Task<PaymentTermDto> Handle(ActivatePaymentTermCommand request, CancellationToken cancellationToken)
    {
        var paymentTerm = await _paymentTermRepository.GetByIdAsync(request.PaymentTermId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment term {request.PaymentTermId} not found.");

        paymentTerm.Activate();
        _paymentTermRepository.Update(paymentTerm);
        await _paymentTermRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment term activated: {PaymentTermId}", paymentTerm.Id);

        return CreatePaymentTermCommandHandler.MapToDto(paymentTerm);
    }
}

public class DeactivatePaymentTermCommandHandler : IRequestHandler<DeactivatePaymentTermCommand, PaymentTermDto>
{
    private readonly IPaymentTermRepository _paymentTermRepository;
    private readonly ILogger<DeactivatePaymentTermCommandHandler> _logger;

    public DeactivatePaymentTermCommandHandler(
        IPaymentTermRepository paymentTermRepository,
        ILogger<DeactivatePaymentTermCommandHandler> logger)
    {
        _paymentTermRepository = paymentTermRepository;
        _logger = logger;
    }

    public async Task<PaymentTermDto> Handle(DeactivatePaymentTermCommand request, CancellationToken cancellationToken)
    {
        var paymentTerm = await _paymentTermRepository.GetByIdAsync(request.PaymentTermId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment term {request.PaymentTermId} not found.");

        paymentTerm.Deactivate();
        _paymentTermRepository.Update(paymentTerm);
        await _paymentTermRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment term deactivated: {PaymentTermId}", paymentTerm.Id);

        return CreatePaymentTermCommandHandler.MapToDto(paymentTerm);
    }
}
