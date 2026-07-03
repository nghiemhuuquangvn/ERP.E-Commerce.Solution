using System.Globalization;
using CsvHelper;
using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        private readonly ProductDbContext _context;

        public ExportProductsHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<string> Handle(ExportProductsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Products.Where(p => !p.IsDeleted).AsQueryable();
            if (request.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == request.CategoryId);

            var rows = await query
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
                .ToListAsync(cancellationToken);

            using var writer = new StringWriter();
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(rows);
            }

            return writer.ToString();
        }
    }
}
