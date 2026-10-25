using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class VatLineRepository : IVatLineRepository
    {
        private readonly AccountingDbContext _db;

        public VatLineRepository(AccountingDbContext db) => _db = db;

        public async Task<List<VatLine>> ListAsync(int documentType, IReadOnlyCollection<Guid> documentIds, CancellationToken ct = default) =>
            documentIds.Count == 0
                ? new List<VatLine>()
                : await _db.VatLines.AsNoTracking().Where(l => l.DocumentType == documentType && documentIds.Contains(l.DocumentId)).OrderBy(l => l.CreatedAt).ToListAsync(ct);

        public async Task ReplaceAsync(int documentType, Guid documentId, IEnumerable<VatLine> lines, CancellationToken ct = default)
        {
            var old = await _db.VatLines.Where(l => l.DocumentType == documentType && l.DocumentId == documentId).ToListAsync(ct);
            _db.VatLines.RemoveRange(old);
            await _db.VatLines.AddRangeAsync(lines, ct);
        }
    }
}
