using ERP.POS.Application.EventHandlers;
using ERP.POS.Domain.Entities;

namespace ERP.POS.Application.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Transaction>> GetByShiftAsync(Guid shiftId, CancellationToken ct = default);
    Task<Transaction?> GetOpenByTerminalAsync(Guid terminalId, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
}

public interface ICashShiftRepository
{
    Task<CashShift?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CashShift?> GetOpenByTerminalAsync(Guid terminalId, CancellationToken ct = default);
    Task<List<CashShift>> GetByDateRangeAsync(Guid? terminalId, DateTime start, DateTime end, CancellationToken ct = default);
    Task AddAsync(CashShift shift, CancellationToken ct = default);
    Task UpdateAsync(CashShift shift, CancellationToken ct = default);
}

public interface ITerminalRepository
{
    Task<Terminal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Terminal>> GetAllAsync(bool? isActive, CancellationToken ct = default);
    Task AddAsync(Terminal terminal, CancellationToken ct = default);
    Task UpdateAsync(Terminal terminal, CancellationToken ct = default);
}

public interface IPosProductCacheRepository
{
    Task UpsertAsync(PosProductCache product, CancellationToken ct = default);
    Task UpdatePriceAsync(Guid productId, decimal newPrice, CancellationToken ct = default);
    Task<PosProductCache?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<List<PosProductCache>> GetActiveAsync(CancellationToken ct = default);
}

public interface IPosPromotionCacheRepository
{
    Task UpsertAsync(PosPromotionCache promotion, CancellationToken ct = default);
    Task<List<PosPromotionCache>> GetActiveAsync(CancellationToken ct = default);
}
