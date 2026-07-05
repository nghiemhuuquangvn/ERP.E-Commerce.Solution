namespace ERP.POS.Application.Interfaces;

public interface IPosUnitOfWork : IDisposable
{
    ITransactionRepository Transactions { get; }
    ICashShiftRepository CashShifts { get; }
    ITerminalRepository Terminals { get; }
    IPosProductCacheRepository ProductCache { get; }
    IPosPromotionCacheRepository PromotionCache { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
