using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;

namespace ERP.PAY.Application.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Payment?> GetByTransactionRefAsync(string transactionRef, CancellationToken ct = default);
    /// <summary>
    /// Tenant of the payment with this gateway reference, looked up ACROSS tenants — only for
    /// signature-verified gateway callbacks, which arrive without a user (and possibly on a
    /// host that is not the payment's tenant). Null when unknown.
    /// </summary>
    Task<Guid?> FindTenantByTransactionRefAsync(string transactionRef, CancellationToken ct = default);
    Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken ct = default);
    Task<(List<Payment> Items, int Total)> GetPagedAsync(int skip, int take, PaymentStatus? status, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}

public interface ISavedPaymentMethodRepository
{
    Task<Domain.Entities.SavedPaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Domain.Entities.SavedPaymentMethod>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task AddAsync(Domain.Entities.SavedPaymentMethod method, CancellationToken ct = default);
    Task UpdateAsync(Domain.Entities.SavedPaymentMethod method, CancellationToken ct = default);
    Task DeleteAsync(Domain.Entities.SavedPaymentMethod method, CancellationToken ct = default);
}

public interface IInstallmentPlanRepository
{
    Task<Domain.Entities.InstallmentPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Domain.Entities.InstallmentPlan?> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct = default);
    Task AddAsync(Domain.Entities.InstallmentPlan plan, CancellationToken ct = default);
    Task UpdateAsync(Domain.Entities.InstallmentPlan plan, CancellationToken ct = default);
}

public interface ISettlementRepository
{
    Task<Settlement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Settlement>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Settlement settlement, CancellationToken ct = default);
    Task UpdateAsync(Settlement settlement, CancellationToken ct = default);
}
