using ERP.ACC.Domain.Entities;

namespace ERP.ACC.Application.Reports
{
    /// <summary>The cash a journal entry moved through one counter-line: positive = received, negative = paid.</summary>
    public sealed record CashMovement(Account Account, decimal Cash);

    /// <summary>One column of the statement: the movements of a range and the cash at its start and end.</summary>
    public sealed record CashFlowColumn(IReadOnlyList<CashMovement> Movements, decimal OpeningCash, decimal ClosingCash);

    /// <summary>Evaluates the <see cref="CashFlowLayout"/> (B03-DN) over one or two ranges.</summary>
    public static class CashFlowStatementEngine
    {
        public static FinancialStatementDto Evaluate(CashFlowLayout layout, CashFlowColumn current, CashFlowColumn? prior)
        {
            var now = Compute(layout, current);
            var before = prior != null ? Compute(layout, prior) : null;

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
                    Amount = now.Amounts[r.Code],
                    PriorAmount = before?.Amounts[r.Code],
                }).ToList(),
            };

            var unmapped = new Dictionary<string, UnmappedAccountDto>();
            foreach (var column in before == null ? new[] { now } : new[] { now, before })
                foreach (var m in column.Unmapped)
                {
                    unmapped.TryGetValue(m.Account.AccountNumber, out var known);
                    unmapped[m.Account.AccountNumber] = new UnmappedAccountDto
                    {
                        AccountNumber = m.Account.AccountNumber,
                        AccountName = m.Account.Name,
                        Balance = (known?.Balance ?? 0m) + m.Cash,
                    };
                }
            statement.Unmapped = unmapped.Values.OrderBy(u => u.AccountNumber).ToList();

            // rule: the closing cash row must equal the cash on the books at the end of the range, in every column
            statement.IsBalanced = now.Amounts["70"] == current.ClosingCash && (before == null || before.Amounts["70"] == prior!.ClosingCash);
            if (!statement.IsBalanced.Value)
                statement.Notes.Add("Closing cash (70) differs from the cash on the books: see the unmapped movements below.");
            if (statement.Unmapped.Count > 0)
                statement.Notes.Add("Some cash movements have a counter-account that no row takes (investing / financing accounts outside the layout); map them or extend the layout.");

            return statement;
        }

        private sealed record Computed(Dictionary<string, decimal> Amounts, List<CashMovement> Unmapped);

        private static Computed Compute(CashFlowLayout layout, CashFlowColumn column)
        {
            var sums = layout.Rows.Where(r => r.Formula.Count == 0).ToDictionary(r => r.Code, _ => 0m);
            var unmapped = new List<CashMovement>();

            foreach (var movement in column.Movements.Where(m => m.Cash != 0m))
            {
                var inflow = movement.Cash > 0m;
                var row = layout.Rows.FirstOrDefault(r => r.Terms.Any(t =>
                              t.Inflow == inflow && movement.Account.AccountNumber.StartsWith(t.Prefix, StringComparison.Ordinal)))
                          ?? layout.Rows.FirstOrDefault(r => r.FallbackCategory.HasValue && r.FallbackInflow == inflow
                                                              && r.FallbackCategory.Value == movement.Account.EffectiveCashFlowCategory);
                if (row == null)
                    unmapped.Add(movement);
                else
                    sums[row.Code] += movement.Cash;
            }

            var byCode = layout.Rows.ToDictionary(r => r.Code);
            var memo = new Dictionary<string, decimal>();

            decimal Value(string code)
            {
                if (memo.TryGetValue(code, out var known))
                    return known;
                var row = byCode[code];
                var amount = row.Special switch
                {
                    "OpeningCash" => column.OpeningCash,
                    "FxEffect" => 0m,
                    _ => row.Formula.Count > 0 ? row.Formula.Sum(p => p.Sign * Value(p.Code)) : sums[code],
                };
                memo[code] = amount;
                return amount;
            }

            foreach (var row in layout.Rows)
                Value(row.Code);
            return new Computed(memo, unmapped);
        }
    }
}
