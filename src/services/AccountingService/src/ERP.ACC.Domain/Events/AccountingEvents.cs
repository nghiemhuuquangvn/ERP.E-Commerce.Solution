using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Events
{
    public record InvoiceCreatedEvent(
        Guid InvoiceId,
        string InvoiceNumber,
        Guid CustomerId,
        Guid OrderId,
        decimal Amount,
        DateTime DueDate) : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record InvoicePaidEvent(
        Guid InvoiceId,
        string InvoiceNumber,
        Guid CustomerId,
        decimal Amount,
        DateTime PaidAt) : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record BillApprovedEvent(
        Guid BillId,
        string BillNumber,
        Guid SupplierId,
        Guid POId,
        decimal Amount) : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record BillPaidEvent(
        Guid BillId,
        string BillNumber,
        Guid SupplierId,
        decimal Amount,
        DateTime PaidAt) : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record JournalEntryPostedEvent(
        Guid JournalEntryId,
        string EntryNumber,
        Guid FiscalPeriodId,
        string Description,
        decimal TotalDebit,
        decimal TotalCredit) : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record FiscalPeriodClosedEvent(
        Guid FiscalPeriodId,
        string PeriodName,
        DateTime StartDate,
        DateTime EndDate,
        DateTime ClosedAt) : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record TaxRateCreatedEvent(
        Guid TaxRateId,
        string Name,
        string Code,
        decimal Rate) : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record BankAccountCreatedEvent(
        Guid BankAccountId,
        string AccountName,
        string BankName,
        string AccountNumber) : IDomainEvent, ITenantEvent
    {
        /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
        public Guid TenantId { get; set; }

        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
