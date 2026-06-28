using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PAY.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;
    public PaymentRepository(PaymentDbContext db) => _db = db;

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Payments.Include(p => p.Refunds).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        await _db.Payments.Include(p => p.Refunds).FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public async Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken ct = default) =>
        await _db.Payments.Where(p => p.Status == status).AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await _db.Payments.AddAsync(payment, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _db.Payments.Update(payment);
        await _db.SaveChangesAsync(ct);
    }
}

public class SettlementRepository : ISettlementRepository
{
    private readonly PaymentDbContext _db;
    public SettlementRepository(PaymentDbContext db) => _db = db;

    public async Task<Settlement?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Settlements.FindAsync(new object[] { id }, ct);

    public async Task<List<Settlement>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Settlements.OrderByDescending(s => s.CreatedAt).AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Settlement settlement, CancellationToken ct = default)
    {
        await _db.Settlements.AddAsync(settlement, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Settlement settlement, CancellationToken ct = default)
    {
        _db.Settlements.Update(settlement);
        await _db.SaveChangesAsync(ct);
    }
}
