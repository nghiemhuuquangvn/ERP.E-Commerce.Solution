using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.Suppliers.Queries;

public class GetSupplierByIdQuery : IRequest<SupplierDto?>
{
    public Guid SupplierId { get; set; }
}
