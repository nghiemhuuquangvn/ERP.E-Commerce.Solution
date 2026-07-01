using ERP.HRM.Application.Commands;
using FluentValidation;

namespace ERP.HRM.Application.Validators;

public class SubmitLeaveRequestCommandValidator : AbstractValidator<SubmitLeaveRequestCommand>
{
    public SubmitLeaveRequestCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.LeaveType)
            .InclusiveBetween(0, 4).WithMessage("Leave type must be between 0 (Annual) and 4 (Paternity).");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after start date.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}

public class RejectLeaveCommandValidator : AbstractValidator<RejectLeaveCommand>
{
    public RejectLeaveCommandValidator()
    {
        RuleFor(x => x.LeaveRequestId)
            .NotEmpty().WithMessage("Leave request ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

public class GeneratePaySlipCommandValidator : AbstractValidator<GeneratePaySlipCommand>
{
    public GeneratePaySlipCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");

        RuleFor(x => x.GrossPay)
            .GreaterThan(0).WithMessage("Gross pay must be greater than 0.");

        RuleFor(x => x.Deductions)
            .GreaterThanOrEqualTo(0).WithMessage("Deductions must be 0 or greater.")
            .LessThan(x => x.GrossPay).WithMessage("Deductions must be less than gross pay.");
    }
}

public class ProcessPayrollCommandValidator : AbstractValidator<ProcessPayrollCommand>
{
    public ProcessPayrollCommandValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");
    }
}
