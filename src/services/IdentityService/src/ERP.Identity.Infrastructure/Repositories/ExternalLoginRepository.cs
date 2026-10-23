using System.Linq.Expressions;
using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories;

/// <summary>
/// Links between a user and an external identity (Google, Microsoft, ...). Like every Identity repository it does
/// not save: the unit of work commits, so a link is written in the same transaction as the user it belongs to.
/// </summary>
public class ExternalLoginRepository : IGenericRepository<ExternalLogin>, IExternalLoginRepository
{
    private readonly AuthDbContext _dbContext;

    public ExternalLoginRepository(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ExternalLogin entity, CancellationToken ct)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        await _dbContext.ExternalLogins.AddAsync(entity, ct);
    }

    public Task UpdateAsync(ExternalLogin entity, CancellationToken ct)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        _dbContext.ExternalLogins.Update(entity);
        return Task.CompletedTask;
    }

    /// <summary>Unlinks: the row is kept (audit) but disabled, so the external account no longer signs in.</summary>
    public Task DeleteAsync(ExternalLogin entity, CancellationToken ct)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        entity.Disable();
        return UpdateAsync(entity, ct);
    }

    public async Task<ExternalLogin> GetByIdAsync(Guid Id, CancellationToken ct)
    {
        return await _dbContext.ExternalLogins.FirstOrDefaultAsync(x => x.Id == Id, ct)
            ?? throw new KeyNotFoundException($"External login {Id} was not found.");
    }

    public Task<IQueryable<ExternalLogin>> GetAllAsync(
        Expression<Func<ExternalLogin, bool>>? filter = null,
        Func<IQueryable<ExternalLogin>, IQueryable<ExternalLogin>>? orderBy = null,
        string? includeProperties = null,
        CancellationToken ct = default)
    {
        IQueryable<ExternalLogin> query = _dbContext.ExternalLogins;

        if (filter != null)
            query = query.Where(filter);

        if (!string.IsNullOrWhiteSpace(includeProperties))
        {
            foreach (var property in includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                query = query.Include(property);
        }

        if (orderBy != null)
            query = orderBy(query);

        return Task.FromResult(query);
    }
}
