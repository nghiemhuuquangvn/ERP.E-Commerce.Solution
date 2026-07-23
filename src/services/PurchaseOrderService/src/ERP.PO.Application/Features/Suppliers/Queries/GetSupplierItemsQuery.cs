using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.Suppliers.Queries;

/// <summary>List all SupplierItems for a given Supplier.</summary>
public class GetSupplierItemsQuery : IRequest<IEnumerable<SupplierItemDto>>
{
    public Guid SupplierId { get; set; }
}

/// <summary>Get a single SupplierItem, scoped by its owning Supplier.</summary>
public class GetSupplierItemByIdQuery : IRequest<SupplierItemDto?>
{
    public Guid SupplierId { get; set; }
    public Guid SupplierItemId { get; set; }
}
