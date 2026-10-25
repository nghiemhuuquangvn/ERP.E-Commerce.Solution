using ERP.ACC.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.ACC.Infrastructure.Persistence
{
    public class AccountingUnitOfWork : IAccountingUnitOfWork, IDisposable
    {
        private readonly AccountingDbContext _db;
        private IDbContextTransaction? _transaction;

        public AccountingUnitOfWork(
            AccountingDbContext db,
            IAccountRepository accounts,
            IInvoiceRepository invoices,
            IBillRepository bills,
            IJournalEntryRepository journalEntries,
            IJournalLineRepository journalLines,
            IFiscalPeriodRepository fiscalPeriods,
            IPostingAccountMappingRepository postingMappings,
            IClosingPolicyRepository closingPolicies,
            IReportProfileRepository reportProfiles,
            IOffBalanceRepository offBalanceEntries,
            ILoanRepository loans,
            ITaxRateRepository taxRates,
            IBankAccountRepository bankAccounts,
            IBankStatementRepository bankStatements)
        {
            _db = db;
            Accounts = accounts;
            Invoices = invoices;
            Bills = bills;
            JournalEntries = journalEntries;
            JournalLines = journalLines;
            FiscalPeriods = fiscalPeriods;
            PostingMappings = postingMappings;
            ClosingPolicies = closingPolicies;
            ReportProfiles = reportProfiles;
            OffBalanceEntries = offBalanceEntries;
            Loans = loans;
            TaxRates = taxRates;
            BankAccounts = bankAccounts;
            BankStatements = bankStatements;
        }

        public IAccountRepository Accounts { get; }
        public IInvoiceRepository Invoices { get; }
        public IBillRepository Bills { get; }
        public IJournalEntryRepository JournalEntries { get; }
        public IJournalLineRepository JournalLines { get; }
        public IFiscalPeriodRepository FiscalPeriods { get; }
        public IPostingAccountMappingRepository PostingMappings { get; }
        public IClosingPolicyRepository ClosingPolicies { get; }
        public IReportProfileRepository ReportProfiles { get; }
        public IOffBalanceRepository OffBalanceEntries { get; }
        public ILoanRepository Loans { get; }
        public ITaxRateRepository TaxRates { get; }
        public IBankAccountRepository BankAccounts { get; }
        public IBankStatementRepository BankStatements { get; }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
            => await _db.SaveChangesAsync(ct);

        public async Task BeginTransactionAsync(CancellationToken ct = default)
            => _transaction = await _db.Database.BeginTransactionAsync(ct);

        public async Task CommitTransactionAsync(CancellationToken ct = default)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                if (_transaction != null) await _transaction.CommitAsync(ct);
            }
            catch
            {
                await RollbackTransactionAsync(ct);
                throw;
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken ct = default)
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync(ct);
                _transaction.Dispose();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            _db.Dispose();
            _transaction?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
