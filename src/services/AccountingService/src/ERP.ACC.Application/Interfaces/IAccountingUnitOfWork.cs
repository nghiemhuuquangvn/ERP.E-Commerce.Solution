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
        IPostingAccountMappingRepository PostingMappings { get; }
        IClosingPolicyRepository ClosingPolicies { get; }
        IReportProfileRepository ReportProfiles { get; }
        IOffBalanceRepository OffBalanceEntries { get; }
        IPaymentApplicationRepository PaymentApplications { get; }
        IStatementMappingRepository StatementMappings { get; }
        ILoanRepository Loans { get; }
        INoteTextRepository NoteTexts { get; }
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
