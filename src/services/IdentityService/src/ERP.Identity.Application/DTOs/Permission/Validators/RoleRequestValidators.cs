using FluentValidation;

namespace ERP.Identity.Application.DTOs.Permission.Validators;

public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.RoleCode)
            .NotEmpty().WithMessage("Role code is required.")
            .MaximumLength(100).WithMessage("Role code must not exceed 100 characters.");

        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(200).WithMessage("Role name must not exceed 200 characters.");
    }
}

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(200).WithMessage("Role name must not exceed 200 characters.");
    }
}

public class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User id is required.");

        RuleFor(x => x.RoleCode)
            .NotEmpty().WithMessage("Role code is required.")
            .MaximumLength(100).WithMessage("Role code must not exceed 100 characters.");
    }
}

public class AssignPermissionToRoleRequestValidator : AbstractValidator<AssignPermissionToRoleRequest>
{
    public AssignPermissionToRoleRequestValidator()
    {
        RuleFor(x => x.PermissionCode)
            .NotEmpty().WithMessage("Permission code is required.")
            .MaximumLength(200).WithMessage("Permission code must not exceed 200 characters.");
    }
}
