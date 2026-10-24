using ERP.ACC.Application.DTOs;
using MediatR;

namespace ERP.ACC.Application.Queries
{
    public class GetAccountQuery : IRequest<AccountDto?>
    {
        public Guid AccountId { get; set; }
    }

    public class ListAccountsQuery : IRequest<List<AccountDto>> { }

    public class GetAccountTreeQuery : IRequest<List<AccountTreeNodeDto>> { }

    public class GetInvoiceQuery : IRequest<InvoiceDto?>
    {
        public Guid InvoiceId { get; set; }
    }

    public class ListInvoicesQuery : IRequest<List<InvoiceDto>>
    {
        public Guid? CustomerId { get; set; }
        public int? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class GetBillQuery : IRequest<BillDto?>
    {
        public Guid BillId { get; set; }
    }

    public class ListBillsQuery : IRequest<List<BillDto>>
    {
        public Guid? SupplierId { get; set; }
        public int? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class GetPostingProfileQuery : IRequest<PostingProfileDto>
    {
    }

    public class GetJournalEntryQuery : IRequest<JournalEntryDto?>
    {
        public Guid JournalEntryId { get; set; }
    }

    public class ListJournalEntriesQuery : IRequest<List<JournalEntryDto>>
    {
        public Guid? FiscalPeriodId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class GetFiscalPeriodQuery : IRequest<FiscalPeriodDto?>
    {
        public Guid FiscalPeriodId { get; set; }
    }

    public class ListFiscalPeriodsQuery : IRequest<List<FiscalPeriodDto>> { }

    public class GetARAgingQuery : IRequest<List<ARAgingDto>>
    {
        public DateTime? AsOfDate { get; set; }
    }

    public class GetAPAgingQuery : IRequest<List<APAgingDto>>
    {
        public DateTime? AsOfDate { get; set; }
    }

    public class GetTrialBalanceQuery : IRequest<List<TrialBalanceLineDto>>
    {
        public Guid? FiscalPeriodId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool HideZeroBalance { get; set; } = true;

        /// <summary>Each parent account also carries the sum of its sub-accounts.</summary>
        public bool RollUp { get; set; } = true;
    }

    /// <summary>The same trial balance with its grand totals and the balanced check.</summary>
    public class GetTrialBalanceFullQuery : IRequest<TrialBalanceDto>
    {
        public Guid? FiscalPeriodId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool HideZeroBalance { get; set; } = true;
        public bool RollUp { get; set; } = true;
    }

    public class GetCashFlowQuery : IRequest<CashFlowStatementDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public class GetAccountLedgerQuery : IRequest<AccountLedgerDto>
    {
        public Guid AccountId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool IncludeSubAccounts { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        /// <summary>Return every row of the range, ignoring the paging (used by the CSV export).</summary>
        public bool All { get; set; }
    }

    public class GetProfitAndLossQuery : IRequest<ProfitAndLossDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public class GetBalanceSheetQuery : IRequest<BalanceSheetDto>
    {
        public DateTime AsOfDate { get; set; }
    }

    public class ListTaxRatesQuery : IRequest<List<TaxRateDto>>
    {
        public bool? ActiveOnly { get; set; }
    }

    public class GetTaxRateQuery : IRequest<TaxRateDto?>
    {
        public Guid TaxRateId { get; set; }
    }

    public class ListBankAccountsQuery : IRequest<List<BankAccountDto>>
    {
        public bool? ActiveOnly { get; set; }
    }

    public class GetBankAccountQuery : IRequest<BankAccountDto?>
    {
        public Guid BankAccountId { get; set; }
    }

    public class ListBankStatementsQuery : IRequest<List<BankStatementDto>>
    {
        public Guid BankAccountId { get; set; }
    }
}
