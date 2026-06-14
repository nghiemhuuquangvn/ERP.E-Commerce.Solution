using Xunit;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Application.Features.Suppliers.Handlers;
using ERP.PO.Application.Mappings;

namespace ERP.PO.Application.Tests.Features.Suppliers;

public class CreateSupplierCommandHandlerTests
{
    private readonly IMapper _mapper;
    private readonly CreateSupplierCommandHandler _handler;

    public CreateSupplierCommandHandlerTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        _handler = new CreateSupplierCommandHandler(_mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCreatedSupplierDto()
    {
        var command = new CreateSupplierCommand
        {
            Code = "SUP-001",
            Name = "Supplier Company A",
            ContactPerson = "John Doe",
            Email = "john@supplier.com",
            Phone = "123-456-7890",
            CreatedBy = "admin@example.com"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(command.Code, result.Code);
        Assert.Equal(command.Name, result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Handle_WithAddress_ReturnsSupplierWithAddress()
    {
        var address = new AddressDto
        {
            Street = "123 Main St",
            City = "New York",
            State = "NY",
            ZipCode = "10001",
            Country = "USA"
        };

        var command = new CreateSupplierCommand
        {
            Code = "SUP-002",
            Name = "Supplier Company B",
            Address = address,
            CreatedBy = "admin@example.com"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Address);
        Assert.Equal(address.City, result.Address.City);
    }

    [Fact]
    public async Task Handle_MinimalData_ReturnsCreatedSupplierDto()
    {
        var command = new CreateSupplierCommand
        {
            Code = "SUP-003",
            Name = "Supplier Company C",
            CreatedBy = "admin@example.com"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(command.Code, result.Code);
        Assert.Null(result.ContactPerson);
        Assert.Null(result.Email);
    }
}
