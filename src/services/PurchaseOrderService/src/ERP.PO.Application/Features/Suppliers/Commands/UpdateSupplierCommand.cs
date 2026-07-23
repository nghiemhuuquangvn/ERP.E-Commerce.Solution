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
    public string? TaxCode { get; set; }
    public string? CurrencyCode { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Dedicated action for setting the supplier's payment term — mirrors DeactivateSupplierCommand's
/// "dedicated action" style rather than folding into the main Update, since PaymentTermId/
/// PaymentTermDays are set together via Supplier.SetPaymentTerm (a distinct domain operation
/// from the plain-field Update).
/// </summary>
public class SetSupplierPaymentTermCommand : IRequest<SupplierDto>
{
    public Guid SupplierId { get; set; }
    public Guid? PaymentTermId { get; set; }
    public int PaymentTermDays { get; set; } = 30;
}
