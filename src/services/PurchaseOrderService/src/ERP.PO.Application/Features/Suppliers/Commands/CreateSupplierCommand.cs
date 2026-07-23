using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.Suppliers.Commands;

/// <summary>
/// Command to create a new supplier.
/// CreatedBy is extracted from IUserContextService (API Gateway headers), not from request.
/// </summary>
public class CreateSupplierCommand : IRequest<SupplierDto>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public AddressDto? Address { get; set; }
    public string? TaxCode { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public string CurrencyCode { get; set; } = "USD";
}
