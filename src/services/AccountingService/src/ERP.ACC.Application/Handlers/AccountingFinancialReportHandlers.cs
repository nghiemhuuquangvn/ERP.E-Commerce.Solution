using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Enums;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class GetTrialBalanceQueryHandler : IRequestHandler<GetTrialBalanceQuery, List<TrialBalanceLineDto>>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _journalLines;
        private readonly IFiscalPeriodRepository _fiscalPeriods;

        public GetTrialBalanceQueryHandler(
            IAccountRepository accounts,
            IJournalLineRepository journalLines,
            IFiscalPeriodRepository fiscalPeriods)
        {
            _accounts = accounts;
            _journalLines = journalLines;
            _fiscalPeriods = fiscalPeriods;
        }

        public async Task<List<TrialBalanceLineDto>> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken) =>
            (await TrialBalanceLoader.LoadAsync(_accounts, _journalLines, _fiscalPeriods,
                request.FiscalPeriodId, request.FromDate, request.ToDate, request.RollUp, request.HideZeroBalance, cancellationToken)).Lines;
    }

    public class GetTrialBalanceFullQueryHandler : IRequestHandler<GetTrialBalanceFullQuery, TrialBalanceDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _journalLines;
        private readonly IFiscalPeriodRepository _fiscalPeriods;

        public GetTrialBalanceFullQueryHandler(
            IAccountRepository accounts, IJournalLineRepository journalLines, IFiscalPeriodRepository fiscalPeriods)
        {
            _accounts = accounts;
            _journalLines = journalLines;
            _fiscalPeriods = fiscalPeriods;
        }

        public Task<TrialBalanceDto> Handle(GetTrialBalanceFullQuery request, CancellationToken cancellationToken) =>
            TrialBalanceLoader.LoadAsync(_accounts, _journalLines, _fiscalPeriods,
                request.FiscalPeriodId, request.FromDate, request.ToDate, request.RollUp, request.HideZeroBalance, cancellationToken);
    }

    internal static class TrialBalanceLoader
    {
        /// <summary>Opening = everything before the range, period = the range, closing = both; a fiscal period stands for its dates.</summary>
        public static async Task<TrialBalanceDto> LoadAsync(
            IAccountRepository accountsRepo, IJournalLineRepository lines, IFiscalPeriodRepository periods,
            Guid? fiscalPeriodId, DateTime? fromDate, DateTime? toDate, bool rollUp, bool hideZero, CancellationToken ct)
        {
            if (fiscalPeriodId.HasValue)
            {
                var period = await periods.GetByIdAsync(fiscalPeriodId.Value, ct);
                if (period != null)
                {
                    fromDate = period.StartDate;
                    toDate = period.EndDate;
                }
            }

            var accounts = await accountsRepo.GetAllAsync(ct);
            var movement = await lines.GetAccountBalancesAsync(fromDate, toDate, null, ct);
            var before = fromDate.HasValue
                ? await lines.GetAccountBalancesAsync(null, fromDate.Value.Date.AddDays(-1), null, ct)
                : new List<AccountBalanceSummary>();

            return TrialBalanceBuilder.Build(accounts, before.ToDictionary(b => b.AccountId), movement.ToDictionary(b => b.AccountId), rollUp, hideZero);
        }
    }

    public class GetProfitAndLossQueryHandler : IRequestHandler<GetProfitAndLossQuery, ProfitAndLossDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _journalLines;

        public GetProfitAndLossQueryHandler(IAccountRepository accounts, IJournalLineRepository journalLines)
        {
            _accounts = accounts;
            _journalLines = journalLines;
        }

        public async Task<ProfitAndLossDto> Handle(GetProfitAndLossQuery request, CancellationToken cancellationToken)
        {
            var accounts = await _accounts.GetAllAsync(cancellationToken);
            // The period-end transfer zeroes every revenue and expense account; the profit and loss statement shows the period's activity, so it leaves it out.
            var balances = await _journalLines.GetActivityBalancesAsync(
                request.FromDate, request.ToDate, Domain.Entities.JournalEntry.PeriodClosingType, cancellationToken);
            var balanceMap = balances.ToDictionary(b => b.AccountId);

            var revenueLines = accounts
                .Where(a => a.AccountType == (int)AccountType.Revenue)
                .Select(a =>
                {
                    balanceMap.TryGetValue(a.Id, out var bal);
                    var net = (bal?.TotalCredit ?? 0m) - (bal?.TotalDebit ?? 0m);
                    return new FinancialAccountLineDto
                    {
                        AccountId = a.Id,
                        AccountNumber = a.AccountNumber,
                        AccountName = a.Name,
                        Amount = net
                    };
                })
                .Where(l => l.Amount != 0)
                .OrderBy(l => l.AccountNumber)
                .ToList();

            var expenseLines = accounts
                .Where(a => a.AccountType == (int)AccountType.Expense)
                .Select(a =>
                {
                    balanceMap.TryGetValue(a.Id, out var bal);
                    var net = (bal?.TotalDebit ?? 0m) - (bal?.TotalCredit ?? 0m);
                    return new FinancialAccountLineDto
                    {
                        AccountId = a.Id,
                        AccountNumber = a.AccountNumber,
                        AccountName = a.Name,
                        Amount = net
                    };
                })
                .Where(l => l.Amount != 0)
                .OrderBy(l => l.AccountNumber)
                .ToList();

            var totalRevenue = revenueLines.Sum(l => l.Amount);
            var totalExpenses = expenseLines.Sum(l => l.Amount);

            return new ProfitAndLossDto
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                TotalRevenue = totalRevenue,
                TotalExpenses = totalExpenses,
                NetIncome = totalRevenue - totalExpenses,
                RevenueLines = revenueLines,
                ExpenseLines = expenseLines
            };
        }
    }

    public class GetBalanceSheetQueryHandler : IRequestHandler<GetBalanceSheetQuery, BalanceSheetDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _journalLines;

        public GetBalanceSheetQueryHandler(IAccountRepository accounts, IJournalLineRepository journalLines)
        {
            _accounts = accounts;
            _journalLines = journalLines;
        }

        public async Task<BalanceSheetDto> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
        {
            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var balances = await _journalLines.GetAccountBalancesAsync(null, request.AsOfDate, null, cancellationToken);
            var balanceMap = balances.ToDictionary(b => b.AccountId);

            List<FinancialAccountLineDto> GetLines(AccountType type, bool debitNormal) =>
                accounts
                    .Where(a => a.AccountType == (int)type)
                    .Select(a =>
                    {
                        balanceMap.TryGetValue(a.Id, out var bal);
                        var net = debitNormal
                            ? (bal?.TotalDebit ?? 0m) - (bal?.TotalCredit ?? 0m)
                            : (bal?.TotalCredit ?? 0m) - (bal?.TotalDebit ?? 0m);
                        return new FinancialAccountLineDto
                        {
                            AccountId = a.Id,
                            AccountNumber = a.AccountNumber,
                            AccountName = a.Name,
                            Amount = net
                        };
                    })
                    .Where(l => l.Amount != 0)
                    .OrderBy(l => l.AccountNumber)
                    .ToList();

            var assetLines = GetLines(AccountType.Asset, debitNormal: true);
            var liabilityLines = GetLines(AccountType.Liability, debitNormal: false);
            var equityLines = GetLines(AccountType.Equity, debitNormal: false);

            // Revenue and expense accounts that the period-end transfer has not closed yet are still open: their result is part of equity, so
            // assets = liabilities + equity holds before closing too (after closing they net to zero and this line disappears).
            var unclosedResult = accounts
                .Where(a => a.AccountType is (int)AccountType.Revenue or (int)AccountType.Expense)
                .Sum(a => balanceMap.TryGetValue(a.Id, out var bal) ? bal.TotalCredit - bal.TotalDebit : 0m);
            if (unclosedResult != 0m)
                equityLines.Add(new FinancialAccountLineDto { AccountId = Guid.Empty, AccountNumber = "", AccountName = "Current result (not yet closed)", Amount = unclosedResult });

            return new BalanceSheetDto
            {
                AsOfDate = request.AsOfDate,
                TotalAssets = assetLines.Sum(l => l.Amount),
                TotalLiabilities = liabilityLines.Sum(l => l.Amount),
                TotalEquity = equityLines.Sum(l => l.Amount),
                AssetLines = assetLines,
                LiabilityLines = liabilityLines,
                EquityLines = equityLines
            };
        }
    }
}
