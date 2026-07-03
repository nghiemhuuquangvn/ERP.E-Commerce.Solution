using MediatR;

namespace ERP.PO.Application.Features.Suppliers.Commands;

public class RateSupplierCommand : IRequest<Guid>
{
    public Guid SupplierId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
