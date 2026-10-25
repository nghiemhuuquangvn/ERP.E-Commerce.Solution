using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// Everything the indirect method reads for one range: the cash movements and cash balances of the direct method, the cumulative balances the day before the
    /// range and at its end (working capital), and the activity of the range with closing entries left out (profit, interest).
    /// </summary>
    public sealed record IndirectColumn(
        CashFlowColumn Direct,
        IReadOnlyDictionary<Guid, AccountBalanceSummary> Before,
        IReadOnlyDictionary<Guid, AccountBalanceSummary> After,
        IReadOnlyDictionary<Guid, AccountBalanceSummary> Activity);

    /// <summary>Evaluates <see cref="IndirectCashFlowLayouts"/> (B03-DN, indirect method) over one or two ranges.</summary>
    public static class IndirectCashFlowEngine
    {
        // the cash rows of the direct form that rows 14 / 15 stand for
        private static readonly Dictionary<string, string> DirectCode = new() { ["14"] = "04", ["15"] = "05" };

        public static FinancialStatementDto Evaluate(
            IndirectCashFlowLayout layout, IReadOnlyCollection<Account> accounts, IndirectColumn current, IndirectColumn? prior,
            decimal divisor = 1m, ReportHeaderDto? header = null)
        {
            var now = Compute(layout, accounts, current, divisor);
            var before = prior != null ? Compute(layout, accounts, prior, divisor) : null;
            var exactNow = divisor == 1m ? now : Compute(layout, accounts, current, 1m);
            var exactBefore = prior == null ? null : divisor == 1m ? before : Compute(layout, accounts, prior, 1m);

            var statement = new FinancialStatementDto
            {
                LayoutId = layout.Id,
                Title = layout.Title,
                Notes = layout.Notes.ToList(),
                UnitDivisor = divisor,
                Unit = divisor == 1_000m ? "thousand VND" : divisor == 1_000_000m ? "million VND" : "VND",
                Header = header,
                Rows = layout.Rows.Select(r => new StatementRowDto
                {
                    Code = r.Code,
                    Name = r.Name,
                    Indent = r.Indent,
                    IsTotal = r.IsTotal,
                    Amount = now.Amounts[r.Code],
                    PriorAmount = before?.Amounts[r.Code],
                }).ToList(),
            };

            // accounts whose balance moved and that the form neither follows nor leaves out on purpose, and cash movements the direct rows could not place
            var unmapped = new Dictionary<string, UnmappedAccountDto>();
            foreach (var column in prior == null ? new[] { current } : new[] { current, prior })
            {
                foreach (var account in accounts.Where(a => a.AccountType is (int)AccountType.Asset or (int)AccountType.Liability or (int)AccountType.Equity && !a.IsCash))
                {
                    var moved = Net(column.After, account) - Net(column.Before, account); // credit minus debit
                    if (moved == 0m || layout.CoveredPrefixes.Any(p => account.AccountNumber.StartsWith(p, StringComparison.Ordinal)))
                        continue;
                    unmapped.TryGetValue(account.AccountNumber, out var known);
                    unmapped[account.AccountNumber] = new UnmappedAccountDto
                    {
                        AccountNumber = account.AccountNumber, AccountName = account.Name, Balance = (known?.Balance ?? 0m) - moved,
                    };
                }
            }
            foreach (var movement in now.DirectUnmapped.Concat(before?.DirectUnmapped ?? new List<CashMovement>()))
            {
                unmapped.TryGetValue(movement.Account.AccountNumber, out var known);
                unmapped[movement.Account.AccountNumber] = new UnmappedAccountDto
                {
                    AccountNumber = movement.Account.AccountNumber, AccountName = movement.Account.Name, Balance = (known?.Balance ?? 0m) + movement.Cash,
                };
            }
            statement.Unmapped = unmapped.Values.OrderBy(u => u.AccountNumber).ToList();

            // the two methods describe the same cash: operating (20) must agree, and the closing cash (70) must be the cash on the books
            var difference = exactNow.Amounts["20"] - exactNow.DirectOperating;
            var priorDifference = exactBefore == null ? 0m : exactBefore.Amounts["20"] - exactBefore.DirectOperating;
            var cashAgrees = exactNow.Amounts["70"] == current.Direct.ClosingCash && (exactBefore == null || exactBefore.Amounts["70"] == prior!.Direct.ClosingCash);
            statement.IsBalanced = difference == 0m && priorDifference == 0m && cashAgrees;

            if (difference != 0m || priorDifference != 0m)
                statement.Notes.Add($"Indirect operating cash flow (20) differs from the direct method by {StatementEngine.RoundTo(difference, divisor)} {statement.Unit}"
                    + (priorDifference != 0m ? $" (prior column {StatementEngine.RoundTo(priorDifference, divisor)})" : string.Empty)
                    + ": a non-cash transaction or an account the form does not take (see the unmapped list); the accountant clears it.");
            else if (cashAgrees && now.Amounts["70"] != StatementEngine.RoundTo(current.Direct.ClosingCash, divisor))
                statement.Notes.Add($"Rounding difference of {now.Amounts["70"] - StatementEngine.RoundTo(current.Direct.ClosingCash, divisor)} {statement.Unit} between closing cash (70) and the rounded cash on the books: the exact amounts agree.");
            if (statement.Unmapped.Count > 0)
                statement.Notes.Add("Some accounts or cash movements are outside the form's rows; map them or extend the layout.");
            return statement;
        }

        private sealed record Computed(Dictionary<string, decimal> Amounts, decimal DirectOperating, List<CashMovement> DirectUnmapped);

        /// <summary>Credit minus debit of one account in a set of balances.</summary>
        private static decimal Net(IReadOnlyDictionary<Guid, AccountBalanceSummary> balances, Account account) =>
            balances.TryGetValue(account.Id, out var b) ? b.TotalCredit - b.TotalDebit : 0m;

        private static bool Matches(Account account, IndirectRow row) =>
            row.Prefixes.Any(p => account.AccountNumber.StartsWith(p, StringComparison.Ordinal))
            && !row.Except.Any(p => account.AccountNumber.StartsWith(p, StringComparison.Ordinal));

        private static Computed Compute(IndirectCashFlowLayout layout, IReadOnlyCollection<Account> accounts, IndirectColumn column, decimal divisor)
        {
            var direct = CashFlowStatementEngine.Compute(CashFlowLayouts.Layout, column.Direct, divisor);
            var byCode = layout.Rows.ToDictionary(r => r.Code);
            var memo = new Dictionary<string, decimal>();

            decimal Exact(IndirectRow row) => row.Source switch
            {
                IndirectSource.ProfitBeforeTax => accounts
                    .Where(a => a.AccountType is (int)AccountType.Revenue or (int)AccountType.Expense && !a.AccountNumber.StartsWith("821", StringComparison.Ordinal))
                    .Sum(a => Net(column.Activity, a)),
                IndirectSource.CreditIncrease or IndirectSource.DebitIncreaseNegated => accounts.Where(a => Matches(a, row))
                    .Sum(a => Net(column.After, a) - Net(column.Before, a)),
                IndirectSource.ExpenseAddBack => accounts.Where(a => Matches(a, row)).Sum(a => -Net(column.Activity, a)),
                IndirectSource.IncomeRemoved => accounts.Where(a => Matches(a, row)).Sum(a => -Net(column.Activity, a)),
                _ => 0m,
            };

            decimal Value(string code)
            {
                if (memo.TryGetValue(code, out var known))
                    return known;
                var row = byCode[code];
                var amount = row.Source switch
                {
                    IndirectSource.Formula => row.Formula.Sum(p => p.Sign * Value(p.Code)),
                    IndirectSource.Direct => direct.Amounts[DirectCode.GetValueOrDefault(code, code)],
                    IndirectSource.OpeningCash => StatementEngine.RoundTo(column.Direct.OpeningCash, divisor),
                    IndirectSource.FxEffect or IndirectSource.NotUsed => 0m,
                    _ => StatementEngine.RoundTo(Exact(row), divisor),
                };
                memo[code] = amount;
                return amount;
            }

            foreach (var row in layout.Rows)
                Value(row.Code);

            // the direct operating total this method must agree with (rows 01 … 07 of the direct form), in the same unit
            return new Computed(memo, direct.Amounts["20"], direct.Unmapped);
        }
    }
}
