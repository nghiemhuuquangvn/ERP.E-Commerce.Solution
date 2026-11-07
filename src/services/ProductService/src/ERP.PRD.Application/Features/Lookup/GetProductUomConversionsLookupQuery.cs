using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Lookup
{
    /// <summary>
    /// The per-product unit conversions (1 From = Factor To) of some products, for ManufacturingService (BOM line unit to the component's base unit, BR-BOM-07).
    /// Identity of the product, the two units and the factor only.
    /// </summary>
    public class GetProductUomConversionsLookupQuery : IRequest<List<ProductUomConversionLookupDto>>
    {
        public List<Guid> ProductIds { get; set; } = new();
    }

    public class ProductUomConversionLookupDto
    {
        public Guid ProductId { get; set; }
        public Guid FromUomId { get; set; }
        public Guid ToUomId { get; set; }
        public decimal ConversionFactor { get; set; }
    }

    public class GetProductUomConversionsLookupHandler : IRequestHandler<GetProductUomConversionsLookupQuery, List<ProductUomConversionLookupDto>>
    {
        private readonly IProductUnitOfWork _uow;

        public GetProductUomConversionsLookupHandler(IProductUnitOfWork uow) => _uow = uow;

        public async Task<List<ProductUomConversionLookupDto>> Handle(GetProductUomConversionsLookupQuery request, CancellationToken ct)
        {
            var ids = request.ProductIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var rows = await _uow.UomConversions.GetByProductIdsAsync(ids, ct);
            return rows.Select(c => new ProductUomConversionLookupDto
            {
                ProductId = c.ProductId,
                FromUomId = c.FromUomId,
                ToUomId = c.ToUomId,
                ConversionFactor = c.ConversionFactor,
            }).ToList();
        }
    }
}
