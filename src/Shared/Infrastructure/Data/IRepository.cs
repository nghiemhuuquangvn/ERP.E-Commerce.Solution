using ERP.Shared.Core.Domain;

namespace ERP.Shared.Infrastructure.Data;

/// <summary>
/// Generic repository interface for domain entities.
/// Enforces tenant isolation and optimistic locking patterns.
/// </summary>
/// <typeparam name="T">Entity type inheriting from BaseEntity</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);

    Task<T?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    IUnitOfWork UnitOfWork { get; }
}

/// <summary>
/// Unit of Work pattern interface for managing transactions and persisting changes.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    ITransaction BeginTransaction();
}

/// <summary>
/// Transaction interface for managing explicit transactions.
/// </summary>
public interface ITransaction : IDisposable, IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
