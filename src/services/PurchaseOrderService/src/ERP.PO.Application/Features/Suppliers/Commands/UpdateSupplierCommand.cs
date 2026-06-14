using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.Suppliers.Commands;

public class UpdateSupplierCommand : IRequest<SupplierDto>
{
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public AddressDto? Address { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}
