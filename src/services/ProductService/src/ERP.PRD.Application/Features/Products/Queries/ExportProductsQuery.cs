using System.Globalization;
using CsvHelper;
using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class ExportProductsQuery : IRequest<string>
    {
        public Guid? CategoryId { get; set; }
    }

    internal sealed class ProductExportRow
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ProductType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid? CategoryId { get; set; }
    }

    public class ExportProductsHandler : IRequestHandler<ExportProductsQuery, string>
    {
        private readonly IProductUnitOfWork _uow;

        public ExportProductsHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<string> Handle(ExportProductsQuery request, CancellationToken cancellationToken)
        {
            // Reuse the paged query with a very large page to get all products for export
            var (items, _) = await _uow.Products.GetPagedAsync(
                0, int.MaxValue, request.CategoryId, null, cancellationToken);

            var rows = items
                .OrderBy(p => p.Sku)
                .Select(p => new ProductExportRow
                {
                    Sku = p.Sku,
                    Name = p.Name,
                    Description = p.Description,
                    ProductType = p.ProductType.ToString(),
                    Status = p.Status.ToString(),
                    CategoryId = p.CategoryId
                })
                .ToList();

            using var writer = new StringWriter();
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(rows);
            }

            return writer.ToString();
        }
    }
}
