using ERP.MST.Application.DTOs;
using MediatR;

namespace ERP.MST.Application.Commands;

// ─── Department Commands ───────────────────────────────────────────────────

public record CreateDepartmentCommand(
    string Name,
    string Code,
    Guid? ManagerId,
    Guid? ParentDepartmentId = null) : IRequest<DepartmentDto>;

public record UpdateDepartmentCommand(
    Guid DepartmentId,
    string Name,
    string Code,
    Guid? ManagerId) : IRequest<DepartmentDto>;

// ─── Position Commands ─────────────────────────────────────────────────────

public record CreatePositionCommand(
    string Title,
    string Code,
    string? Description,
    Guid? DepartmentId) : IRequest<PositionDto>;

public record UpdatePositionCommand(
    Guid PositionId,
    string Title,
    string? Description) : IRequest<PositionDto>;

public record DeactivatePositionCommand(
    Guid PositionId) : IRequest<PositionDto>;

public record ActivatePositionCommand(
    Guid PositionId) : IRequest<PositionDto>;

// ─── Employee Commands ─────────────────────────────────────────────────────

public record HireEmployeeCommand(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    Guid DepartmentId,
    DateTime DateOfHire,
    int EmploymentType,
    Guid? PositionId = null) : IRequest<EmployeeDto>;

public record TerminateEmployeeCommand(
    Guid EmployeeId,
    string Reason) : IRequest<EmployeeDto>;

public record UpdateEmployeeProfileCommand(
    Guid EmployeeId,
    string? Email,
    string? Phone) : IRequest<EmployeeDto>;

public record TransferEmployeeCommand(
    Guid EmployeeId,
    Guid NewDepartmentId) : IRequest<EmployeeDto>;

// Driven by HRM's LeaveApprovedEvent (via "hrm-topic") — closes the loop so the
// canonical Employee status reflects leave HRM approved locally.
public record SetEmployeeOnLeaveCommand(Guid EmployeeId) : IRequest<Unit>;

// ─── Unit Of Measure Commands ──────────────────────────────────────────────

public record CreateUnitOfMeasureCommand(
    string Code,
    string Name,
    string Symbol,
    int UnitType,
    Guid? BaseUnitId,
    decimal ConversionFactor = 1m) : IRequest<UnitOfMeasureDto>;

public record UpdateUnitOfMeasureCommand(
    Guid UnitOfMeasureId,
    string Name,
    string Symbol,
    decimal ConversionFactor) : IRequest<UnitOfMeasureDto>;

// ─── Geography Commands ────────────────────────────────────────────────────

public record CreateCountryCommand(
    string Code,
    string Name,
    string? PhoneCode) : IRequest<CountryDto>;

public record CreateProvinceCommand(
    Guid CountryId,
    string Code,
    string Name) : IRequest<ProvinceDto>;

public record CreateDistrictCommand(
    Guid ProvinceId,
    string Code,
    string Name) : IRequest<DistrictDto>;

public record CreateWardCommand(
    Guid DistrictId,
    string Code,
    string Name) : IRequest<WardDto>;

// ─── Language Commands ─────────────────────────────────────────────────────

public record CreateLanguageCommand(
    string Code,
    string Name,
    string NativeName,
    bool IsDefault,
    int SortOrder) : IRequest<SupportedLanguageDto>;
