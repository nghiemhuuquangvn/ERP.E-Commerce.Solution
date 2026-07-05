using ERP.CRM.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.CRM.Infrastructure.Persistence;

public class CrmUnitOfWork : ICrmUnitOfWork, IDisposable
{
    private readonly CrmDbContext _db;
    private IDbContextTransaction? _transaction;

    public CrmUnitOfWork(
        CrmDbContext db,
        IContactRepository contacts,
        ILeadRepository leads,
        IOpportunityRepository opportunities,
        ICaseRepository cases,
        IActivityRepository activities,
        IContactTagRepository contactTags)
    {
        _db = db;
        Contacts = contacts;
        Leads = leads;
        Opportunities = opportunities;
        Cases = cases;
        Activities = activities;
        ContactTags = contactTags;
    }

    public IContactRepository Contacts { get; }
    public ILeadRepository Leads { get; }
    public IOpportunityRepository Opportunities { get; }
    public ICaseRepository Cases { get; }
    public IActivityRepository Activities { get; }
    public IContactTagRepository ContactTags { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _db.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            if (_transaction != null) await _transaction.CommitAsync(ct);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        _transaction?.Dispose();
        GC.SuppressFinalize(this);
    }
}
