using System.Linq.Expressions;

namespace ERP.Entity.Application.Interfaces.Repositories
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T> GetByIdAsync(Guid Id, CancellationToken ct);
        Task<IQueryable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IQueryable<T>>? orderBy = null, string? includeProperties = null, CancellationToken ct = default);
        Task AddAsync(T entity, CancellationToken ct);
        Task DeleteAsync(T entity, CancellationToken ct);
        Task UpdateAsync(T entity, CancellationToken ct);
    }
}