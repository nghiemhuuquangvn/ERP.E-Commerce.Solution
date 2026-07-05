using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly AccountingDbContext _db;

        public InvoiceRepository(AccountingDbContext db) => _db = db;

        public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);

        public async Task<List<Invoice>> GetAllAsync(CancellationToken ct = default) =>
            await _db.Invoices.OrderByDescending(i => i.CreatedAt).AsNoTracking().ToListAsync(ct);

        public async Task<List<Invoice>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default) =>
            await _db.Invoices.Where(i => i.CustomerId == customerId).OrderByDescending(i => i.CreatedAt).AsNoTracking().ToListAsync(ct);

        public async Task<List<Invoice>> GetByStatusAsync(int status, CancellationToken ct = default) =>
            await _db.Invoices.Where(i => i.Status == status).OrderByDescending(i => i.CreatedAt).AsNoTracking().ToListAsync(ct);

        public async Task<List<Invoice>> GetUnpaidInvoicesAsync(CancellationToken ct = default) =>
            await _db.Invoices.Where(i => i.Status == 0 || i.Status == 2).AsNoTracking().ToListAsync(ct);

        public async Task AddAsync(Invoice invoice, CancellationToken ct = default)
        {
            await _db.Invoices.AddAsync(invoice, ct);
        }

        public Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
        {
            _db.Invoices.Update(invoice);
            return Task.CompletedTask;
        }
    }
}
