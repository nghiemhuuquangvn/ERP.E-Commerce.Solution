using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Domain.Common;

namespace ERP.Identity.Application.Interfaces
{
    public interface IDomainEventHandler<in TDomainEvent> where TDomainEvent : IDomainEvent
    {
        Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
    }
}