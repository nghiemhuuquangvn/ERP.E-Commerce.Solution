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
    }
}
