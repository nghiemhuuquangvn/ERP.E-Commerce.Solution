using ERP.MST.Application.Commands;
using FluentValidation;

namespace ERP.MST.Application.Validators;

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(200).WithMessage("Department name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Department code is required.")
            .MaximumLength(20).WithMessage("Department code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Department code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("Department ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(200).WithMessage("Department name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Department code is required.")
            .MaximumLength(20).WithMessage("Department code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Department code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Position title is required.")
            .MaximumLength(200).WithMessage("Position title must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Position code is required.")
            .MaximumLength(20).WithMessage("Position code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Position code must contain only uppercase letters, digits, underscores, and hyphens.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}

public class UpdatePositionCommandValidator : AbstractValidator<UpdatePositionCommand>
{
    public UpdatePositionCommandValidator()
    {
        RuleFor(x => x.PositionId)
            .NotEmpty().WithMessage("Position ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Position title is required.")
            .MaximumLength(200).WithMessage("Position title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}

public class HireEmployeeCommandValidator : AbstractValidator<HireEmployeeCommand>
{
    public HireEmployeeCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone must not exceed 20 characters.")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("Department is required.");

        RuleFor(x => x.DateOfHire)
            .NotEmpty().WithMessage("Date of hire is required.")
            .LessThanOrEqualTo(DateTime.UtcNow.AddDays(30))
            .WithMessage("Date of hire cannot be more than 30 days in the future.");

        RuleFor(x => x.EmploymentType)
            .InclusiveBetween(0, 2).WithMessage("Employment type must be 0 (FullTime), 1 (PartTime), or 2 (Contract).");
    }
}

public class TerminateEmployeeCommandValidator : AbstractValidator<TerminateEmployeeCommand>
{
    public TerminateEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Termination reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

public class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Unit of measure code is required.")
            .MaximumLength(20).WithMessage("Unit of measure code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Unit of measure name is required.")
            .MaximumLength(100).WithMessage("Unit of measure name must not exceed 100 characters.");

        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Unit of measure symbol is required.")
            .MaximumLength(10).WithMessage("Unit of measure symbol must not exceed 10 characters.");

        RuleFor(x => x.UnitType)
            .InclusiveBetween(0, 4).WithMessage("Unit type must be between 0 (Weight) and 4 (Area).");

        RuleFor(x => x.ConversionFactor)
            .GreaterThan(0).WithMessage("Conversion factor must be greater than 0.");
    }
}

public class CreateCountryCommandValidator : AbstractValidator<CreateCountryCommand>
{
    public CreateCountryCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Country code is required.")
            .Length(2).WithMessage("Country code must be a 2-letter ISO alpha-2 code.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Country name is required.")
            .MaximumLength(100).WithMessage("Country name must not exceed 100 characters.");
    }
}

public class CreateProvinceCommandValidator : AbstractValidator<CreateProvinceCommand>
{
    public CreateProvinceCommandValidator()
    {
        RuleFor(x => x.CountryId)
            .NotEmpty().WithMessage("Country ID is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Province code is required.")
            .MaximumLength(20).WithMessage("Province code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Province name is required.")
            .MaximumLength(100).WithMessage("Province name must not exceed 100 characters.");
    }
}

public class CreateDistrictCommandValidator : AbstractValidator<CreateDistrictCommand>
{
    public CreateDistrictCommandValidator()
    {
        RuleFor(x => x.ProvinceId)
            .NotEmpty().WithMessage("Province ID is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("District code is required.")
            .MaximumLength(20).WithMessage("District code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("District name is required.")
            .MaximumLength(100).WithMessage("District name must not exceed 100 characters.");
    }
}

public class CreateWardCommandValidator : AbstractValidator<CreateWardCommand>
{
    public CreateWardCommandValidator()
    {
        RuleFor(x => x.DistrictId)
            .NotEmpty().WithMessage("District ID is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Ward code is required.")
            .MaximumLength(20).WithMessage("Ward code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ward name is required.")
            .MaximumLength(100).WithMessage("Ward name must not exceed 100 characters.");
    }
}

public class CreateLanguageCommandValidator : AbstractValidator<CreateLanguageCommand>
{
    public CreateLanguageCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Language code is required.")
            .MaximumLength(10).WithMessage("Language code must not exceed 10 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Language name is required.")
            .MaximumLength(100).WithMessage("Language name must not exceed 100 characters.");

        RuleFor(x => x.NativeName)
            .NotEmpty().WithMessage("Language native name is required.")
            .MaximumLength(100).WithMessage("Language native name must not exceed 100 characters.");
    }
}

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Company code is required.")
            .MaximumLength(20).WithMessage("Company code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Company code must contain only uppercase letters, digits, underscores, and hyphens.");

        RuleFor(x => x.TaxCode)
            .MaximumLength(50).WithMessage("Tax code must not exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.TaxCode));

        RuleFor(x => x.LegalAddress)
            .MaximumLength(500).WithMessage("Legal address must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.LegalAddress));

        RuleFor(x => x.CurrencyCode)
            .Length(3).WithMessage("Currency code must be a 3-letter ISO code.")
            .When(x => !string.IsNullOrEmpty(x.CurrencyCode));
    }
}

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Company code is required.")
            .MaximumLength(20).WithMessage("Company code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Company code must contain only uppercase letters, digits, underscores, and hyphens.");

        RuleFor(x => x.TaxCode)
            .MaximumLength(50).WithMessage("Tax code must not exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.TaxCode));

        RuleFor(x => x.LegalAddress)
            .MaximumLength(500).WithMessage("Legal address must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.LegalAddress));

        RuleFor(x => x.CurrencyCode)
            .Length(3).WithMessage("Currency code must be a 3-letter ISO code.")
            .When(x => !string.IsNullOrEmpty(x.CurrencyCode));
    }
}

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Branch name is required.")
            .MaximumLength(200).WithMessage("Branch name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Branch code is required.")
            .MaximumLength(20).WithMessage("Branch code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Branch code must contain only uppercase letters, digits, underscores, and hyphens.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Address));
    }
}

public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Branch name is required.")
            .MaximumLength(200).WithMessage("Branch name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Branch code is required.")
            .MaximumLength(20).WithMessage("Branch code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Branch code must contain only uppercase letters, digits, underscores, and hyphens.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Address));
    }
}

public class CreateDivisionCommandValidator : AbstractValidator<CreateDivisionCommand>
{
    public CreateDivisionCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Division name is required.")
            .MaximumLength(200).WithMessage("Division name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Division code is required.")
            .MaximumLength(20).WithMessage("Division code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Division code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class UpdateDivisionCommandValidator : AbstractValidator<UpdateDivisionCommand>
{
    public UpdateDivisionCommandValidator()
    {
        RuleFor(x => x.DivisionId)
            .NotEmpty().WithMessage("Division ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Division name is required.")
            .MaximumLength(200).WithMessage("Division name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Division code is required.")
            .MaximumLength(20).WithMessage("Division code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Division code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("Department ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Team name is required.")
            .MaximumLength(200).WithMessage("Team name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Team code is required.")
            .MaximumLength(20).WithMessage("Team code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Team code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("Team ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Team name is required.")
            .MaximumLength(200).WithMessage("Team name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Team code is required.")
            .MaximumLength(20).WithMessage("Team code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Team code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class CreateBusinessUnitCommandValidator : AbstractValidator<CreateBusinessUnitCommand>
{
    public CreateBusinessUnitCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Business unit name is required.")
            .MaximumLength(200).WithMessage("Business unit name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Business unit code is required.")
            .MaximumLength(20).WithMessage("Business unit code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Business unit code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class UpdateBusinessUnitCommandValidator : AbstractValidator<UpdateBusinessUnitCommand>
{
    public UpdateBusinessUnitCommandValidator()
    {
        RuleFor(x => x.BusinessUnitId)
            .NotEmpty().WithMessage("Business unit ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Business unit name is required.")
            .MaximumLength(200).WithMessage("Business unit name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Business unit code is required.")
            .MaximumLength(20).WithMessage("Business unit code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Business unit code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class CreateCostCenterCommandValidator : AbstractValidator<CreateCostCenterCommand>
{
    public CreateCostCenterCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Cost center name is required.")
            .MaximumLength(200).WithMessage("Cost center name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Cost center code is required.")
            .MaximumLength(20).WithMessage("Cost center code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Cost center code must contain only uppercase letters, digits, underscores, and hyphens.");

        RuleFor(x => x.BudgetAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Budget amount cannot be negative.");
    }
}

public class UpdateCostCenterCommandValidator : AbstractValidator<UpdateCostCenterCommand>
{
    public UpdateCostCenterCommandValidator()
    {
        RuleFor(x => x.CostCenterId)
            .NotEmpty().WithMessage("Cost center ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Cost center name is required.")
            .MaximumLength(200).WithMessage("Cost center name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Cost center code is required.")
            .MaximumLength(20).WithMessage("Cost center code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Cost center code must contain only uppercase letters, digits, underscores, and hyphens.");

        RuleFor(x => x.BudgetAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Budget amount cannot be negative.");
    }
}

public class CreateProfitCenterCommandValidator : AbstractValidator<CreateProfitCenterCommand>
{
    public CreateProfitCenterCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Profit center name is required.")
            .MaximumLength(200).WithMessage("Profit center name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Profit center code is required.")
            .MaximumLength(20).WithMessage("Profit center code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Profit center code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class UpdateProfitCenterCommandValidator : AbstractValidator<UpdateProfitCenterCommand>
{
    public UpdateProfitCenterCommandValidator()
    {
        RuleFor(x => x.ProfitCenterId)
            .NotEmpty().WithMessage("Profit center ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Profit center name is required.")
            .MaximumLength(200).WithMessage("Profit center name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Profit center code is required.")
            .MaximumLength(20).WithMessage("Profit center code must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9_\-]+$").WithMessage("Profit center code must contain only uppercase letters, digits, underscores, and hyphens.");
    }
}

public class CreateOrganizationCalendarCommandValidator : AbstractValidator<CreateOrganizationCalendarCommand>
{
    public CreateOrganizationCalendarCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Calendar name is required.")
            .MaximumLength(200).WithMessage("Calendar name must not exceed 200 characters.");
    }
}

public class UpdateOrganizationCalendarCommandValidator : AbstractValidator<UpdateOrganizationCalendarCommand>
{
    public UpdateOrganizationCalendarCommandValidator()
    {
        RuleFor(x => x.CalendarId)
            .NotEmpty().WithMessage("Calendar ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Calendar name is required.")
            .MaximumLength(200).WithMessage("Calendar name must not exceed 200 characters.");
    }
}

public class CreateHolidayCommandValidator : AbstractValidator<CreateHolidayCommand>
{
    public CreateHolidayCommandValidator()
    {
        RuleFor(x => x.CalendarId)
            .NotEmpty().WithMessage("Calendar ID is required.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Holiday date is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Holiday name is required.")
            .MaximumLength(200).WithMessage("Holiday name must not exceed 200 characters.");
    }
}

public class UpdateHolidayCommandValidator : AbstractValidator<UpdateHolidayCommand>
{
    public UpdateHolidayCommandValidator()
    {
        RuleFor(x => x.HolidayId)
            .NotEmpty().WithMessage("Holiday ID is required.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Holiday date is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Holiday name is required.")
            .MaximumLength(200).WithMessage("Holiday name must not exceed 200 characters.");
    }
}

public class CreateEmployeeTeamAssignmentCommandValidator : AbstractValidator<CreateEmployeeTeamAssignmentCommand>
{
    public CreateEmployeeTeamAssignmentCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("Team ID is required.");

        RuleFor(x => x.EffectiveFrom)
            .NotEmpty().WithMessage("Effective from date is required.");
    }
}

public class EndEmployeeTeamAssignmentCommandValidator : AbstractValidator<EndEmployeeTeamAssignmentCommand>
{
    public EndEmployeeTeamAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment ID is required.");

        RuleFor(x => x.EffectiveTo)
            .NotEmpty().WithMessage("Effective to date is required.");
    }
}
