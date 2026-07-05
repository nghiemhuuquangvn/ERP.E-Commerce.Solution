namespace ERP.ACC.Application.Interfaces
{
    public interface IAccountingUnitOfWork
    {
        IAccountRepository Accounts { get; }
        IInvoiceRepository Invoices { get; }
        IBillRepository Bills { get; }
        IJournalEntryRepository JournalEntries { get; }
        IJournalLineRepository JournalLines { get; }
        IFiscalPeriodRepository FiscalPeriods { get; }
        ITaxRateRepository TaxRates { get; }
        IBankAccountRepository BankAccounts { get; }
        IBankStatementRepository BankStatements { get; }
        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task BeginTransactionAsync(CancellationToken ct = default);
        Task CommitTransactionAsync(CancellationToken ct = default);
        Task RollbackTransactionAsync(CancellationToken ct = default);
        void Dispose();
    }
}
