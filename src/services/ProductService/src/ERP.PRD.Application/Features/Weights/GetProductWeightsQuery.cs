using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Weights;
using MediatR;

namespace ERP.PRD.Application.Features.Weights
{
    /// <summary>The weight in grams of one unit of each product (the first variant by SKU that has a weight); products without a weight are left out. Asked by ShippingService (P1-11b).</summary>
    public class GetProductWeightsQuery : IRequest<List<ProductWeightDto>>
    {
        public List<Guid> ProductIds { get; set; } = new();
    }

    public class ProductWeightDto
    {
        public Guid ProductId { get; set; }
        public int Grams { get; set; }
    }

    public class GetProductWeightsHandler : IRequestHandler<GetProductWeightsQuery, List<ProductWeightDto>>
    {
        private readonly IProductUnitOfWork _uow;

        public GetProductWeightsHandler(IProductUnitOfWork uow) => _uow = uow;

        public async Task<List<ProductWeightDto>> Handle(GetProductWeightsQuery request, CancellationToken cancellationToken)
        {
            var ids = request.ProductIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var variants = await _uow.Variants.GetByProductIdsAsync(ids, cancellationToken);
            return variants
                .OrderBy(v => v.Sku)
                .Select(v => (v.ProductId, Grams: WeightConverter.ToGrams(v.Weight, v.WeightUnit)))
                .Where(x => x.Grams.HasValue)
                .GroupBy(x => x.ProductId)
                .Select(g => new ProductWeightDto { ProductId = g.Key, Grams = g.First().Grams!.Value })
                .ToList();
        }
    }
}
