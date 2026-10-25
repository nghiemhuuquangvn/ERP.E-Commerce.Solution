using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Reports
{
    public class StatementMappingValueDto
    {
        public string AccountPrefix { get; set; } = string.Empty;

        /// <summary>"Net", "DebitOnly" or "CreditOnly".</summary>
        public string Side { get; set; } = nameof(BalanceSide.Net);
    }

    public class StatementMappingRowDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Indent { get; set; }

        /// <summary>Rows that add up accounts; totals, formulas and computed rows take no account.</summary>
        public bool IsMappable { get; set; }

        /// <summary>The prefixes the standard layout of the chart already places in the row.</summary>
        public List<string> StandardPrefixes { get; set; } = new();
        public List<StatementMappingValueDto> Mappings { get; set; } = new();
    }

    public class StatementUnmappedAccountDto
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
    }

    public class StatementMappingDto
    {
        public string StatementId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<StatementMappingRowDto> Rows { get; set; } = new();

        /// <summary>Accounts with postings that no row of the statement takes (neither the standard layout nor the tenant's mapping).</summary>
        public List<StatementUnmappedAccountDto> Unmapped { get; set; } = new();
    }

    public class GetStatementMappingQuery : IRequest<StatementMappingDto>
    {
        public string StatementId { get; set; } = StatementLayouts.BalanceSheet;
    }

    public class StatementMappingInput
    {
        public string RowCode { get; set; } = string.Empty;
        public string AccountPrefix { get; set; } = string.Empty;
        public string Side { get; set; } = nameof(BalanceSide.Net);
    }

    /// <summary>Replaces the tenant's mapping of one statement: rows kept as they are, new ones added, the others removed.</summary>
    public class UpdateStatementMappingCommand : IRequest<StatementMappingDto>
    {
        public string StatementId { get; set; } = StatementLayouts.BalanceSheet;
        public List<StatementMappingInput> Mappings { get; set; } = new();
    }

    internal static class StatementMappingRules
    {
        public static bool IsMappable(StatementRow row) => row.Formula.Count == 0 && row.Special == null;

        public static StatementLayout RequireLayout(string statementId, IEnumerable<Account> accounts) =>
            StatementLayouts.For(statementId ?? string.Empty, accounts)
            ?? throw new BusinessRuleException("Accounting", $"Unknown statement '{statementId}'. Use {StatementLayouts.BalanceSheet} or {StatementLayouts.IncomeStatement}.");

        public static BalanceSide ParseSide(string? side) =>
            string.IsNullOrWhiteSpace(side) ? BalanceSide.Net
            : Enum.TryParse<BalanceSide>(side, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed
            : throw new BusinessRuleException("Accounting", $"Unknown side '{side}'. Use Net, DebitOnly or CreditOnly.");

        public static async Task<StatementMappingDto> LoadAsync(IAccountingUnitOfWork uow, string statementId, CancellationToken ct)
        {
            var accounts = await uow.Accounts.GetAllAsync(ct);
            var standard = RequireLayout(statementId, accounts);
            var mappings = (await uow.StatementMappings.GetAllAsync(ct)).Where(m => m.StatementId == standard.Id).ToList();
            var effective = StatementLayouts.WithMappings(standard, mappings);

            var dto = new StatementMappingDto { StatementId = standard.Id, Title = standard.Title };
            foreach (var row in standard.Rows)
            {
                dto.Rows.Add(new StatementMappingRowDto
                {
                    Code = row.Code,
                    Name = row.Name,
                    Indent = row.Indent,
                    IsMappable = IsMappable(row),
                    StandardPrefixes = row.Terms.Select(t => t.Prefix).ToList(),
                    Mappings = mappings.Where(m => m.RowCode == row.Code).Select(m => new StatementMappingValueDto
                    {
                        AccountPrefix = m.AccountPrefix, Side = ((BalanceSide)m.Side).ToString(),
                    }).ToList(),
                });
            }

            // accounts the statement is about that have postings and that no row takes
            var types = effective.AccountTypes.Select(t => (int)t).ToHashSet();
            var prefixes = effective.Rows.SelectMany(r => r.Terms).Select(t => t.Prefix).ToList();
            // the balance sheet shows balances (an account that nets to zero is not a problem); the income statement shows the activity of the year, closing entries left out
            var moved = standard.Id == StatementLayouts.BalanceSheet
                ? (await uow.JournalLines.GetAccountBalancesAsync(null, null, null, ct)).Where(b => b.TotalDebit != b.TotalCredit)
                : (await uow.JournalLines.GetActivityBalancesAsync(null, null, JournalEntry.PeriodClosingType, ct)).Where(b => b.TotalDebit != 0m || b.TotalCredit != 0m);
            var movedIds = moved.Select(b => b.AccountId).ToHashSet();
            dto.Unmapped = accounts
                .Where(a => types.Contains(a.AccountType) && movedIds.Contains(a.Id))
                .Where(a => !prefixes.Any(p => a.AccountNumber.StartsWith(p, StringComparison.Ordinal)))
                .OrderBy(a => a.AccountNumber)
                .Select(a => new StatementUnmappedAccountDto { AccountNumber = a.AccountNumber, AccountName = a.Name })
                .ToList();
            return dto;
        }
    }

    public class GetStatementMappingQueryHandler : IRequestHandler<GetStatementMappingQuery, StatementMappingDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public GetStatementMappingQueryHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public Task<StatementMappingDto> Handle(GetStatementMappingQuery request, CancellationToken ct) =>
            StatementMappingRules.LoadAsync(_uow, request.StatementId, ct);
    }

    public class UpdateStatementMappingCommandHandler : IRequestHandler<UpdateStatementMappingCommand, StatementMappingDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public UpdateStatementMappingCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<StatementMappingDto> Handle(UpdateStatementMappingCommand request, CancellationToken ct)
        {
            var accounts = await _uow.Accounts.GetAllAsync(ct);
            var standard = StatementMappingRules.RequireLayout(request.StatementId, accounts);
            var inputs = request.Mappings ?? new List<StatementMappingInput>();

            var seen = new HashSet<string>();
            foreach (var input in inputs)
            {
                var prefix = (input.AccountPrefix ?? string.Empty).Trim();
                var row = standard.Rows.FirstOrDefault(r => r.Code == input.RowCode)
                    ?? throw new BusinessRuleException("Accounting", $"Row {input.RowCode} does not exist on {standard.Id}.");
                if (!StatementMappingRules.IsMappable(row))
                    throw new BusinessRuleException("Accounting", $"Row {row.Code} ({row.Name}) is a total or a computed row: it takes no account.");
                if (prefix.Length == 0 || prefix.Length > 30)
                    throw new BusinessRuleException("Accounting", "An account number prefix is required (up to 30 characters).");
                StatementMappingRules.ParseSide(input.Side);
                if (!accounts.Any(a => a.AccountNumber.StartsWith(prefix, StringComparison.Ordinal)))
                    throw new BusinessRuleException("Accounting", $"No account number starts with '{prefix}'.");
                if (!seen.Add(prefix))
                    throw new BusinessRuleException("Accounting", $"The prefix '{prefix}' is mapped twice: an account would be counted in two rows.");
                var placed = standard.Rows.FirstOrDefault(r => r.Terms.Any(t => t.Prefix == prefix));
                if (placed != null)
                    throw new BusinessRuleException("Accounting", $"The prefix '{prefix}' is already placed by the standard layout in row {placed.Code}.");
            }

            var current = (await _uow.StatementMappings.GetAllAsync(ct)).Where(m => m.StatementId == standard.Id).ToList();
            foreach (var existing in current)
            {
                var kept = inputs.FirstOrDefault(i => i.RowCode == existing.RowCode && i.AccountPrefix.Trim() == existing.AccountPrefix);
                if (kept == null)
                    _uow.StatementMappings.Remove(existing);
                else
                    existing.SetSide((int)StatementMappingRules.ParseSide(kept.Side));
            }
            foreach (var input in inputs.Where(i => !current.Any(c => c.RowCode == i.RowCode && c.AccountPrefix == i.AccountPrefix.Trim())))
                await _uow.StatementMappings.AddAsync(StatementAccountMapping.Create(standard.Id, input.RowCode, input.AccountPrefix, (int)StatementMappingRules.ParseSide(input.Side)), ct);

            await _uow.SaveChangesAsync(ct);
            return await StatementMappingRules.LoadAsync(_uow, standard.Id, ct);
        }
    }
}
