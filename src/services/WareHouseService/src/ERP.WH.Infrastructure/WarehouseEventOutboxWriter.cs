using ERP.Shared.Messaging;
using ERP.WH.Application.Interfaces;
using ERP.WH.Infrastructure.Persistence;
using Newtonsoft.Json;

namespace ERP.WH.Infrastructure;

/// <summary>
/// Implements <see cref="IEventOutboxWriter"/> by adding a row to the same
/// <see cref="WarehouseDbContext"/> instance the calling handler's IWarehouseUnitOfWork
/// already uses (both scoped, same DI scope) — so the row is persisted by the handler's
/// existing SaveChangesAsync call, atomically with the business write. Uses the same
/// Type-name discriminator the Settings outbox path already relies on
/// (nameof(SettingsChangedIntegrationEvent)) so the two publish paths never cross-process
/// each other's rows from the shared OutboxMessages table.
/// </summary>
public class WarehouseEventOutboxWriter : IEventOutboxWriter
{
    private readonly WarehouseDbContext _context;

    public WarehouseEventOutboxWriter(WarehouseDbContext context)
    {
        _context = context;
    }

    public void Stage(object domainEvent)
    {
        // Events are staged by handlers that run in exactly one tenant (request or TenantScope).
        EventTenant.Stamp(domainEvent, _context.CurrentTenantId ?? Guid.Empty);
        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        var content = JsonConvert.SerializeObject(domainEvent, settings);
        _context.OutboxMessages.Add(new OutboxMessage(domainEvent.GetType().Name, content));
    }
}
