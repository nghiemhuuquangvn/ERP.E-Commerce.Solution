using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.Suppliers.Commands;

public class UpdateSupplierItemCommand : IRequest<SupplierItemDto>
{
    public Guid SupplierId { get; set; }
    public Guid SupplierItemId { get; set; }
    public decimal LastPurchasePrice { get; set; }
    public int LeadTimeDays { get; set; }
    public string? SupplierItemCode { get; set; }
}
