using Xunit;
using FluentValidation.TestHelper;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Application.Features.PurchaseOrders.Validators;

namespace ERP.PO.Application.Tests.Features.PurchaseOrders.Validators;

public class CreatePurchaseOrderCommandValidatorTests
{
    private readonly CreatePurchaseOrderCommandValidator _validator;

    public CreatePurchaseOrderCommandValidatorTests()
    {
        _validator = new CreatePurchaseOrderCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = "PO-2024-001",
            SupplierId = Guid.NewGuid(),
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(30),
            CreatedBy = "user@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyPONumber_ShouldHaveError()
    {
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = "",
            SupplierId = Guid.NewGuid(),
            CreatedBy = "user@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PONumber);
    }

    [Fact]
    public void Validate_EmptySupplierId_ShouldHaveError()
    {
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = "PO-2024-002",
            SupplierId = Guid.Empty,
            CreatedBy = "user@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SupplierId);
    }

    [Fact]
    public void Validate_EmptyCreatedBy_ShouldHaveError()
    {
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = "PO-2024-003",
            SupplierId = Guid.NewGuid(),
            CreatedBy = ""
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CreatedBy);
    }

    [Fact]
    public void Validate_PastExpectedDeliveryDate_ShouldHaveError()
    {
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = "PO-2024-004",
            SupplierId = Guid.NewGuid(),
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "user@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ExpectedDeliveryDate);
    }

    [Fact]
    public void Validate_PONumberExceedsMaxLength_ShouldHaveError()
    {
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = new string('A', 51),
            SupplierId = Guid.NewGuid(),
            CreatedBy = "user@example.com"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PONumber);
    }
}
