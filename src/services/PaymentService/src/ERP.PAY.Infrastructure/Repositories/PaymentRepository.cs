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

    public async Task<Payment?> GetByTransactionRefAsync(string transactionRef, CancellationToken ct = default) =>
        await _db.Payments.Include(p => p.Refunds).FirstOrDefaultAsync(p => p.TransactionRef == transactionRef, ct);

    public async Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken ct = default) =>
        await _db.Payments.Where(p => p.Status == status).AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct = default) =>
        await _db.Payments.AddAsync(payment, ct);

    public Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _db.Payments.Update(payment);
        return Task.CompletedTask;
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

    public async Task AddAsync(Settlement settlement, CancellationToken ct = default) =>
        await _db.Settlements.AddAsync(settlement, ct);

    public Task UpdateAsync(Settlement settlement, CancellationToken ct = default)
    {
        _db.Settlements.Update(settlement);
        return Task.CompletedTask;
    }
}

public class SavedPaymentMethodRepository : ISavedPaymentMethodRepository
{
    private readonly PaymentDbContext _db;
    public SavedPaymentMethodRepository(PaymentDbContext db) => _db = db;

    public async Task<SavedPaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.SavedPaymentMethods.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<List<SavedPaymentMethod>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default) =>
        await _db.SavedPaymentMethods.Where(m => m.CustomerId == customerId).ToListAsync(ct);

    public async Task AddAsync(SavedPaymentMethod method, CancellationToken ct = default) =>
        await _db.SavedPaymentMethods.AddAsync(method, ct);

    public Task UpdateAsync(SavedPaymentMethod method, CancellationToken ct = default)
    {
        _db.SavedPaymentMethods.Update(method);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SavedPaymentMethod method, CancellationToken ct = default)
    {
        _db.SavedPaymentMethods.Remove(method);
        return Task.CompletedTask;
    }
}

public class InstallmentPlanRepository : IInstallmentPlanRepository
{
    private readonly PaymentDbContext _db;
    public InstallmentPlanRepository(PaymentDbContext db) => _db = db;

    public async Task<InstallmentPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.InstallmentPlans.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<InstallmentPlan?> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct = default) =>
        await _db.InstallmentPlans.FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);

    public async Task AddAsync(InstallmentPlan plan, CancellationToken ct = default) =>
        await _db.InstallmentPlans.AddAsync(plan, ct);

    public Task UpdateAsync(InstallmentPlan plan, CancellationToken ct = default)
    {
        _db.InstallmentPlans.Update(plan);
        return Task.CompletedTask;
    }
}
