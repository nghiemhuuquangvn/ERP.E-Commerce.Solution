using Xunit;
using FluentValidation.TestHelper;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Application.Features.Suppliers.Validators;

namespace ERP.PO.Application.Tests.Features.Suppliers.Validators;

public class CreateSupplierCommandValidatorTests
{
    private readonly CreateSupplierCommandValidator _validator;

    public CreateSupplierCommandValidatorTests()
    {
        _validator = new CreateSupplierCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var command = new CreateSupplierCommand
        {
            Code = "SUP-001",
            Name = "Supplier Company",
            Email = "contact@supplier.com",
            CreatedBy = "admin@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyCode_ShouldHaveError()
    {
        var command = new CreateSupplierCommand
        {
            Code = "",
            Name = "Supplier Company",
            CreatedBy = "admin@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Validate_EmptyName_ShouldHaveError()
    {
        var command = new CreateSupplierCommand
        {
            Code = "SUP-002",
            Name = "",
            CreatedBy = "admin@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_InvalidEmail_ShouldHaveError()
    {
        var command = new CreateSupplierCommand
        {
            Code = "SUP-003",
            Name = "Supplier Company",
            Email = "invalid-email",
            CreatedBy = "admin@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmptyCreatedBy_ShouldHaveError()
    {
        var command = new CreateSupplierCommand
        {
            Code = "SUP-004",
            Name = "Supplier Company",
            CreatedBy = ""
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CreatedBy);
    }

    [Fact]
    public void Validate_CodeExceedsMaxLength_ShouldHaveError()
    {
        var command = new CreateSupplierCommand
        {
            Code = new string('A', 21),
            Name = "Supplier Company",
            CreatedBy = "admin@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Code);
    }
}
