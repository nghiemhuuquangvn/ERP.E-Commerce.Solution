using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    /// <summary>
    /// Shared by the ledger and the trial balance: the accounts below an account in the tree, and the sign convention of a balance
    /// (debit minus credit for a debit-normal account, credit minus debit otherwise).
    /// </summary>
    internal static class AccountTree
    {
        /// <summary>The account itself plus every descendant (cycle-safe).</summary>
        public static List<Account> SelfAndDescendants(Account root, IReadOnlyCollection<Account> all)
        {
            var childrenOf = all.Where(a => a.ParentAccountId.HasValue).ToLookup(a => a.ParentAccountId!.Value);
            var result = new List<Account>();
            var seen = new HashSet<Guid>();
            var stack = new Stack<Account>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!seen.Add(current.Id))
                    continue;
                result.Add(current);
                foreach (var child in childrenOf[current.Id])
                    stack.Push(child);
            }
            return result;
        }

        public static decimal Signed(Account account, decimal debit, decimal credit) =>
            account.NormalBalance == (int)NormalBalance.Debit ? debit - credit : credit - debit;
    }

    /// <summary>
    /// The account ledger (sổ cái / sổ chi tiết tài khoản): opening balance, every journal line in the range with the counter-accounts and a
    /// running balance, closing balance. A parent account includes its sub-accounts. Posted and Reversed entries count (Draft and Voided do not).
    /// </summary>
    public class GetAccountLedgerQueryHandler : IRequestHandler<GetAccountLedgerQuery, AccountLedgerDto>
    {
        private const int MaxPageSize = 500;
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;

        public GetAccountLedgerQueryHandler(IAccountRepository accounts, IJournalLineRepository lines)
        {
            _accounts = accounts;
            _lines = lines;
        }

        public async Task<AccountLedgerDto> Handle(GetAccountLedgerQuery request, CancellationToken cancellationToken)
        {
            if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value.Date > request.ToDate.Value.Date)
                throw new BusinessRuleException("Accounting", "The start date is after the end date.");

            var all = await _accounts.GetAllAsync(cancellationToken);
            var account = all.FirstOrDefault(a => a.Id == request.AccountId)
                ?? throw new KeyNotFoundException($"Account {request.AccountId} not found.");

            var members = request.IncludeSubAccounts ? AccountTree.SelfAndDescendants(account, all) : new List<Account> { account };
            var memberIds = members.Select(m => m.Id).ToHashSet();
            var debitNormal = account.NormalBalance == (int)NormalBalance.Debit;

            // Opening = everything before the first day of the range (members can be of either side, so add up debit minus credit).
            var opening = 0m;
            if (request.FromDate.HasValue)
            {
                var before = await _lines.GetAccountBalancesAsync(null, request.FromDate.Value.Date.AddDays(-1), null, cancellationToken);
                opening = before.Where(b => memberIds.Contains(b.AccountId)).Sum(b => b.TotalDebit - b.TotalCredit);
            }

            var rows = await _lines.GetLedgerLinesAsync(memberIds, request.FromDate, request.ToDate, cancellationToken);

            var running = opening;
            var balances = new List<decimal>(rows.Count);
            foreach (var row in rows)
            {
                running += row.Debit - row.Credit;
                balances.Add(running);
            }

            var pageSize = request.All ? Math.Max(1, rows.Count) : Math.Clamp(request.PageSize, 1, MaxPageSize);
            var pageNumber = request.All ? 1 : Math.Max(1, request.PageNumber);
            var pageRows = rows.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            // Counter-accounts only for the page that is shown.
            var entryAccounts = await _lines.GetEntryAccountsAsync(pageRows.Select(r => r.EntryId).Distinct().ToList(), cancellationToken);
            var numberById = all.ToDictionary(a => a.Id, a => a.AccountNumber);
            string Counter(LedgerRow row) => string.Join(", ",
                entryAccounts.Where(e => e.EntryId == row.EntryId && !memberIds.Contains(e.AccountId))
                    .Select(e => numberById.GetValueOrDefault(e.AccountId, "?")).Distinct().OrderBy(n => n));

            decimal Side(decimal debitMinusCredit) => debitNormal ? debitMinusCredit : -debitMinusCredit;

            var totalDebit = rows.Sum(r => r.Debit);
            var totalCredit = rows.Sum(r => r.Credit);
            return new AccountLedgerDto
            {
                AccountId = account.Id,
                AccountNumber = account.AccountNumber,
                AccountName = account.Name,
                NormalBalance = ((NormalBalance)account.NormalBalance).ToString(),
                IncludesSubAccounts = members.Count > 1,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                OpeningBalance = Side(opening),
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                ClosingBalance = Side(opening + totalDebit - totalCredit),
                TotalRows = rows.Count,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Lines = pageRows.Select((row, i) => new LedgerLineDto
                {
                    EntryId = row.EntryId,
                    EntryNumber = row.EntryNumber,
                    EntryDate = row.EntryDate,
                    Description = string.IsNullOrWhiteSpace(row.LineDescription) ? row.EntryDescription : row.LineDescription,
                    AccountNumber = numberById.GetValueOrDefault(row.AccountId, "?"),
                    CounterAccounts = Counter(row),
                    Debit = row.Debit,
                    Credit = row.Credit,
                    Balance = Side(balances[(pageNumber - 1) * pageSize + i]),
                    Status = ((JournalStatus)row.Status).ToString(),
                    IsClosingEntry = row.ReferenceType == JournalEntry.PeriodClosingType,
                }).ToList(),
            };
        }
    }
}
