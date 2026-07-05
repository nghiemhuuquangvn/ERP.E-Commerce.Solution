using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class CategoryRepository : ICategoryRepository
    {
        private readonly ProductDbContext _db;

        public CategoryRepository(ProductDbContext db) => _db = db;

        public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        public async Task<Category?> GetByIdWithTranslationsAsync(Guid id, CancellationToken ct = default)
            => await _db.Categories
                .Include(c => c.Translations)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
            => await _db.Categories.AnyAsync(c => c.Id == id && !c.IsDeleted, ct);

        public async Task<List<Category>> GetByParentIdAsync(Guid? parentId, CancellationToken ct = default)
        {
            var query = _db.Categories
                .Include(c => c.Translations)
                .AsQueryable();

            query = parentId.HasValue
                ? query.Where(c => c.ParentCategoryId == parentId)
                : query.Where(c => c.ParentCategoryId == null);

            return await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync(ct);
        }

        public async Task AddAsync(Category category, CancellationToken ct = default)
            => await _db.Categories.AddAsync(category, ct);

        public void Update(Category category)
            => _db.Categories.Update(category);
    }
}
