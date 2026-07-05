using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductRelationRepository : IProductRelationRepository
    {
        private readonly ProductDbContext _db;

        public ProductRelationRepository(ProductDbContext db) => _db = db;

        public async Task<object?> FindByIdAsync(Guid relationId, CancellationToken ct = default)
            => await _db.ProductRelations.FindAsync(new object[] { relationId }, ct);

        public async Task<List<ProductRelationProjection>> GetByProductIdAsync(
            Guid productId, ProductRelationType? relationType, CancellationToken ct = default)
        {
            var query =
                from relation in _db.ProductRelations
                join related in _db.Products on relation.RelatedProductId equals related.Id
                where relation.ProductId == productId
                select new { relation, related };

            if (relationType.HasValue)
                query = query.Where(x => x.relation.RelationType == relationType.Value);

            return await query
                .OrderBy(x => x.relation.SortOrder)
                .Select(x => new ProductRelationProjection(
                    x.relation.Id,
                    x.related.Id,
                    x.related.Name,
                    x.related.Sku,
                    x.relation.RelationType,
                    x.relation.SortOrder))
                .ToListAsync(ct);
        }

        public async Task AddAsync(ProductRelation relation, CancellationToken ct = default)
            => await _db.ProductRelations.AddAsync(relation, ct);

        public void Remove(object relation)
        {
            if (relation is ProductRelation r)
                _db.ProductRelations.Remove(r);
        }
    }
}
