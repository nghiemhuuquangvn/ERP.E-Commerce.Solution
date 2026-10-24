using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;

namespace ERP.ACC.Application.Interfaces
{
    public interface IAccountRepository
    {
        Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Account>> GetAllAsync(CancellationToken ct = default);
        /// <summary>Every account, tracked, so changes made by the caller are saved by the unit of work.</summary>
        Task<List<Account>> GetAllTrackedAsync(CancellationToken ct = default);
        /// <summary>Ids of the accounts that have at least one active sub-account (they take no postings).</summary>
        Task<HashSet<Guid>> GetParentAccountIdsAsync(CancellationToken ct = default);
        Task<List<Account>> GetByNumbersAsync(IEnumerable<string> accountNumbers, CancellationToken ct = default);
        Task<List<Account>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
        Task AddAsync(Account account, CancellationToken ct = default);
        Task UpdateAsync(Account account, CancellationToken ct = default);
    }

    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Invoice>> GetAllAsync(CancellationToken ct = default);
        Task<List<Invoice>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
        Task<Invoice?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
        Task<List<Invoice>> GetByStatusAsync(int status, CancellationToken ct = default);
        Task<List<Invoice>> GetUnpaidInvoicesAsync(CancellationToken ct = default);
        Task AddAsync(Invoice invoice, CancellationToken ct = default);
        Task UpdateAsync(Invoice invoice, CancellationToken ct = default);
    }

    public interface IBillRepository
    {
        Task<Bill?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Bill>> GetAllAsync(CancellationToken ct = default);
        Task<List<Bill>> GetBySupplierIdAsync(Guid supplierId, CancellationToken ct = default);
        Task<Bill?> GetByPurchaseOrderIdAsync(Guid purchaseOrderId, CancellationToken ct = default);
        Task<List<Bill>> GetByStatusAsync(int status, CancellationToken ct = default);
        Task<List<Bill>> GetUnpaidBillsAsync(CancellationToken ct = default);
        Task AddAsync(Bill bill, CancellationToken ct = default);
        Task UpdateAsync(Bill bill, CancellationToken ct = default);
    }

    public interface IJournalEntryRepository
    {
        Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<JournalEntry>> GetAllAsync(CancellationToken ct = default);
        Task<List<JournalEntry>> GetByFiscalPeriodIdAsync(Guid fiscalPeriodId, CancellationToken ct = default);
        /// <summary>The entry posted for a document ("Invoice:&lt;id&gt;"), with its lines; null when none (tracked).</summary>
        Task<JournalEntry?> GetBySourceKeyAsync(string sourceKey, CancellationToken ct = default);
        /// <summary>True when the tenant has at least one journal entry of any status.</summary>
        Task<bool> AnyAsync(CancellationToken ct = default);
        Task AddAsync(JournalEntry entry, CancellationToken ct = default);
        Task UpdateAsync(JournalEntry entry, CancellationToken ct = default);
    }

    public interface IFiscalPeriodRepository
    {
        Task<FiscalPeriod?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<FiscalPeriod>> GetAllAsync(CancellationToken ct = default);
        Task<FiscalPeriod?> GetOpenPeriodAsync(CancellationToken ct = default);
        /// <summary>The period whose date range contains <paramref name="date"/> (any status), or null.</summary>
        Task<FiscalPeriod?> GetContainingAsync(DateTime date, CancellationToken ct = default);
        Task AddAsync(FiscalPeriod period, CancellationToken ct = default);
        Task UpdateAsync(FiscalPeriod period, CancellationToken ct = default);
    }

    public interface IJournalLineRepository
    {
        Task<List<JournalLine>> GetByJournalEntryIdAsync(Guid journalEntryId, CancellationToken ct = default);
        Task<List<AccountBalanceSummary>> GetAccountBalancesAsync(
            DateTime? fromDate, DateTime? toDate, Guid? fiscalPeriodId, CancellationToken ct = default);
        /// <summary>Like <see cref="GetAccountBalancesAsync"/> but leaves out the entries of one reference type (the P&amp;L leaves out the period-closing entries).</summary>
        Task<List<AccountBalanceSummary>> GetActivityBalancesAsync(
            DateTime? fromDate, DateTime? toDate, string? excludeReferenceType, CancellationToken ct = default);
        /// <summary>The journal lines of the given accounts in the date range (Posted and Reversed entries), oldest first: by entry date, entry number, line.</summary>
        Task<List<LedgerRow>> GetLedgerLinesAsync(
            IReadOnlyCollection<Guid> accountIds, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
        /// <summary>
        /// Every line of the entries (Posted and Reversed, closing entries left out) dated in the range that have at least one line on one of the
        /// given cash accounts: the cash lines and their counter-lines.
        /// </summary>
        Task<List<CashEntryLine>> GetCashEntryLinesAsync(
            IReadOnlyCollection<Guid> cashAccountIds, DateTime from, DateTime to, CancellationToken ct = default);
        /// <summary>The (entry, account) pairs of the given entries: the counter-accounts shown next to a ledger line.</summary>
        Task<List<EntryAccountRef>> GetEntryAccountsAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<JournalLine> lines, CancellationToken ct = default);
    }

    public interface IClosingPolicyRepository
    {
        /// <summary>The tenant's policy (tracked), or null when it never set one (= Monthly, December year end).</summary>
        Task<ClosingPolicy?> GetAsync(CancellationToken ct = default);
        Task AddAsync(ClosingPolicy policy, CancellationToken ct = default);
    }

    public interface IReportProfileRepository
    {
        /// <summary>The tenant's report profile (tracked), or null when it never set one (= plain VND, no header).</summary>
        Task<ReportProfile?> GetAsync(CancellationToken ct = default);
        Task AddAsync(ReportProfile profile, CancellationToken ct = default);
    }

    public interface IPostingAccountMappingRepository
    {
        /// <summary>Tracked, so a changed mapping is saved by the unit of work.</summary>
        Task<List<PostingAccountMapping>> GetAllAsync(CancellationToken ct = default);
        Task AddAsync(PostingAccountMapping mapping, CancellationToken ct = default);
    }

    public interface ITaxRateRepository
    {
        Task<TaxRate?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<TaxRate>> GetAllAsync(bool? activeOnly = null, CancellationToken ct = default);
        Task AddAsync(TaxRate taxRate, CancellationToken ct = default);
        Task UpdateAsync(TaxRate taxRate, CancellationToken ct = default);
    }

    public interface IBankAccountRepository
    {
        Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<BankAccount>> GetAllAsync(bool? activeOnly = null, CancellationToken ct = default);
        Task AddAsync(BankAccount bankAccount, CancellationToken ct = default);
        Task UpdateAsync(BankAccount bankAccount, CancellationToken ct = default);
    }

    public interface IBankStatementRepository
    {
        Task<BankStatement?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<BankStatement>> GetByBankAccountIdAsync(Guid bankAccountId, CancellationToken ct = default);
        Task AddAsync(BankStatement statement, CancellationToken ct = default);
        Task UpdateAsync(BankStatement statement, CancellationToken ct = default);
    }
}
