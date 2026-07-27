using MediatR;
using ERP.Shared.Infrastructure.Caching;
using ERP.WH.Application.Interfaces;

namespace ERP.WH.Application.Queries.Barcodes;

public class ResolveBarcodeQuery : IRequest<ResolveBarcodeResponse?>
{
    public string Barcode { get; set; } = string.Empty;
}

public class ResolveBarcodeResponse
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public Guid? LotId { get; set; }
    public string? SerialNumber { get; set; }
}

/// <summary>Must be fast — used by handheld scanners. Short-TTL cache since stock levels change frequently and this isn't the source of truth for a pick decision, just a quick lookup.</summary>
public class ResolveBarcodeQueryHandler : IRequestHandler<ResolveBarcodeQuery, ResolveBarcodeResponse?>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly ICacheService _cache;

    public ResolveBarcodeQueryHandler(IWarehouseUnitOfWork uow, ICacheService cache)
    {
        _uow = uow;
        _cache = cache;
    }

    public async Task<ResolveBarcodeResponse?> Handle(ResolveBarcodeQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"wh:barcode:{request.Barcode}";
        var cached = await _cache.GetAsync<ResolveBarcodeResponse>(cacheKey, cancellationToken);
        if (cached != null) return cached;

        var mapping = await _uow.BarcodeMappings.GetByBarcodeAsync(request.Barcode, cancellationToken);
        if (mapping == null) return null;

        var inventory = await _uow.Inventories.GetByProductIdAsync(mapping.ProductId, cancellationToken);
        if (inventory == null) return null;

        var response = new ResolveBarcodeResponse
        {
            ProductId = mapping.ProductId,
            ProductName = inventory.ProductName,
            AvailableQuantity = inventory.AvailableQuantity,
            LotId = mapping.LotId,
            SerialNumber = mapping.SerialNumber
        };

        await _cache.SetAsync(cacheKey, response, TimeSpan.FromSeconds(60), cancellationToken);
        return response;
    }
}
