using ERP.HRM.Application.DTOs;
using MediatR;

namespace ERP.HRM.Application.Commands;

// ─── DTOs ─────────────────────────────────────────────────────────────────

public record CompensationDto(
    Guid Id,
    Guid EmployeeId,
    DateTime EffectiveFrom,
    decimal BaseSalary,
    decimal TaxableAllowance,
    decimal ExemptAllowance,
    decimal? InsuranceSalary,
    bool InsuranceEnabled,
    string? TaxCode,
    string? InsuranceNumber);

/// <summary>One row of the salary-profile list: an employee and the salary terms in force today (null = no profile yet).</summary>
public record SalaryProfileDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    int DependentCount,
    CompensationDto? Current);

public record DependentDto(
    Guid Id,
    Guid EmployeeId,
    string FullName,
    string Relationship,
    string? TaxCode,
    DateTime FromMonth,
    DateTime? ToMonth);

public record PitBracketDto(decimal? UpTo, decimal Rate);

public record PayrollRateSetDto(
    Guid Id,
    DateTime EffectiveFrom,
    string Name,
    decimal SocialEmployeeRate,
    decimal HealthEmployeeRate,
    decimal UnemploymentEmployeeRate,
    decimal SocialEmployerRate,
    decimal HealthEmployerRate,
    decimal UnemploymentEmployerRate,
    decimal PersonalDeduction,
    decimal DependentDeduction,
    decimal InsuranceSalaryCap,
    decimal UnemploymentSalaryCap,
    decimal AnnualLeaveDays,
    bool IsConfirmed,
    List<PitBracketDto> Brackets);

public record LeaveBalanceDto(Guid EmployeeId, int Year, string LeaveType, decimal TotalDays, decimal UsedDays, decimal RemainingDays);

public record SkippedEmployeeDto(Guid EmployeeId, string EmployeeNumber, string FullName, string Reason);

public record ProcessPayrollResultDto(List<PaySlipDto> Created, List<SkippedEmployeeDto> Skipped);

// ─── Commands / queries ───────────────────────────────────────────────────

/// <summary>Salary terms from a date. Saving the same effective date again replaces that row.</summary>
public record SaveCompensationCommand(
    Guid EmployeeId,
    DateTime EffectiveFrom,
    decimal BaseSalary,
    decimal TaxableAllowance,
    decimal ExemptAllowance,
    decimal? InsuranceSalary,
    bool InsuranceEnabled,
    string? TaxCode,
    string? InsuranceNumber) : IRequest<CompensationDto>;

public record ListCompensationsQuery(Guid EmployeeId) : IRequest<List<CompensationDto>>;

public record ListSalaryProfilesQuery(string? Search = null, int Page = 1, int PageSize = 20) : IRequest<PagedResult<SalaryProfileDto>>;

public record AddDependentCommand(
    Guid EmployeeId,
    string FullName,
    string Relationship,
    string? TaxCode,
    DateTime FromMonth,
    DateTime? ToMonth) : IRequest<DependentDto>;

public record RemoveDependentCommand(Guid DependentId) : IRequest;

public record ListDependentsQuery(Guid EmployeeId) : IRequest<List<DependentDto>>;

public record SaveRateSetCommand(
    Guid? Id,
    DateTime EffectiveFrom,
    string Name,
    decimal SocialEmployeeRate,
    decimal HealthEmployeeRate,
    decimal UnemploymentEmployeeRate,
    decimal SocialEmployerRate,
    decimal HealthEmployerRate,
    decimal UnemploymentEmployerRate,
    decimal PersonalDeduction,
    decimal DependentDeduction,
    decimal InsuranceSalaryCap,
    decimal UnemploymentSalaryCap,
    decimal AnnualLeaveDays,
    bool IsConfirmed,
    List<PitBracketDto> Brackets) : IRequest<PayrollRateSetDto>;

public record ListRateSetsQuery : IRequest<List<PayrollRateSetDto>>;

public record ListLeaveBalancesQuery(Guid? EmployeeId, int Year) : IRequest<List<LeaveBalanceDto>>;
