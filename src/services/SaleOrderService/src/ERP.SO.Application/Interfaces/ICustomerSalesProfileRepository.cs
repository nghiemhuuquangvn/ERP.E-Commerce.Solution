using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Interfaces;

public interface ICustomerSalesProfileRepository
{
    Task<CustomerSalesProfile?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task AddAsync(CustomerSalesProfile profile, CancellationToken ct = default);
    Task UpdateAsync(CustomerSalesProfile profile, CancellationToken ct = default);

    /// <summary>One page of the tenant's profiles with the customer names, highest open balance first, and how many match in all.</summary>
    Task<(List<CreditProfileRow> Rows, int Total)> ListAsync(CreditProfileSearch search, int skip, int take, CancellationToken ct = default);

    /// <summary>The name on the customer's most recent order, or empty.</summary>
    Task<string> GetCustomerNameAsync(Guid customerId, CancellationToken ct = default);
}
