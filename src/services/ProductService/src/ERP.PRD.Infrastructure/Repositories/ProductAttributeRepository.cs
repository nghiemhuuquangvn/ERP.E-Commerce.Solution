using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductAttributeRepository : IProductAttributeRepository
    {
        private readonly ProductDbContext _db;

        public ProductAttributeRepository(ProductDbContext db) => _db = db;

        public async Task<ProductAttribute?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.ProductAttributes.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        public async Task<ProductAttribute?> GetByIdWithOptionsAsync(Guid id, CancellationToken ct = default)
            => await _db.ProductAttributes
                .Include(a => a.Options)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
            => await _db.ProductAttributes.AnyAsync(a => a.Code == code && !a.IsDeleted, ct);

        public async Task<List<ProductAttribute>> GetByCategoryAsync(Guid? categoryId, CancellationToken ct = default)
        {
            var query = _db.ProductAttributes
                .Include(a => a.Options)
                .Where(a => !a.IsDeleted)
                .AsQueryable();

            query = categoryId.HasValue
                ? query.Where(a => a.CategoryId == null || a.CategoryId == categoryId)
                : query.Where(a => a.CategoryId == null);

            return await query
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToListAsync(ct);
        }

        public async Task<Dictionary<Guid, ProductAttribute>> GetByIdsWithOptionsAsDictionaryAsync(
            IEnumerable<Guid> ids, CancellationToken ct = default)
            => await _db.ProductAttributes
                .Include(a => a.Options)
                .Where(a => ids.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct);

        public async Task AddAsync(ProductAttribute attribute, CancellationToken ct = default)
            => await _db.ProductAttributes.AddAsync(attribute, ct);

        public void Update(ProductAttribute attribute)
            => _db.ProductAttributes.Update(attribute);
    }
}
