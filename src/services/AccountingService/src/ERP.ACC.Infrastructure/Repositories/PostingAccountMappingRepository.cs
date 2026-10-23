using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class PostingAccountMappingRepository : IPostingAccountMappingRepository
    {
        private readonly AccountingDbContext _db;

        public PostingAccountMappingRepository(AccountingDbContext db) => _db = db;

        public async Task<List<PostingAccountMapping>> GetAllAsync(CancellationToken ct = default) =>
            await _db.PostingAccountMappings.OrderBy(m => m.PostingKey).ToListAsync(ct);

        public async Task AddAsync(PostingAccountMapping mapping, CancellationToken ct = default)
        {
            await _db.PostingAccountMappings.AddAsync(mapping, ct);
        }
    }
}
