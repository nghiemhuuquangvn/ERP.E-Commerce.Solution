using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class LoanRepository : ILoanRepository
    {
        private readonly AccountingDbContext _db;

        public LoanRepository(AccountingDbContext db) => _db = db;

        public async Task<Loan?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.Loans.Include(l => l.Installments).FirstOrDefaultAsync(l => l.Id == id, ct);

        public async Task<List<Loan>> ListAsync(CancellationToken ct = default) =>
            await _db.Loans.Include(l => l.Installments).AsNoTracking().OrderByDescending(l => l.StartDate).ToListAsync(ct);

        public async Task AddAsync(Loan loan, CancellationToken ct = default) =>
            await _db.Loans.AddAsync(loan, ct);
    }
}
