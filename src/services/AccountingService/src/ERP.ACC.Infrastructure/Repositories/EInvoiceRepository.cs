using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class EInvoiceRepository : IEInvoiceRepository
    {
        private readonly AccountingDbContext _db;

        public EInvoiceRepository(AccountingDbContext db) => _db = db;

        public async Task<EInvoice?> GetActiveByInvoiceAsync(Guid invoiceId, CancellationToken ct = default) =>
            await _db.EInvoices.FirstOrDefaultAsync(e => e.InvoiceId == invoiceId && e.Status != (int)EInvoiceStatus.Cancelled, ct);

        public async Task<EInvoice?> GetLatestByInvoiceAsync(Guid invoiceId, CancellationToken ct = default) =>
            await _db.EInvoices.AsNoTracking().Where(e => e.InvoiceId == invoiceId).OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync(ct);

        public async Task AddAsync(EInvoice einvoice, CancellationToken ct = default) =>
            await _db.EInvoices.AddAsync(einvoice, ct);
    }
}
