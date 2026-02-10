namespace ERP.Identity.Infrastructure.Repositories
{
    using ERP.Entity.Application.Interfaces.Repositories;
    using ERP.Identity.Infrastructure.Persistence;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly AuthDbContext _db;

        public GenericRepository(AuthDbContext db)
        {
            _db = db;
        }

        public async Task<T> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var entity = await _db.Set<T>().FindAsync(new object[] { id }, ct);
            return entity!;
        }

        public async Task<IQueryable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null,
                Func<IQueryable<T>, IQueryable<T>>? orderBy = null, 
                string? includeProperties = null,
                CancellationToken ct = default)
        {
            var query = _db.Set<T>().AsNoTracking();
            if (filter != null)
            {
                query = query.Where(filter);
            }
            foreach (var includeProperty in (includeProperties ?? string.Empty).Split([','], StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty);
            }
            if (orderBy != null)
            {
                query = orderBy(query);
            }
            return await Task.FromResult(query);
        }

        public async Task AddAsync(T entity, CancellationToken ct)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            await _db.Set<T>().AddAsync(entity, ct);
        }

        public Task DeleteAsync(T entity, CancellationToken ct)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _db.Set<T>().Remove(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(T entity, CancellationToken ct)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _db.Set<T>().Update(entity);
            return Task.CompletedTask;
        }
    }
}