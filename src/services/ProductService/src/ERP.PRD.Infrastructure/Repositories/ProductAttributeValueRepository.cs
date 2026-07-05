using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductAttributeValueRepository : IProductAttributeValueRepository
    {
        private readonly ProductDbContext _db;

        public ProductAttributeValueRepository(ProductDbContext db) => _db = db;

        public async Task<ProductAttributeValue?> GetExistingAsync(
            AttributeOwnerType ownerType, Guid ownerId, Guid attributeId, CancellationToken ct = default)
            => await _db.ProductAttributeValues
                .FirstOrDefaultAsync(v =>
                    v.OwnerType == ownerType &&
                    v.OwnerId == ownerId &&
                    v.ProductAttributeId == attributeId &&
                    !v.IsDeleted, ct);

        public async Task<List<ProductAttributeValue>> GetByOwnerAsync(
            Guid productId, Guid? variantId, CancellationToken ct = default)
        {
            if (variantId.HasValue)
            {
                return await _db.ProductAttributeValues
                    .Where(v => !v.IsDeleted &&
                        ((v.OwnerType == AttributeOwnerType.Product && v.OwnerId == productId) ||
                         (v.OwnerType == AttributeOwnerType.ProductVariant && v.OwnerId == variantId.Value)))
                    .ToListAsync(ct);
            }

            return await _db.ProductAttributeValues
                .Where(v => !v.IsDeleted &&
                    v.OwnerType == AttributeOwnerType.Product &&
                    v.OwnerId == productId)
                .ToListAsync(ct);
        }

        public async Task<bool> AnyByAttributeIdAsync(Guid attributeId, CancellationToken ct = default)
            => await _db.ProductAttributeValues.AnyAsync(
                v => v.ProductAttributeId == attributeId && !v.IsDeleted, ct);

        public async Task<Guid> GetProductIdForVariantAsync(Guid variantId, CancellationToken ct = default)
            => await _db.ProductVariants
                .Where(v => v.Id == variantId)
                .Select(v => v.ProductId)
                .FirstAsync(ct);

        public async Task AddAsync(ProductAttributeValue value, CancellationToken ct = default)
            => await _db.ProductAttributeValues.AddAsync(value, ct);

        public void Update(ProductAttributeValue value)
            => _db.ProductAttributeValues.Update(value);
    }
}
