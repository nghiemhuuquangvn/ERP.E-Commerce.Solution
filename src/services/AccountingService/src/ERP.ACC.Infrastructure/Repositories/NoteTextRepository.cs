using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class NoteTextRepository : INoteTextRepository
    {
        private readonly AccountingDbContext _db;

        public NoteTextRepository(AccountingDbContext db) => _db = db;

        public async Task<List<NoteText>> GetAllAsync(CancellationToken ct = default) =>
            await _db.NoteTexts.OrderBy(n => n.SectionKey).ToListAsync(ct);

        public async Task AddAsync(NoteText text, CancellationToken ct = default) =>
            await _db.NoteTexts.AddAsync(text, ct);

        public async Task AddRevisionAsync(NoteTextRevision revision, CancellationToken ct = default) =>
            await _db.NoteTextRevisions.AddAsync(revision, ct);

        public async Task<List<NoteTextRevision>> GetRevisionsAsync(string sectionKey, string language, int fiscalYear, int take, CancellationToken ct = default) =>
            await _db.NoteTextRevisions.AsNoTracking()
                .Where(r => r.SectionKey == sectionKey && r.Language == language && r.FiscalYear == fiscalYear)
                .OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync(ct);
    }
}
