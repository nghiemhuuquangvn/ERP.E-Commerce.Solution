using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;

namespace ERP.ACC.Application.Interfaces
{
    public interface IAccountRepository
    {
        Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Account>> GetAllAsync(CancellationToken ct = default);
        Task AddAsync(Account account, CancellationToken ct = default);
        Task UpdateAsync(Account account, CancellationToken ct = default);
    }

    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Invoice>> GetAllAsync(CancellationToken ct = default);
        Task<List<Invoice>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
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
        Task AddAsync(JournalEntry entry, CancellationToken ct = default);
        Task UpdateAsync(JournalEntry entry, CancellationToken ct = default);
    }

    public interface IFiscalPeriodRepository
    {
        Task<FiscalPeriod?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<FiscalPeriod>> GetAllAsync(CancellationToken ct = default);
        Task<FiscalPeriod?> GetOpenPeriodAsync(CancellationToken ct = default);
        Task AddAsync(FiscalPeriod period, CancellationToken ct = default);
        Task UpdateAsync(FiscalPeriod period, CancellationToken ct = default);
    }

    public interface IJournalLineRepository
    {
        Task<List<JournalLine>> GetByJournalEntryIdAsync(Guid journalEntryId, CancellationToken ct = default);
        Task<List<AccountBalanceSummary>> GetAccountBalancesAsync(
            DateTime? fromDate, DateTime? toDate, Guid? fiscalPeriodId, CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<JournalLine> lines, CancellationToken ct = default);
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
