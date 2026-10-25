using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.ACC.Application.Reports
{
    /// <summary>One row a tenant can map accounts to: its code, name, whether it takes accounts, and the prefixes the standard layout already places there.</summary>
    public sealed record MappingRow(string Code, string Name, int Indent, bool IsMappable, IReadOnlyList<string> StandardPrefixes);

    /// <summary>The statements the account mapping works on, and their rows (ROADMAP P1-2j for B01 / B02, P1-2k for B03 and the notes).</summary>
    public sealed record MappingStatement(string Id, string Title, bool UsesSide, IReadOnlyList<MappingRow> Rows);

    public static class StatementMappingCatalog
    {
        public static readonly IReadOnlyList<string> Ids = new[]
        {
            StatementLayouts.BalanceSheet, StatementLayouts.IncomeStatement, CashFlowLayouts.CashFlow, IndirectCashFlowLayouts.Id, NoteMappingRows.StatementId,
        };

        public static MappingStatement Get(string? statementId, IReadOnlyCollection<Account> accounts)
        {
            var chart = ChartTemplates.Detect(accounts);
            switch (statementId)
            {
                case StatementLayouts.BalanceSheet:
                case StatementLayouts.IncomeStatement:
                    var layout = StatementLayouts.For(statementId, accounts)!;
                    return new MappingStatement(layout.Id, layout.Title, true, layout.Rows.Select(r => new MappingRow(
                        r.Code, r.Name, r.Indent, r.Formula.Count == 0 && r.Special == null, r.Terms.Select(t => t.Prefix).ToList())).ToList());

                case CashFlowLayouts.CashFlow:
                    var direct = CashFlowLayouts.For(chart);
                    return new MappingStatement(direct.Id, direct.Title, false, direct.Rows.Select(r => new MappingRow(
                        r.Code, r.Name, r.Indent, r.Formula.Count == 0 && r.Special == null && r.Terms.Count > 0, r.Terms.Select(t => t.Prefix).ToList())).ToList());

                case IndirectCashFlowLayouts.Id:
                    var indirect = IndirectCashFlowLayouts.For(chart);
                    return new MappingStatement(indirect.Id, indirect.Title, false, indirect.Rows.Select(r => new MappingRow(
                        r.Code, r.Name, r.Indent, IndirectCashFlowLayouts.TakesPrefixes(r), r.Prefixes.ToList())).ToList());

                case NoteMappingRows.StatementId:
                    return new MappingStatement(NoteMappingRows.StatementId, "B09-DN — Notes", false, NoteMappingRows.Rows(NotePrefixes.For(chart)));

                default:
                    throw new BusinessRuleException("Accounting", $"Unknown statement '{statementId}'. Use {string.Join(", ", Ids)}.");
            }
        }
    }

    /// <summary>The tables of the notes that sum accounts, as mapping rows: the row code is the section number of the table.</summary>
    public static class NoteMappingRows
    {
        public const string StatementId = "B09-DN";

        private static readonly (string Code, Func<NoteStrings, string> Title, Func<NotePrefixes, string[]> Prefixes, Func<NotePrefixes, string[], NotePrefixes> With)[] Tables =
        {
            ("V.1", s => s.Cash, p => p.Cash, (p, v) => p with { Cash = v }),
            ("V.2", s => s.Receivables, p => p.Receivable, (p, v) => p with { Receivable = v }),
            ("V.3", s => s.Inventories, p => p.Inventory, (p, v) => p with { Inventory = v }),
            ("V.4", s => s.FixedAssets, p => p.FixedCost, (p, v) => p with { FixedCost = v }),
            ("V.5", s => s.ConstructionInProgress, p => p.ConstructionInProgress, (p, v) => p with { ConstructionInProgress = v }),
            ("V.6", s => s.Prepaid, p => p.Prepaid, (p, v) => p with { Prepaid = v }),
            ("V.7", s => s.Payables, p => p.Payable, (p, v) => p with { Payable = v }),
            ("V.8", s => s.Taxes, p => p.Tax, (p, v) => p with { Tax = v }),
            ("V.9", s => s.Accrued, p => p.Accrued, (p, v) => p with { Accrued = v }),
            ("V.11", s => s.Equity, p => p.Equity, (p, v) => p with { Equity = v }),
            ("VI.1", s => s.Revenue, p => p.Revenue, (p, v) => p with { Revenue = v }),
            ("VI.2", s => s.Expenses, p => p.Expenses, (p, v) => p with { Expenses = v }),
        };

        public static IReadOnlyList<MappingRow> Rows(NotePrefixes prefixes) =>
            Tables.Select(t => new MappingRow(t.Code, t.Title(NoteStrings.Vi), 0, true, t.Prefixes(prefixes))).ToList();

        /// <summary>The prefix sets of the notes with the tenant's own accounts added to the tables they were mapped to.</summary>
        public static NotePrefixes WithMappings(NotePrefixes prefixes, IEnumerable<StatementAccountMapping>? mappings)
        {
            foreach (var m in (mappings ?? Enumerable.Empty<StatementAccountMapping>()).Where(m => m.StatementId == StatementId))
            {
                var table = Tables.FirstOrDefault(t => t.Code == m.RowCode);
                if (table.Code == null)
                    continue;
                var current = table.Prefixes(prefixes);
                if (!current.Contains(m.AccountPrefix))
                    prefixes = table.With(prefixes, current.Append(m.AccountPrefix).ToArray());
            }
            return prefixes;
        }
    }
}
