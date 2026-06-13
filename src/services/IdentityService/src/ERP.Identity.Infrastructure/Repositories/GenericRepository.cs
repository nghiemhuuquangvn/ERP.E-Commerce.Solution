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
        protected readonly AuthDbContext _dbContext;

        public GenericRepository(AuthDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<T> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var entity = await _dbContext.Set<T>().FindAsync(new object[] { id }, ct);
            return entity!;
        }

        public async Task<IQueryable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null,
                Func<IQueryable<T>, IQueryable<T>>? orderBy = null, 
                string? includeProperties = null,
                CancellationToken ct = default)
        {
            var query = _dbContext.Set<T>().AsNoTracking();
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
            await _dbContext.Set<T>().AddAsync(entity, ct);
        }

        public Task DeleteAsync(T entity, CancellationToken ct)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _dbContext.Set<T>().Remove(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(T entity, CancellationToken ct)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _dbContext.Set<T>().Update(entity);
            return Task.CompletedTask;
        }
        public virtual void Attach(T entity)
        {
            // 1. Tìm trong bộ nhớ đệm (Local) xem thực thể này đã được track chưa
            var entry = _dbContext.Entry(entity);
            
            if (entry.State == EntityState.Detached)
            {
                // 2. Nếu chưa, thử tìm thực thể cùng ID trong Local để tránh xung đột
                var entityType = _dbContext.Model.FindEntityType(typeof(T));
                var primaryKeyResult = entityType?.FindPrimaryKey();
                var primaryKey = primaryKeyResult?.Properties.Select(p => p.Name).FirstOrDefault();
                
                if (string.IsNullOrEmpty(primaryKey))
                {
                    _dbContext.Set<T>().Attach(entity);
                    return;
                }
                                                
                var idValue = entity.GetType().GetProperty(primaryKey)?.GetValue(entity);
                
                var localEntity = _dbContext.Set<T>().Local.FirstOrDefault(e => 
                    e.GetType().GetProperty(primaryKey)?.GetValue(e)?.Equals(idValue) ?? false);

                if (localEntity == null)
                {
                    // 3. Nếu thực sự chưa có trong Local, tiến hành Attach
                    _dbContext.Set<T>().Attach(entity);
                }
            }
        }
    }
}