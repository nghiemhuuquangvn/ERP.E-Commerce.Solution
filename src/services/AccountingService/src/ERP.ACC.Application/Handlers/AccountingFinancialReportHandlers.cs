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

        public async Task<List<TrialBalanceLineDto>> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken)
        {
            DateTime? fromDate = request.FromDate;
            DateTime? toDate = request.ToDate;

            if (request.FiscalPeriodId.HasValue)
            {
                var period = await _fiscalPeriods.GetByIdAsync(request.FiscalPeriodId.Value, cancellationToken);
                if (period != null)
                {
                    fromDate = period.StartDate;
                    toDate = period.EndDate;
                }
            }

            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var balances = await _journalLines.GetAccountBalancesAsync(fromDate, toDate, null, cancellationToken);
            var balanceMap = balances.ToDictionary(b => b.AccountId);

            return accounts
                .Select(a =>
                {
                    balanceMap.TryGetValue(a.Id, out var bal);
                    var debit = bal?.TotalDebit ?? 0m;
                    var credit = bal?.TotalCredit ?? 0m;
                    var isDebitNormal = a.NormalBalance == (int)NormalBalance.Debit;
                    var balance = isDebitNormal ? debit - credit : credit - debit;

                    return new TrialBalanceLineDto
                    {
                        AccountId = a.Id,
                        AccountNumber = a.AccountNumber,
                        AccountName = a.Name,
                        AccountType = ((AccountType)a.AccountType).ToString(),
                        NormalBalance = ((NormalBalance)a.NormalBalance).ToString(),
                        TotalDebit = debit,
                        TotalCredit = credit,
                        Balance = balance
                    };
                })
                .Where(l => !request.HideZeroBalance || l.TotalDebit != 0 || l.TotalCredit != 0)
                .OrderBy(l => l.AccountNumber)
                .ToList();
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
            var balances = await _journalLines.GetAccountBalancesAsync(
                request.FromDate, request.ToDate, null, cancellationToken);
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
