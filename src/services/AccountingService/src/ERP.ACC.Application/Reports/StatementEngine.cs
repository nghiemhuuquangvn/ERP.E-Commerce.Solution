using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Reports
{
    public class StatementRowDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Indent { get; set; }
        public bool IsTotal { get; set; }
        public decimal Amount { get; set; }

        /// <summary>The same row for the comparison period; null when no comparison was asked for.</summary>
        public decimal? PriorAmount { get; set; }
    }

    /// <summary>An account with a balance that no row of the statement takes (so the statement would silently leave it out).</summary>
    public class UnmappedAccountDto
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;

        /// <summary>Debit minus credit.</summary>
        public decimal Balance { get; set; }
    }

    public class FinancialStatementDto
    {
        public string LayoutId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        /// <summary>The layout is a draft until the company's accountant has reviewed it.</summary>
        public bool IsDraft { get; set; } = true;
        public string Currency { get; set; } = "VND";
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime? CompareFromDate { get; set; }
        public DateTime? CompareToDate { get; set; }
        public List<StatementRowDto> Rows { get; set; } = new();
        public List<UnmappedAccountDto> Unmapped { get; set; } = new();
        public List<string> Notes { get; set; } = new();

        /// <summary>Balance sheet only: total assets (270) equal total sources (440) in every column; null for other statements.</summary>
        public bool? IsBalanced { get; set; }
    }

    /// <summary>Evaluates a <see cref="StatementLayout"/> over the account balances of one or two periods.</summary>
    public static class StatementEngine
    {
        public static FinancialStatementDto Evaluate(
            StatementLayout layout,
            IReadOnlyCollection<Account> accounts,
            IReadOnlyDictionary<Guid, AccountBalanceSummary> current,
            IReadOnlyDictionary<Guid, AccountBalanceSummary>? prior)
        {
            var amounts = Compute(layout, accounts, current);
            var priorAmounts = prior != null ? Compute(layout, accounts, prior) : null;

            var statement = new FinancialStatementDto
            {
                LayoutId = layout.Id,
                Title = layout.Title,
                Notes = layout.Notes.ToList(),
                Rows = layout.Rows.Select(r => new StatementRowDto
                {
                    Code = r.Code,
                    Name = r.Name,
                    Indent = r.Indent,
                    IsTotal = r.IsTotal,
                    Amount = amounts[r.Code],
                    PriorAmount = priorAmounts?[r.Code],
                }).ToList(),
                Unmapped = Unmapped(layout, accounts, current, prior),
            };

            if (layout.Id == StatementLayouts.BalanceSheet)
            {
                statement.IsBalanced = amounts["270"] == amounts["440"] && (priorAmounts == null || priorAmounts["270"] == priorAmounts["440"]);
                if (!statement.IsBalanced.Value)
                    statement.Notes.Add("Total assets (270) differ from total sources (440): see the unmapped accounts below.");
            }

            if (statement.Unmapped.Count > 0)
                statement.Notes.Add("Some accounts with a balance are not part of this statement's layout; the layout is written for the Vietnamese chart (Vas99). Map them or extend the layout.");

            return statement;
        }

        private static Dictionary<string, decimal> Compute(
            StatementLayout layout, IReadOnlyCollection<Account> accounts, IReadOnlyDictionary<Guid, AccountBalanceSummary> balances)
        {
            var byCode = layout.Rows.ToDictionary(r => r.Code);
            var memo = new Dictionary<string, decimal>();
            var inProgress = new HashSet<string>();

            decimal Value(string code)
            {
                if (memo.TryGetValue(code, out var known))
                    return known;
                if (!inProgress.Add(code))
                    throw new InvalidOperationException($"Statement {layout.Id}: row {code} depends on itself.");

                var row = byCode[code];
                decimal amount;
                if (row.Formula.Count > 0)
                {
                    amount = row.Formula.Sum(p => p.Sign * Value(p.Code));
                }
                else
                {
                    amount = 0m;
                    foreach (var account in accounts)
                    {
                        if (!balances.TryGetValue(account.Id, out var bal))
                            continue;
                        var net = bal.TotalDebit - bal.TotalCredit; // debit minus credit
                        if (net == 0m)
                            continue;
                        foreach (var term in row.Terms)
                        {
                            if (!account.AccountNumber.StartsWith(term.Prefix, StringComparison.Ordinal) || !Accepts(term.Side, net))
                                continue;
                            amount += row.Natural == NormalBalance.Debit ? net : -net;
                            break; // an account counts once per row
                        }
                    }

                    if (row.Special == "UnclosedResult")
                    {
                        // revenue minus expenses that no closing entry has moved yet (credit minus debit)
                        amount += accounts
                            .Where(a => a.AccountType is (int)AccountType.Revenue or (int)AccountType.Expense)
                            .Sum(a => balances.TryGetValue(a.Id, out var b) ? b.TotalCredit - b.TotalDebit : 0m);
                    }
                }

                inProgress.Remove(code);
                memo[code] = amount;
                return amount;
            }

            foreach (var row in layout.Rows)
                Value(row.Code);
            return memo;
        }

        private static bool Accepts(BalanceSide side, decimal debitMinusCredit) => side switch
        {
            BalanceSide.DebitOnly => debitMinusCredit > 0m,
            BalanceSide.CreditOnly => debitMinusCredit < 0m,
            _ => true,
        };

        /// <summary>Accounts of the statement's types with a balance in either period that no term covers (on the side the balance has).</summary>
        public static List<UnmappedAccountDto> Unmapped(
            StatementLayout layout,
            IReadOnlyCollection<Account> accounts,
            IReadOnlyDictionary<Guid, AccountBalanceSummary> current,
            IReadOnlyDictionary<Guid, AccountBalanceSummary>? prior)
        {
            var types = layout.AccountTypes.Select(t => (int)t).ToHashSet();
            var terms = layout.Rows.SelectMany(r => r.Terms).ToList();
            var result = new List<UnmappedAccountDto>();

            foreach (var account in accounts.Where(a => types.Contains(a.AccountType)).OrderBy(a => a.AccountNumber))
            {
                foreach (var balances in prior == null ? new[] { current } : new[] { current, prior })
                {
                    if (!balances.TryGetValue(account.Id, out var bal))
                        continue;
                    var net = bal.TotalDebit - bal.TotalCredit;
                    if (net == 0m)
                        continue;
                    if (terms.Any(t => account.AccountNumber.StartsWith(t.Prefix, StringComparison.Ordinal) && Accepts(t.Side, net)))
                        continue;

                    if (result.All(u => u.AccountNumber != account.AccountNumber))
                        result.Add(new UnmappedAccountDto { AccountNumber = account.AccountNumber, AccountName = account.Name, Balance = net });
                }
            }

            return result;
        }
    }
}
