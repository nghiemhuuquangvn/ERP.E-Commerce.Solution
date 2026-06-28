using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;

namespace ERP.PAY.Application.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}

public interface ISettlementRepository
{
    Task<Settlement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Settlement>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Settlement settlement, CancellationToken ct = default);
    Task UpdateAsync(Settlement settlement, CancellationToken ct = default);
}
