using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class BillRepository : IBillRepository
    {
        private readonly AccountingDbContext _db;

        public BillRepository(AccountingDbContext db) => _db = db;

        public async Task<Bill?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await _db.Bills.FirstOrDefaultAsync(b => b.Id == id, ct);

        public async Task<List<Bill>> GetAllAsync(CancellationToken ct = default) =>
            await _db.Bills.OrderByDescending(b => b.CreatedAt).AsNoTracking().ToListAsync(ct);

        public async Task<List<Bill>> GetBySupplierIdAsync(Guid supplierId, CancellationToken ct = default) =>
            await _db.Bills.Where(b => b.SupplierId == supplierId).OrderByDescending(b => b.CreatedAt).AsNoTracking().ToListAsync(ct);

        public async Task<Bill?> GetByPurchaseOrderIdAsync(Guid purchaseOrderId, CancellationToken ct = default) =>
            await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.POId == purchaseOrderId, ct);

        public async Task<Bill?> GetBySupplierInvoiceIdAsync(Guid supplierInvoiceId, CancellationToken ct = default) =>
            await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.SupplierInvoiceId == supplierInvoiceId, ct);

        public async Task<List<Bill>> GetOpenApprovedByPurchaseOrderIdAsync(Guid purchaseOrderId, CancellationToken ct = default) =>
            await _db.Bills.Where(b => b.POId == purchaseOrderId && b.Status == 1 && b.CreditedAmount < b.Amount).OrderBy(b => b.DueDate).ToListAsync(ct);

        public async Task<List<Bill>> GetByStatusAsync(int status, CancellationToken ct = default) =>
            await _db.Bills.Where(b => b.Status == status).OrderByDescending(b => b.CreatedAt).AsNoTracking().ToListAsync(ct);

        public async Task<List<Bill>> GetUnpaidBillsAsync(CancellationToken ct = default) =>
            await _db.Bills.Where(b => b.Status == 0 || b.Status == 1).AsNoTracking().ToListAsync(ct);

        public async Task<List<Bill>> GetMaturityCandidatesAsync(DateTime asOf, DateTime dueAfter, CancellationToken ct = default) =>
            await _db.Bills.Where(b => (b.Status == 1 || b.Status == 2) && b.DueDate > dueAfter && b.CreatedAt < asOf.Date.AddDays(1)).AsNoTracking().ToListAsync(ct);

        public async Task<List<Bill>> GetForeignCreatedByAsync(DateTime asOf, CancellationToken ct = default) =>
            await _db.Bills.Where(b => b.ForeignCurrencyCode != null && b.CreatedAt < asOf.Date.AddDays(1)).AsNoTracking().ToListAsync(ct);

        public async Task AddAsync(Bill bill, CancellationToken ct = default)
        {
            await _db.Bills.AddAsync(bill, ct);
        }

        public Task UpdateAsync(Bill bill, CancellationToken ct = default)
        {
            _db.Bills.Update(bill);
            return Task.CompletedTask;
        }
    }
}
