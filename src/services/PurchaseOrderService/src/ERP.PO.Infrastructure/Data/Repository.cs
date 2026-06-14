using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.PO.Infrastructure.Data;

/// <summary>
/// Generic repository implementation with tenant isolation support.
/// All queries are automatically filtered by TenantId via DbContext query filters.
/// </summary>
/// <typeparam name="T">Entity type inheriting from BaseEntity</typeparam>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly PurchasingDbContext _context;

    public Repository(PurchasingDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => _context;

    public void Add(T entity)
    {
        _context.Set<T>().Add(entity);
    }

    public void Update(T entity)
    {
        _context.Set<T>().Update(entity);
    }

    public void Delete(T entity)
    {
        _context.Set<T>().Remove(entity);
    }

    /// <summary>
    /// Gets entity by ID with tenant isolation.
    /// </summary>
    public async Task<T?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<T>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId, cancellationToken);
    }

    /// <summary>
    /// Gets all entities for a tenant.
    /// Query filter automatically restricts to tenant.
    /// </summary>
    public async Task<IEnumerable<T>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<T>()
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Unit of Work implementation using DbContext.
/// Manages transactions and persisting changes.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly PurchasingDbContext _context;

    public UnitOfWork(PurchasingDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public ITransaction BeginTransaction()
    {
        var transaction = _context.Database.BeginTransaction();
        return new TransactionAdapter(transaction);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
    }
}

