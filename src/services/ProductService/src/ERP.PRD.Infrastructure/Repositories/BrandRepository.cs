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
