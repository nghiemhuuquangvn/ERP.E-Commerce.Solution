namespace ERP.CRM.Application.Interfaces;

public interface ICrmUnitOfWork
{
    IContactRepository Contacts { get; }
    ILeadRepository Leads { get; }
    IOpportunityRepository Opportunities { get; }
    ICaseRepository Cases { get; }
    IActivityRepository Activities { get; }
    IContactTagRepository ContactTags { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
    void Dispose();
}
