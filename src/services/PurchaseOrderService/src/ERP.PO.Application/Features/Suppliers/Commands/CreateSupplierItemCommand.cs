using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.Suppliers.Commands;

/// <summary>
/// Command to add a SupplierItem (what the supplier can supply, at what reference
/// price/lead time) to a Supplier. ProductId/VariantId reference ProductService's
/// Product/ProductVariant by Id only — cross-service, no DB FK.
/// </summary>
public class CreateSupplierItemCommand : IRequest<SupplierItemDto>
{
    public Guid SupplierId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string? SupplierItemCode { get; set; }
    public decimal LastPurchasePrice { get; set; }
    public int LeadTimeDays { get; set; }
}
