using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Interfaces;

public interface ICustomerSalesProfileRepository
{
    Task<CustomerSalesProfile?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task AddAsync(CustomerSalesProfile profile, CancellationToken ct = default);
    Task UpdateAsync(CustomerSalesProfile profile, CancellationToken ct = default);
}
