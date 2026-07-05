namespace ERP.PAY.Application.Interfaces;

public interface IPaymentUnitOfWork : IDisposable
{
    IPaymentRepository Payments { get; }
    ISettlementRepository Settlements { get; }
    ISavedPaymentMethodRepository SavedPaymentMethods { get; }
    IInstallmentPlanRepository InstallmentPlans { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
