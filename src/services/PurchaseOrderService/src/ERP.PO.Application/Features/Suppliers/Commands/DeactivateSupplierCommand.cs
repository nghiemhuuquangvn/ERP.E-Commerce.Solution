using MediatR;

namespace ERP.PO.Application.Features.Suppliers.Commands;

public class DeactivateSupplierCommand : IRequest<Unit>
{
    public Guid SupplierId { get; set; }
    public string DeactivatedBy { get; set; } = string.Empty;
}
