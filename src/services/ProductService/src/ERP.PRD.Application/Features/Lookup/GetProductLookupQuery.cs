using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Enums;
using MediatR;

namespace ERP.PRD.Application.Features.Lookup
{
    /// <summary>
    /// What ManufacturingService needs to know about a product (service-to-service): does it exist, what is it called, may it be used
    /// (status Active) and which base unit it counts in. Id, SKU, name, unit only - no price, cost or stock.
    /// </summary>
    public class GetProductLookupQuery : IRequest<List<ProductLookupDto>>
    {
        public List<Guid> ProductIds { get; set; } = new();
    }

    public class ProductLookupDto
    {
        public Guid Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>True when the product is Active (not Draft, Discontinued or pending review).</summary>
        public bool IsActive { get; set; }
        public Guid? BaseUomId { get; set; }
    }

    public class GetProductLookupHandler : IRequestHandler<GetProductLookupQuery, List<ProductLookupDto>>
    {
        private readonly IProductUnitOfWork _uow;

        public GetProductLookupHandler(IProductUnitOfWork uow) => _uow = uow;

        public async Task<List<ProductLookupDto>> Handle(GetProductLookupQuery request, CancellationToken ct)
        {
            var ids = request.ProductIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var products = await _uow.Products.GetByIdsAsync(ids, ct);
            return products.Select(p => new ProductLookupDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                IsActive = p.Status == ProductStatus.Active,
                BaseUomId = p.BaseUomId,
            }).ToList();
        }
    }
}
