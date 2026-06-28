using ERP.CRM.Domain.Entities;

namespace ERP.CRM.Application.Interfaces;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Contact?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);
    Task UpdateAsync(Contact contact, CancellationToken cancellationToken = default);
    Task<IEnumerable<Contact>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IEnumerable<Contact> Items, int TotalCount)> GetPagedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public interface ILeadRepository
{
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Lead lead, CancellationToken cancellationToken = default);
    Task UpdateAsync(Lead lead, CancellationToken cancellationToken = default);
    Task<IEnumerable<Lead>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IEnumerable<Lead> Items, int TotalCount)> GetPagedAsync(
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public interface IOpportunityRepository
{
    Task<Opportunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
    Task<IEnumerable<Opportunity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IEnumerable<Opportunity> Items, int TotalCount)> GetPagedAsync(
        Guid? contactId,
        int? stage,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetStageCounts(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalValue(CancellationToken cancellationToken = default);
    Task<decimal> GetWonValue(CancellationToken cancellationToken = default);
}

public interface ICaseRepository
{
    Task<Case?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Case caseEntity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Case caseEntity, CancellationToken cancellationToken = default);
    Task<IEnumerable<Case>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IEnumerable<Case> Items, int TotalCount)> GetPagedAsync(
        Guid? contactId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
