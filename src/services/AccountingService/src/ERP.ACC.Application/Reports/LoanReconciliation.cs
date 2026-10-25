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

        /// <summary>Interest accrued and not yet paid on the unpaid installments (today's accruals), the balance of the interest-payable account, and their difference (ROADMAP P1-2k).</summary>
        public string InterestAccountNumber { get; set; } = string.Empty;
        public decimal InterestLedger { get; set; }
        public decimal InterestBook { get; set; }
        public decimal InterestDifference { get; set; }
        public bool InterestReconciled { get; set; } = true;
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
            var mappings = await _mappings.GetAllAsync(ct);
            Account? Resolve(string key)
            {
                var mapping = mappings.FirstOrDefault(m => m.PostingKey == key);
                var found = mapping != null ? accounts.FirstOrDefault(a => a.Id == mapping.AccountId) : null;
                if (found != null)
                    return found;
                // the key is mapped by the first posting; until then it is the default of the tenant's chart
                var number = ChartTemplates.Find(ChartTemplates.Detect(accounts))?.PostingMap.GetValueOrDefault(key)
                    ?? PostingKeys.DefaultAccountNumbers.GetValueOrDefault(key);
                return accounts.FirstOrDefault(a => a.AccountNumber == number);
            }

            var account = Resolve(PostingKeys.LoanPayable);
            var interestAccount = Resolve(PostingKeys.InterestPayable);

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

            // interest accrued and not yet paid (the accruals are not kept by date: the check is of today's state)
            result.InterestLedger = loans.Where(l => l.PostedToLedger).SelectMany(l => l.Installments).Where(i => !i.PaidOn.HasValue).Sum(i => i.AccruedInterest);
            var sameAccount = interestAccount != null && interestAccount.Id == account.Id;
            if (interestAccount != null && !sameAccount)
            {
                var interestIds = accounts.Where(a => a.AccountNumber.StartsWith(interestAccount.AccountNumber, StringComparison.Ordinal)).Select(a => a.Id).ToHashSet();
                result.InterestAccountNumber = interestAccount.AccountNumber;
                result.InterestBook = balances.Where(b => interestIds.Contains(b.AccountId)).Sum(b => b.TotalCredit - b.TotalDebit);
                result.InterestDifference = result.InterestLedger - result.InterestBook;
                result.InterestReconciled = result.InterestDifference == 0m;
                if (day < DateTime.UtcNow.Date)
                    result.Notes.Add("The interest check compares today's accruals with the balance on the date: use today's date for it.");
            }

            // one account for both (the 14-account chart): its balance is principal plus accrued interest
            result.Difference = result.LedgerTotal + (sameAccount ? result.InterestLedger : 0m) - result.BookBalance;
            result.IsReconciled = result.Difference == 0m;
            if (sameAccount)
                result.Notes.Add("Loan payable and interest payable share this account: the ledger side adds the accrued interest.");
            if (loans.Any(l => !l.PostedToLedger))
                result.Notes.Add("Some loans are not posted to the books: their drawdown and repayments are journal entries made by hand, so a difference can be expected.");
            if (account.AccountNumber == "2100")
                result.Notes.Add("This chart keeps borrowings in the same account as accrued liabilities (2100), so the book balance includes those too.");
            return result;
        }
    }
}
