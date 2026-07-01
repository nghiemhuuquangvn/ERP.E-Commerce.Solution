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
        new(d.Id, d.DepartmentName, d.Code, d.ManagerId, d.ParentDepartmentId, languageCode, d.CreatedAt, d.UpdatedAt);
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
