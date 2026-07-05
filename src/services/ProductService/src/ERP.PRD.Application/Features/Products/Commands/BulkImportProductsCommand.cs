using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    /// <summary>
    /// Bulk-creates products from a CSV payload — expected header row:
    /// Sku,Name,Description,ProductType,CategoryId (ProductType is the enum name e.g.
    /// "Physical"/"Digital"/"Service"/"Bundle"; CategoryId is optional/blank-able).
    /// CSV rather than Excel: opens/edits fine in Excel already, and avoids pulling in
    /// a much heavier Excel-specific library (ClosedXML/EPPlus) for what both formats
    /// can express identically here — one row per product, no multi-sheet/formatting
    /// need.
    /// </summary>
    public class BulkImportProductsCommand : IRequest<BulkImportResult>
    {
        public string CsvContent { get; set; } = string.Empty;
    }

    public class BulkImportRowError
    {
        public int RowNumber { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class BulkImportResult
    {
        public int SuccessCount { get; set; }
        public List<BulkImportRowError> Errors { get; set; } = new();
    }

    internal sealed class ProductImportRow
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ProductType { get; set; } = nameof(Domain.Enums.ProductType.Physical);
        public Guid? CategoryId { get; set; }
    }

    public class BulkImportProductsHandler : IRequestHandler<BulkImportProductsCommand, BulkImportResult>
    {
        private readonly IProductUnitOfWork _uow;

        public BulkImportProductsHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<BulkImportResult> Handle(BulkImportProductsCommand request, CancellationToken cancellationToken)
        {
            var result = new BulkImportResult();

            using var reader = new StringReader(request.CsvContent);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            var rows = csv.GetRecords<ProductImportRow>().ToList();
            var rowNumber = 1; // header is row 0

            foreach (var row in rows)
            {
                rowNumber++;

                try
                {
                    if (string.IsNullOrWhiteSpace(row.Sku) || string.IsNullOrWhiteSpace(row.Name))
                        throw new InvalidOperationException("Sku and Name are required");

                    var skuTaken = await _uow.Products.SkuExistsAsync(row.Sku, cancellationToken);
                    if (skuTaken)
                        throw new InvalidOperationException($"Sku '{row.Sku}' already exists");

                    if (!Enum.TryParse<ProductType>(row.ProductType, ignoreCase: true, out var productType))
                        throw new InvalidOperationException($"Unknown ProductType '{row.ProductType}'");

                    var product = Product.Create(row.Name, row.Sku, productType, row.CategoryId);
                    if (!string.IsNullOrWhiteSpace(row.Description))
                        product.UpdateDescription(row.Description);

                    await _uow.Products.AddAsync(product, cancellationToken);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new BulkImportRowError { RowNumber = rowNumber, Sku = row.Sku, Reason = ex.Message });
                }
            }

            if (result.SuccessCount > 0)
                await _uow.SaveChangesAsync(cancellationToken);

            return result;
        }
    }
}
