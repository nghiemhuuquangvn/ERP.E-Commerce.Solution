using ERP.ACC.Application.DTOs;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;

namespace ERP.ACC.Application.Handlers
{
    /// <summary>
    /// The Vietnamese trial balance (bảng cân đối số phát sinh): per account an opening balance, the period's debit and credit movement and a
    /// closing balance. Opening and closing balances are shown as a debit or a credit amount (the net of the account); with roll-up each parent
    /// also carries the sum of its sub-accounts. The grand totals add the accounts that are not rolled into a parent, so nothing is counted twice.
    /// </summary>
    internal static class TrialBalanceBuilder
    {
        public static TrialBalanceDto Build(
            IReadOnlyCollection<Account> accounts,
            IReadOnlyDictionary<Guid, AccountBalanceSummary> opening,
            IReadOnlyDictionary<Guid, AccountBalanceSummary> period,
            bool rollUp,
            bool hideZero)
        {
            // net debit (debit minus credit) of opening and period movement per account
            var data = accounts.ToDictionary(
                a => a.Id,
                a =>
                {
                    opening.TryGetValue(a.Id, out var o);
                    period.TryGetValue(a.Id, out var p);
                    return new Cell((o?.TotalDebit ?? 0m) - (o?.TotalCredit ?? 0m), p?.TotalDebit ?? 0m, p?.TotalCredit ?? 0m);
                });

            var byId = accounts.ToDictionary(a => a.Id);
            var hasChildren = new HashSet<Guid>(accounts.Where(a => a.ParentAccountId.HasValue && byId.ContainsKey(a.ParentAccountId.Value)).Select(a => a.ParentAccountId!.Value));

            Dictionary<Guid, Cell> shown = data;
            if (rollUp)
            {
                shown = accounts.ToDictionary(a => a.Id, a => data[a.Id]);
                foreach (var account in accounts)
                {
                    // add this account's own figures to every ancestor
                    var guard = 0;
                    var current = account;
                    while (current.ParentAccountId.HasValue && byId.TryGetValue(current.ParentAccountId.Value, out var parent) && guard++ <= accounts.Count)
                    {
                        shown[parent.Id] = shown[parent.Id] + data[account.Id];
                        current = parent;
                    }
                }
            }

            var lines = new List<TrialBalanceLineDto>();
            foreach (var account in accounts.OrderBy(a => a.AccountNumber))
            {
                var cell = shown[account.Id];
                if (hideZero && cell.IsZero)
                    continue;

                var closingNet = cell.OpeningNet + cell.PeriodDebit - cell.PeriodCredit;
                var isDebitNormal = account.NormalBalance == (int)NormalBalance.Debit;
                lines.Add(new TrialBalanceLineDto
                {
                    AccountId = account.Id,
                    AccountNumber = account.AccountNumber,
                    AccountName = account.Name,
                    AccountType = ((AccountType)account.AccountType).ToString(),
                    NormalBalance = ((NormalBalance)account.NormalBalance).ToString(),
                    // legacy fields: the period's movement and its balance on the normal side
                    TotalDebit = cell.PeriodDebit,
                    TotalCredit = cell.PeriodCredit,
                    Balance = isDebitNormal ? cell.PeriodDebit - cell.PeriodCredit : cell.PeriodCredit - cell.PeriodDebit,
                    Level = account.Level,
                    ParentAccountId = account.ParentAccountId,
                    IsParent = hasChildren.Contains(account.Id),
                    OpeningDebit = Math.Max(cell.OpeningNet, 0m),
                    OpeningCredit = Math.Max(-cell.OpeningNet, 0m),
                    PeriodDebit = cell.PeriodDebit,
                    PeriodCredit = cell.PeriodCredit,
                    ClosingDebit = Math.Max(closingNet, 0m),
                    ClosingCredit = Math.Max(-closingNet, 0m),
                });
            }

            // totals over the accounts that are not rolled into a parent (without roll-up every account counts once)
            var totalLines = rollUp ? lines.Where(l => !l.IsParent) : lines;
            var totals = new TrialBalanceTotalsDto
            {
                OpeningDebit = totalLines.Sum(l => l.OpeningDebit),
                OpeningCredit = totalLines.Sum(l => l.OpeningCredit),
                PeriodDebit = totalLines.Sum(l => l.PeriodDebit),
                PeriodCredit = totalLines.Sum(l => l.PeriodCredit),
                ClosingDebit = totalLines.Sum(l => l.ClosingDebit),
                ClosingCredit = totalLines.Sum(l => l.ClosingCredit),
            };
            totals.IsBalanced = totals.OpeningDebit == totals.OpeningCredit
                                && totals.PeriodDebit == totals.PeriodCredit
                                && totals.ClosingDebit == totals.ClosingCredit;

            return new TrialBalanceDto { Lines = lines, Totals = totals };
        }

        private readonly record struct Cell(decimal OpeningNet, decimal PeriodDebit, decimal PeriodCredit)
        {
            public bool IsZero => OpeningNet == 0m && PeriodDebit == 0m && PeriodCredit == 0m;
            public static Cell operator +(Cell a, Cell b) =>
                new(a.OpeningNet + b.OpeningNet, a.PeriodDebit + b.PeriodDebit, a.PeriodCredit + b.PeriodCredit);
        }
    }
}
