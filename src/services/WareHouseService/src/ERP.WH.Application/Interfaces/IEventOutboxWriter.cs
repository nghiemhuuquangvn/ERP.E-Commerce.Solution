namespace ERP.WH.Application.Interfaces;

/// <summary>
/// Stages a domain event into the Outbox as part of the same SaveChanges transaction as
/// the business write, instead of publishing it synchronously right away
/// (IEventPublisher.PublishAsync outside a transaction — a crash between the DB write and
/// the publish would otherwise silently drop the event). Actual delivery happens later via
/// WarehouseEventOutboxProcessor. Per-event, not per-aggregate, since these domain classes
/// don't extend AggregateRoot/BaseEntity and have no DomainEvents collection to stage from.
/// </summary>
public interface IEventOutboxWriter
{
    void Stage(object domainEvent);
}
