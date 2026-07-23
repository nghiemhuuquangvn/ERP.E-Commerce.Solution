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

public record SetDepartmentDivisionCommand(
    Guid DepartmentId,
    Guid? DivisionId) : IRequest<DepartmentDto>;

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

// ─── Company Commands ──────────────────────────────────────────────────────

public record CreateCompanyCommand(
    string Name,
    string Code,
    string? TaxCode,
    string? LegalAddress,
    string? CurrencyCode) : IRequest<CompanyDto>;

public record UpdateCompanyCommand(
    Guid CompanyId,
    string Name,
    string Code,
    string? TaxCode,
    string? LegalAddress,
    string? CurrencyCode) : IRequest<CompanyDto>;

public record ActivateCompanyCommand(Guid CompanyId) : IRequest<CompanyDto>;

public record DeactivateCompanyCommand(Guid CompanyId) : IRequest<CompanyDto>;

// ─── Branch Commands ───────────────────────────────────────────────────────

public record CreateBranchCommand(
    Guid CompanyId,
    string Name,
    string Code,
    string? Address,
    Guid? WardId,
    Guid? ManagerEmployeeId) : IRequest<BranchDto>;

public record UpdateBranchCommand(
    Guid BranchId,
    string Name,
    string Code,
    string? Address,
    Guid? WardId,
    Guid? ManagerEmployeeId) : IRequest<BranchDto>;

public record ActivateBranchCommand(Guid BranchId) : IRequest<BranchDto>;

public record DeactivateBranchCommand(Guid BranchId) : IRequest<BranchDto>;

// ─── Division Commands ─────────────────────────────────────────────────────

public record CreateDivisionCommand(
    Guid BranchId,
    string Name,
    string Code,
    Guid? ParentDivisionId) : IRequest<DivisionDto>;

public record UpdateDivisionCommand(
    Guid DivisionId,
    string Name,
    string Code,
    Guid? ParentDivisionId) : IRequest<DivisionDto>;

public record ActivateDivisionCommand(Guid DivisionId) : IRequest<DivisionDto>;

public record DeactivateDivisionCommand(Guid DivisionId) : IRequest<DivisionDto>;

// ─── Team Commands ─────────────────────────────────────────────────────────

public record CreateTeamCommand(
    Guid DepartmentId,
    string Name,
    string Code,
    Guid? LeaderEmployeeId) : IRequest<TeamDto>;

public record UpdateTeamCommand(
    Guid TeamId,
    string Name,
    string Code,
    Guid? LeaderEmployeeId) : IRequest<TeamDto>;

public record ActivateTeamCommand(Guid TeamId) : IRequest<TeamDto>;

public record DeactivateTeamCommand(Guid TeamId) : IRequest<TeamDto>;

// ─── Business Unit Commands ────────────────────────────────────────────────

public record CreateBusinessUnitCommand(
    Guid CompanyId,
    string Name,
    string Code) : IRequest<BusinessUnitDto>;

public record UpdateBusinessUnitCommand(
    Guid BusinessUnitId,
    string Name,
    string Code) : IRequest<BusinessUnitDto>;

public record ActivateBusinessUnitCommand(Guid BusinessUnitId) : IRequest<BusinessUnitDto>;

public record DeactivateBusinessUnitCommand(Guid BusinessUnitId) : IRequest<BusinessUnitDto>;

// ─── Cost Center Commands ──────────────────────────────────────────────────

public record CreateCostCenterCommand(
    Guid CompanyId,
    string Name,
    string Code,
    decimal BudgetAmount,
    Guid? DepartmentId = null) : IRequest<CostCenterDto>;

public record UpdateCostCenterCommand(
    Guid CostCenterId,
    string Name,
    string Code,
    decimal BudgetAmount,
    Guid? DepartmentId) : IRequest<CostCenterDto>;

public record ActivateCostCenterCommand(Guid CostCenterId) : IRequest<CostCenterDto>;

public record DeactivateCostCenterCommand(Guid CostCenterId) : IRequest<CostCenterDto>;

// ─── Profit Center Commands ────────────────────────────────────────────────

public record CreateProfitCenterCommand(
    Guid CompanyId,
    string Name,
    string Code,
    Guid? BusinessUnitId = null) : IRequest<ProfitCenterDto>;

public record UpdateProfitCenterCommand(
    Guid ProfitCenterId,
    string Name,
    string Code,
    Guid? BusinessUnitId) : IRequest<ProfitCenterDto>;

public record ActivateProfitCenterCommand(Guid ProfitCenterId) : IRequest<ProfitCenterDto>;

public record DeactivateProfitCenterCommand(Guid ProfitCenterId) : IRequest<ProfitCenterDto>;

// ─── Organization Calendar Commands ────────────────────────────────────────

public record CreateOrganizationCalendarCommand(
    Guid CompanyId,
    int Year,
    string Name,
    bool IsDefault = false) : IRequest<OrganizationCalendarDto>;

public record UpdateOrganizationCalendarCommand(
    Guid CalendarId,
    string Name,
    bool IsDefault) : IRequest<OrganizationCalendarDto>;

// ─── Holiday Commands ──────────────────────────────────────────────────────

public record CreateHolidayCommand(
    Guid CalendarId,
    DateTime Date,
    string Name,
    bool IsRecurringYearly = false) : IRequest<HolidayDto>;

public record UpdateHolidayCommand(
    Guid HolidayId,
    DateTime Date,
    string Name,
    bool IsRecurringYearly) : IRequest<HolidayDto>;

// ─── Employee Team Assignment Commands ─────────────────────────────────────

public record CreateEmployeeTeamAssignmentCommand(
    Guid EmployeeId,
    Guid TeamId,
    DateTime EffectiveFrom,
    Guid? PositionId = null) : IRequest<EmployeeTeamAssignmentDto>;

public record EndEmployeeTeamAssignmentCommand(
    Guid AssignmentId,
    DateTime EffectiveTo) : IRequest<EmployeeTeamAssignmentDto>;

// ─── Currency Commands ─────────────────────────────────────────────────────

public record CreateCurrencyCommand(
    string Code,
    string Name,
    string Symbol,
    int DecimalPlaces = 2) : IRequest<CurrencyDto>;

public record UpdateCurrencyCommand(
    Guid CurrencyId,
    string Name,
    string Symbol,
    int DecimalPlaces) : IRequest<CurrencyDto>;

public record ActivateCurrencyCommand(Guid CurrencyId) : IRequest<CurrencyDto>;

public record DeactivateCurrencyCommand(Guid CurrencyId) : IRequest<CurrencyDto>;

// ─── Exchange Rate Commands ─────────────────────────────────────────────────

public record CreateExchangeRateCommand(
    Guid FromCurrencyId,
    Guid ToCurrencyId,
    decimal Rate,
    DateTime EffectiveDate) : IRequest<ExchangeRateDto>;

public record UpdateExchangeRateCommand(
    Guid ExchangeRateId,
    decimal Rate,
    DateTime EffectiveDate) : IRequest<ExchangeRateDto>;

// ─── Payment Term Commands ──────────────────────────────────────────────────

public record CreatePaymentTermCommand(
    string Code,
    string Name,
    int NetDays,
    decimal? DepositPercent = null) : IRequest<PaymentTermDto>;

public record UpdatePaymentTermCommand(
    Guid PaymentTermId,
    string Name,
    int NetDays,
    decimal? DepositPercent) : IRequest<PaymentTermDto>;

public record ActivatePaymentTermCommand(Guid PaymentTermId) : IRequest<PaymentTermDto>;

public record DeactivatePaymentTermCommand(Guid PaymentTermId) : IRequest<PaymentTermDto>;
