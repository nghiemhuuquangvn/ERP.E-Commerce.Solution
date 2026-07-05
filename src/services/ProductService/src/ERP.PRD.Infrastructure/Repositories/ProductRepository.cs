using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductRepository : IProductRepository
    {
        private readonly ProductDbContext _db;

        public ProductRepository(ProductDbContext db) => _db = db;

        public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        public async Task<Product?> GetByIdWithVariantsAsync(Guid id, CancellationToken ct = default)
            => await _db.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        public async Task<Product?> GetByIdWithTranslationsAsync(Guid id, CancellationToken ct = default)
            => await _db.Products
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        public async Task<Product?> GetByIdWithVariantsAndTranslationsAsync(Guid id, CancellationToken ct = default)
            => await _db.Products
                .Include(p => p.Variants)
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        public async Task<Product?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default)
            => await _db.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
            => await _db.Products
                .Include(p => p.Variants)
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Sku == sku.ToUpper() && !p.IsDeleted, ct);

        public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
            => await _db.Products.AnyAsync(p => p.Id == id && !p.IsDeleted, ct);

        public async Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default)
            => await _db.Products.AnyAsync(p => p.Sku == sku && !p.IsDeleted, ct);

        public async Task<bool> SlugTakenByAnotherAsync(Guid productId, string slug, CancellationToken ct = default)
            => await _db.Products.AnyAsync(
                p => p.Id != productId && p.Slug == slug.Trim().ToLower(), ct);

        public async Task<(List<Product> Items, int Total)> GetPagedAsync(
            int skip, int take, Guid? categoryId, int? status, CancellationToken ct = default)
        {
            var query = _db.Products
                .Include(p => p.Variants)
                .Include(p => p.Translations)
                .Where(p => !p.IsDeleted
                    && (!categoryId.HasValue || p.CategoryId == categoryId)
                    && (!status.HasValue || (int)p.Status == status));

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<List<Product>> SearchAsync(string searchTerm, CancellationToken ct = default)
        {
            var lower = searchTerm.ToLower();
            return await _db.Products
                .Include(p => p.Variants)
                .Include(p => p.Translations)
                .Where(p => !p.IsDeleted
                    && (p.Name.ToLower().Contains(lower)
                        || p.Sku.ToLower().Contains(lower)
                        || (p.Description != null && p.Description.ToLower().Contains(lower))))
                .ToListAsync(ct);
        }

        public async Task<List<Guid>> GetAllActiveIdsAsync(CancellationToken ct = default)
            => await _db.Products
                .Where(p => !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(ct);

        public async Task<List<string>> GetExportRowsAsync(Guid? categoryId, CancellationToken ct = default)
        {
            // Returns CSV rows; building the actual CSV is done in the handler,
            // but we return the projected data as a delimited list is too complex.
            // Instead this method is unused — ExportProductsHandler uses the projection
            // directly. Implementation here satisfies the interface contract;
            // actual data fetching is via GetPagedExportableAsync below.
            await Task.CompletedTask;
            return new List<string>();
        }

        public async Task AddAsync(Product product, CancellationToken ct = default)
            => await _db.Products.AddAsync(product, ct);

        public void Update(Product product)
            => _db.Products.Update(product);
    }
}
