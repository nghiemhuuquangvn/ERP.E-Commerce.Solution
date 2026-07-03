using ERP.POS.Application.DTOs;
using ERP.POS.Application.Interfaces;
using ERP.POS.Application.Queries;
using MediatR;

namespace ERP.POS.Application.Handlers;

/// <summary>
/// IPosProductCacheRepository.GetBySkuAsync already existed and worked, but nothing
/// in Application/API ever called it — barcode/SKU scan-to-lookup had no reachable
/// endpoint despite the underlying capability being real.
/// </summary>
public class GetProductByBarcodeQueryHandler : IRequestHandler<GetProductByBarcodeQuery, ProductLookupDto?>
{
    private readonly IPosProductCacheRepository _cache;
    public GetProductByBarcodeQueryHandler(IPosProductCacheRepository cache) => _cache = cache;

    public async Task<ProductLookupDto?> Handle(GetProductByBarcodeQuery request, CancellationToken cancellationToken)
    {
        var product = await _cache.GetBySkuAsync(request.Barcode, cancellationToken);
        return product == null ? null : new ProductLookupDto
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Sku = product.Sku,
            Price = product.Price,
            IsActive = product.IsActive
        };
    }
}
