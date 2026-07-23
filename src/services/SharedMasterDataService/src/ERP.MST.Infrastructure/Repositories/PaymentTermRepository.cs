using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class PaymentTermRepository : IPaymentTermRepository
{
    private readonly MasterDataDbContext _context;

    public PaymentTermRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentTerm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentTerms
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<PaymentTerm> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PaymentTerms
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentTerms
            .AnyAsync(p => !p.IsDeleted && p.Code == code, cancellationToken);
    }

    public void Add(PaymentTerm paymentTerm)
    {
        _context.PaymentTerms.Add(paymentTerm);
    }

    public void Update(PaymentTerm paymentTerm)
    {
        _context.PaymentTerms.Update(paymentTerm);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
