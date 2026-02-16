namespace ERP.Identity.Infrastructure.Events
{
    using ERP.Identity.Application.Interfaces;
    using ERP.Identity.Domain.Common;
    using Microsoft.Extensions.DependencyInjection;

    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public DomainEventDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task DispatchAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            if (domainEvent == null)
            {
                ArgumentNullException.ThrowIfNull(domainEvent);
            }

            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

            var handlers = _serviceProvider.GetServices(handlerType);

            if (handlers == null || !handlers.Any())
            {
                return;
            }

            foreach (var handler in handlers)
            {
                var method = handlerType.GetMethod("HandleAsync");
                if (method != null)
                {
                    await (Task)method.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
                }
            }
        }
    }
}