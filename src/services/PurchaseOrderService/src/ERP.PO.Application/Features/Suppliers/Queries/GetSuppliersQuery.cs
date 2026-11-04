using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.Suppliers.Queries;

public class GetSuppliersQuery : IRequest<PagedList<SupplierDto>>
{
    public bool? IsActive { get; set; }
    /// <summary>Part of the supplier code, name, e-mail or tax code (case-insensitive).</summary>
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
