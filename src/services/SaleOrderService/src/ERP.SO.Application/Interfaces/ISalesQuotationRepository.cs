using ERP.SO.Application.Queries;
using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Interfaces;

public interface ISalesQuotationRepository
{
    Task<SalesQuotation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(SalesQuotation quotation, CancellationToken ct = default);
    Task UpdateAsync(SalesQuotation quotation, CancellationToken ct = default);
    Task<List<SalesQuotation>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task<List<SalesQuotation>> GetAllAsync(int skip, int take, QuotationListFilter filter, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(QuotationListFilter filter, CancellationToken ct = default);
}
