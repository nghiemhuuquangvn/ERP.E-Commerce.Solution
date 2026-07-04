using ERP.CRM.Application.Commands;
using FluentValidation;

namespace ERP.CRM.Application.Validators;

public class CreateContactCommandValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(200).WithMessage("Contact name must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone must not exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Company)
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.Company));
    }
}

public class UpdateContactCommandValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactCommandValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("ContactId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(200).WithMessage("Contact name must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone must not exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Company)
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.Company));
    }
}

public class CreateLeadCommandValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadCommandValidator()
    {
        RuleFor(x => x.LeadName)
            .NotEmpty().WithMessage("Lead name is required.")
            .MaximumLength(200).WithMessage("Lead name must not exceed 200 characters.");

        RuleFor(x => x.LeadSource)
            .InclusiveBetween(0, 5).WithMessage("Lead source must be a valid LeadSource value (0–5).");

        RuleFor(x => x.EstimatedValue)
            .GreaterThanOrEqualTo(0).WithMessage("Estimated value must be non-negative.")
            .When(x => x.EstimatedValue.HasValue);
    }
}

public class CreateOpportunityCommandValidator : AbstractValidator<CreateOpportunityCommand>
{
    public CreateOpportunityCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Opportunity title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("ContactId is required.");

        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0).WithMessage("Value must be non-negative.");

        RuleFor(x => x.ExpectedCloseDate)
            .GreaterThan(DateTime.UtcNow).WithMessage("Expected close date must be in the future.");
    }
}

public class CreateActivityCommandValidator : AbstractValidator<CreateActivityCommand>
{
    public CreateActivityCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("ContactId is required.");
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Activity subject is required.")
            .MaximumLength(300).WithMessage("Subject must not exceed 300 characters.");
        RuleFor(x => x.ActivityType)
            .InclusiveBetween(0, 4).WithMessage("ActivityType must be 0=Call, 1=Email, 2=Meeting, 3=Note, 4=Task.");
        RuleFor(x => x.Notes)
            .MaximumLength(4000).WithMessage("Notes must not exceed 4000 characters.")
            .When(x => x.Notes != null);
    }
}

public class AddContactTagCommandValidator : AbstractValidator<AddContactTagCommand>
{
    public AddContactTagCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("ContactId is required.");
        RuleFor(x => x.Tag)
            .NotEmpty().WithMessage("Tag is required.")
            .MaximumLength(50).WithMessage("Tag must not exceed 50 characters.")
            .Matches(@"^[a-zA-Z0-9\-_\s]+$").WithMessage("Tag may only contain letters, numbers, hyphens, underscores and spaces.");
    }
}

public class OpenCaseCommandValidator : AbstractValidator<OpenCaseCommand>
{
    public OpenCaseCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Case title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("ContactId is required.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Priority)
            .InclusiveBetween(0, 3).WithMessage("Priority must be a valid CasePriority value (0–3).");
    }
}
