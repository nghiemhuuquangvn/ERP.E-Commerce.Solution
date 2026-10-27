using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class PaymentApplicationRepository : IPaymentApplicationRepository
    {
        private readonly AccountingDbContext _db;

        public PaymentApplicationRepository(AccountingDbContext db) => _db = db;

        public async Task AddAsync(PaymentApplication application, CancellationToken ct = default) =>
            await _db.PaymentApplications.AddAsync(application, ct);

        public Task<bool> ExistsBySourceKeyAsync(string sourceKey, CancellationToken ct = default) =>
            _db.PaymentApplications.AsNoTracking().AnyAsync(p => p.SourceKey == sourceKey, ct);

        public async Task<List<PaymentApplication>> ListForDocumentsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken ct = default) =>
            documentIds.Count == 0 ? new List<PaymentApplication>() : await _db.PaymentApplications.AsNoTracking().Where(p => documentIds.Contains(p.DocumentId)).ToListAsync(ct);

        public async Task<List<PaymentApplication>> ListForMaturityAsync(DateTime asOf, DateTime dueAfter, CancellationToken ct = default)
        {
            var created = asOf.Date.AddDays(1);
            var invoiceIds = _db.Invoices.Where(i => i.DueDate > dueAfter && i.CreatedAt < created).Select(i => i.Id);
            var billIds = _db.Bills.Where(b => (b.Status == 1 || b.Status == 2) && b.DueDate > dueAfter && b.CreatedAt < created).Select(b => b.Id);
            return await _db.PaymentApplications.AsNoTracking()
                .Where(p => (p.DocumentType == PaymentApplication.InvoiceDocument && invoiceIds.Contains(p.DocumentId))
                         || (p.DocumentType == PaymentApplication.BillDocument && billIds.Contains(p.DocumentId)))
                .ToListAsync(ct);
        }
    }
}
