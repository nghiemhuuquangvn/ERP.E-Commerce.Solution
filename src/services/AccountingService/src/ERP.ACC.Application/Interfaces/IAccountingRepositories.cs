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

        /// <summary>Invoices that could be long-term on a date: due later than <paramref name="dueAfter"/>, created by <paramref name="asOf"/>, whatever their status today.</summary>
        Task<List<Invoice>> GetMaturityCandidatesAsync(DateTime asOf, DateTime dueAfter, CancellationToken ct = default);
        /// <summary>Invoices in a foreign currency created by <paramref name="asOf"/>, whatever their status today (the period-end revaluation, ROADMAP P1-4).</summary>
        Task<List<Invoice>> GetForeignCreatedByAsync(DateTime asOf, CancellationToken ct = default);
        Task AddAsync(Invoice invoice, CancellationToken ct = default);
        Task UpdateAsync(Invoice invoice, CancellationToken ct = default);
    }

    public interface IBillRepository
    {
        Task<Bill?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Bill>> GetAllAsync(CancellationToken ct = default);
        Task<List<Bill>> GetBySupplierIdAsync(Guid supplierId, CancellationToken ct = default);
        Task<Bill?> GetByPurchaseOrderIdAsync(Guid purchaseOrderId, CancellationToken ct = default);
        Task<Bill?> GetBySupplierInvoiceIdAsync(Guid supplierInvoiceId, CancellationToken ct = default);
        /// <summary>Approved bills of a purchase order that still have something to pay, oldest due date first (tracked: credits are applied to them).</summary>
        Task<List<Bill>> GetOpenApprovedByPurchaseOrderIdAsync(Guid purchaseOrderId, CancellationToken ct = default);
        Task<List<Bill>> GetByStatusAsync(int status, CancellationToken ct = default);
        Task<List<Bill>> GetUnpaidBillsAsync(CancellationToken ct = default);

        /// <summary>Approved or paid bills due later than <paramref name="dueAfter"/> that existed on <paramref name="asOf"/>, whatever their status today.</summary>
        Task<List<Bill>> GetMaturityCandidatesAsync(DateTime asOf, DateTime dueAfter, CancellationToken ct = default);
        /// <summary>Bills in a foreign currency created by <paramref name="asOf"/>, whatever their status today.</summary>
        Task<List<Bill>> GetForeignCreatedByAsync(DateTime asOf, CancellationToken ct = default);
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
        /// <summary>Which of the source keys already have an entry (any status) — used to find documents that were never posted.</summary>
        Task<HashSet<string>> ExistingSourceKeysAsync(IReadOnlyCollection<string> sourceKeys, CancellationToken ct = default);

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

    public interface INoteTextRepository
    {
        /// <summary>Every note text of the tenant (tracked, so a changed text is saved by the unit of work).</summary>
        Task<List<NoteText>> GetAllAsync(CancellationToken ct = default);
        Task AddAsync(NoteText text, CancellationToken ct = default);

        /// <summary>Appends a revision (the history of a text, ROADMAP P1-2l).</summary>
        Task AddRevisionAsync(NoteTextRevision revision, CancellationToken ct = default);

        /// <summary>The revisions of one text, newest first.</summary>
        Task<List<NoteTextRevision>> GetRevisionsAsync(string sectionKey, string language, int fiscalYear, int take, CancellationToken ct = default);
    }

    public interface IVatLineRepository
    {
        /// <summary>The stored VAT lines of the documents (no tracking); a document with none has an implicit line.</summary>
        Task<List<VatLine>> ListAsync(int documentType, IReadOnlyCollection<Guid> documentIds, CancellationToken ct = default);

        /// <summary>Replaces the lines of one document (an empty list removes them).</summary>
        Task ReplaceAsync(int documentType, Guid documentId, IEnumerable<VatLine> lines, CancellationToken ct = default);
    }

    public interface IEInvoiceRepository
    {
        /// <summary>The e-invoice of an invoice that is not cancelled (issued or failed), tracked; null when none.</summary>
        Task<EInvoice?> GetActiveByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);

        /// <summary>Every e-invoice of an invoice (originals, replacements, adjustments), oldest first, no tracking.</summary>
        Task<List<EInvoice>> ListByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);

        /// <summary>The latest e-invoice of an invoice whatever its status (no tracking); null when none.</summary>
        Task<EInvoice?> GetLatestByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
        Task AddAsync(EInvoice einvoice, CancellationToken ct = default);
    }

    public interface ILoanRepository
    {
        /// <summary>The loan with its installments (tracked, so a repayment is saved by the unit of work).</summary>
        Task<Loan?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Loan>> ListAsync(CancellationToken ct = default);
        Task AddAsync(Loan loan, CancellationToken ct = default);
    }

    public interface IOffBalanceRepository
    {
        Task<OffBalanceEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
        /// <summary>Entries newest first; every filter is optional (dates inclusive).</summary>
        Task<List<OffBalanceEntry>> ListAsync(Guid? accountId, DateTime? from, DateTime? to, CancellationToken ct = default);
        Task AddAsync(OffBalanceEntry entry, CancellationToken ct = default);
    }

    public interface IReportProfileRepository
    {
        /// <summary>The tenant's report profile (tracked), or null when it never set one (= plain VND, no header).</summary>
        Task<ReportProfile?> GetAsync(CancellationToken ct = default);
        Task AddAsync(ReportProfile profile, CancellationToken ct = default);
    }

    public interface IStatementMappingRepository
    {
        /// <summary>Tracked, so a changed or removed mapping is saved by the unit of work.</summary>
        Task<List<StatementAccountMapping>> GetAllAsync(CancellationToken ct = default);
        Task AddAsync(StatementAccountMapping mapping, CancellationToken ct = default);
        void Remove(StatementAccountMapping mapping);
    }

    public interface IPaymentApplicationRepository
    {
        Task AddAsync(PaymentApplication application, CancellationToken ct = default);

        /// <summary>Every application of the given invoices and bills (whatever their date).</summary>
        Task<List<PaymentApplication>> ListForDocumentsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken ct = default);


        /// <summary>
        /// Every application (whatever its date) of the documents <see cref="IInvoiceRepository.GetMaturityCandidatesAsync"/> / <see cref="IBillRepository.GetMaturityCandidatesAsync"/>
        /// return for the same arguments. Payments after the date are needed too: a document with no row at all is told apart from one paid later.
        /// </summary>
        Task<List<PaymentApplication>> ListForMaturityAsync(DateTime asOf, DateTime dueAfter, CancellationToken ct = default);
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

    public interface IBankStatementLineRepository
    {
        Task<BankStatementLine?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>The lines of a statement, oldest first (tracked, so a match is saved by the unit of work).</summary>
        Task<List<BankStatementLine>> ListByStatementAsync(Guid bankStatementId, CancellationToken ct = default);

        /// <summary>Every line of the bank account dated up to <paramref name="to"/>, whichever statement it belongs to.</summary>
        Task<List<BankStatementLine>> ListByBankAccountAsync(Guid bankAccountId, DateTime to, CancellationToken ct = default);

        /// <summary>The ledger lines already matched to a statement line (any bank account of the tenant).</summary>
        Task<HashSet<Guid>> MatchedJournalLineIdsAsync(CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<BankStatementLine> lines, CancellationToken ct = default);
        void Remove(BankStatementLine line);
    }

    public interface IBankStatementRepository
    {
        Task<BankStatement?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<BankStatement>> GetByBankAccountIdAsync(Guid bankAccountId, CancellationToken ct = default);
        Task AddAsync(BankStatement statement, CancellationToken ct = default);
        Task UpdateAsync(BankStatement statement, CancellationToken ct = default);
    }
}
