namespace ERP.Identity.Application.Interfaces
{
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
    }
}