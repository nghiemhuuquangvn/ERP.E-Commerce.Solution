using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class BrandRepository : IBrandRepository
    {
        private readonly ProductDbContext _db;

        public BrandRepository(ProductDbContext db) => _db = db;

        public async Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.Brands.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);

        public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
            => await _db.Brands.AnyAsync(b => b.Id == id && !b.IsDeleted, ct);

        public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
            => await _db.Brands.AnyAsync(b => b.Code == code && !b.IsDeleted, ct);

        public async Task<Dictionary<string, Guid>> GetIdsByCodesAsync(IReadOnlyCollection<string> upperCaseCodes, CancellationToken ct = default)
        {
            if (upperCaseCodes.Count == 0) return new Dictionary<string, Guid>();
            var rows = await _db.Brands
                .Where(b => !b.IsDeleted && b.IsActive && upperCaseCodes.Contains(b.Code.ToUpper()))
                .Select(b => new { Code = b.Code.ToUpper(), b.Id })
                .ToListAsync(ct);
            return rows.GroupBy(r => r.Code).ToDictionary(g => g.Key, g => g.First().Id);
        }

        public async Task<List<Brand>> GetAllAsync(bool? isActive, CancellationToken ct = default)
        {
            var query = _db.Brands.Where(b => !b.IsDeleted).AsQueryable();

            if (isActive.HasValue)
                query = query.Where(b => b.IsActive == isActive.Value);

            return await query.OrderBy(b => b.Name).ToListAsync(ct);
        }

        public async Task AddAsync(Brand brand, CancellationToken ct = default)
            => await _db.Brands.AddAsync(brand, ct);

        public void Update(Brand brand)
            => _db.Brands.Update(brand);
    }
}
