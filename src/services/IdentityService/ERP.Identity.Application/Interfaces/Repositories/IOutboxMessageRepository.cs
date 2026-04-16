namespace ERP.Identity.Application.Interfaces.Repositories
{
    using ERP.Entity.Application.Interfaces.Repositories;
    using ERP.Identity.Domain.Entities;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IOutboxMessageRepository : IGenericRepository<OutboxMessage>
    {
        Task<List<OutboxMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken = default);
    }
}