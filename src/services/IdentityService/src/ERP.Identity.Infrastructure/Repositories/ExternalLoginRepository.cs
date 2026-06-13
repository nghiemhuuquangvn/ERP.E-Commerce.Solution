using System.Linq.Expressions;
using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;

namespace ERP.Identity.Infrastructure.Repositories;

public class ExternalLoginRepository : IGenericRepository<ExternalLogin>, IExternalLoginRepository
{
    private readonly AuthDbContext _dbContext;

    public ExternalLoginRepository(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(ExternalLogin entity, CancellationToken ct)
    {
        _dbContext.ExternalLogins.Add(entity);
        return _dbContext.SaveChangesAsync(ct);
    }

    public Task DeleteAsync(ExternalLogin entity, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<ExternalLogin>> GetAllAsync(Expression<Func<ExternalLogin, bool>>? filter = null, Func<IQueryable<ExternalLogin>, IQueryable<ExternalLogin>>? orderBy = null, string? includeProperties = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalLogin> GetByIdAsync(Guid Id, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(ExternalLogin entity, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}