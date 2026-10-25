using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using MediatR;

namespace ERP.ACC.Application.Reports
{
    public class LoanReconciliationLineDto
    {
        public Guid LoanId { get; set; }
        public string LoanNumber { get; set; } = string.Empty;
        public string Lender { get; set; } = string.Empty;
        public bool PostedToLedger { get; set; }

        /// <summary>Principal still owed on the date, from the repayment schedule.</summary>
        public decimal Outstanding { get; set; }
    }

    /// <summary>The loan ledger against the books: what the schedules say is owed, what the Loan payable account says, and the difference (ROADMAP P1-2j).</summary>
    public class LoanReconciliationDto
    {
        public DateTime AsOfDate { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal LedgerTotal { get; set; }
        public decimal BookBalance { get; set; }
        public decimal Difference { get; set; }
        public bool IsReconciled { get; set; }
        public List<LoanReconciliationLineDto> Loans { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }

    public class GetLoanReconciliationQuery : IRequest<LoanReconciliationDto>
    {
        public DateTime AsOfDate { get; set; }
    }

    public class GetLoanReconciliationQueryHandler : IRequestHandler<GetLoanReconciliationQuery, LoanReconciliationDto>
    {
        private readonly ILoanRepository _loans;
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;
        private readonly IPostingAccountMappingRepository _mappings;

        public GetLoanReconciliationQueryHandler(ILoanRepository loans, IAccountRepository accounts, IJournalLineRepository lines, IPostingAccountMappingRepository mappings)
        {
            _loans = loans;
            _accounts = accounts;
            _lines = lines;
            _mappings = mappings;
        }

        public async Task<LoanReconciliationDto> Handle(GetLoanReconciliationQuery request, CancellationToken ct)
        {
            var day = request.AsOfDate.Date;
            var accounts = await _accounts.GetAllAsync(ct);
            var mapping = (await _mappings.GetAllAsync(ct)).FirstOrDefault(m => m.PostingKey == PostingKeys.LoanPayable);
            Account? account = mapping != null ? accounts.FirstOrDefault(a => a.Id == mapping.AccountId) : null;
            if (account == null)
            {
                // the key is mapped by the first posting; until then it is the default of the tenant's chart
                var number = ChartTemplates.Find(ChartTemplates.Detect(accounts))?.PostingMap.GetValueOrDefault(PostingKeys.LoanPayable)
                    ?? PostingKeys.DefaultAccountNumbers.GetValueOrDefault(PostingKeys.LoanPayable);
                account = accounts.FirstOrDefault(a => a.AccountNumber == number);
            }

            var result = new LoanReconciliationDto { AsOfDate = day };
            var loans = (await _loans.ListAsync(ct)).Where(l => l.Status != (int)LoanStatus.Cancelled && l.StartDate.Date <= day).ToList();
            result.Loans = loans.Select(l => new LoanReconciliationLineDto
            {
                LoanId = l.Id, LoanNumber = l.LoanNumber, Lender = l.Lender, PostedToLedger = l.PostedToLedger, Outstanding = l.OutstandingAt(day),
            }).OrderBy(l => l.LoanNumber).ToList();
            result.LedgerTotal = result.Loans.Sum(l => l.Outstanding);

            if (account == null)
            {
                result.Notes.Add("The Loan payable posting key points to no account: complete the posting profile.");
                result.Difference = result.LedgerTotal;
                return result;
            }

            // a parent account (a VAS chart has 341 with children) carries the balances of its children
            var ids = accounts.Where(a => a.AccountNumber.StartsWith(account.AccountNumber, StringComparison.Ordinal)).Select(a => a.Id).ToHashSet();
            var balances = await _lines.GetAccountBalancesAsync(null, day, null, ct);
            result.AccountNumber = account.AccountNumber;
            result.AccountName = account.Name;
            result.BookBalance = balances.Where(b => ids.Contains(b.AccountId)).Sum(b => b.TotalCredit - b.TotalDebit);
            result.Difference = result.LedgerTotal - result.BookBalance;
            result.IsReconciled = result.Difference == 0m;
            if (loans.Any(l => !l.PostedToLedger))
                result.Notes.Add("Some loans are not posted to the books: their drawdown and repayments are journal entries made by hand, so a difference can be expected.");
            if (account.AccountNumber == "2100")
                result.Notes.Add("This chart keeps borrowings in the same account as accrued liabilities (2100), so the book balance includes those too.");
            return result;
        }
    }
}
