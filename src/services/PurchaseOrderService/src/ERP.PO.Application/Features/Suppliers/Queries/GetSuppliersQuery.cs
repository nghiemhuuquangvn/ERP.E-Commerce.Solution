using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.Suppliers.Queries;

public class GetSuppliersQuery : IRequest<IEnumerable<SupplierDto>>
{
    public bool? IsActive { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
