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
    /// The cash-flow statement by the direct method (ROADMAP P1-2b). Every posted entry of the range that touches a cash account is read line by line:
    /// the cash lines are the movement itself, and each other line says what the money was for — a counter-line of Cr 5111 1000 against Dr 1121 1000 is
    /// 1000 received for sales. Counter-lines are attributed to the account's cash-flow category (operating, investing, financing); entries whose lines
    /// are all cash accounts (transfers between cash and bank) move no money in or out and are left out. Closing entries and voided entries never count.
    /// Because an entry is balanced, the counter-lines add up to the cash movement exactly, so opening cash + net change = closing cash; the report
    /// shows that check (a difference would point to data that bypassed the ledger rules).
    /// </summary>
    public class GetCashFlowQueryHandler : IRequestHandler<GetCashFlowQuery, CashFlowStatementDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;

        public GetCashFlowQueryHandler(IAccountRepository accounts, IJournalLineRepository lines)
        {
            _accounts = accounts;
            _lines = lines;
        }

        public async Task<CashFlowStatementDto> Handle(GetCashFlowQuery request, CancellationToken cancellationToken)
        {
            var from = request.FromDate.Date;
            var to = request.ToDate.Date;
            if (from > to)
                throw new BusinessRuleException("Accounting", "The start date is after the end date.");

            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var byId = accounts.ToDictionary(a => a.Id);
            var cashIds = accounts.Where(a => a.IsCash).Select(a => a.Id).ToHashSet();

            var statement = new CashFlowStatementDto { FromDate = from, ToDate = to, CashAccountCount = cashIds.Count };
            if (cashIds.Count == 0)
            {
                statement.IsReconciled = true;
                return statement; // no account is flagged as cash: nothing to report (the admin screen explains how to flag one)
            }

            // cash before the range and at its end
            var before = await _lines.GetAccountBalancesAsync(null, from.AddDays(-1), null, cancellationToken);
            var untilEnd = await _lines.GetAccountBalancesAsync(null, to, null, cancellationToken);
            decimal Cash(IEnumerable<AccountBalanceSummary> balances) =>
                balances.Where(b => cashIds.Contains(b.AccountId)).Sum(b => b.TotalDebit - b.TotalCredit);
            statement.OpeningCash = Cash(before);
            statement.ClosingCash = Cash(untilEnd);

            var rows = await _lines.GetCashEntryLinesAsync(cashIds, from, to, cancellationToken);
            var inflow = new Dictionary<(CashFlowCategory, Guid), decimal>();
            var outflow = new Dictionary<(CashFlowCategory, Guid), decimal>();

            foreach (var entry in rows.GroupBy(r => r.EntryId))
            {
                var counterLines = entry.Where(r => !cashIds.Contains(r.AccountId)).ToList();
                if (counterLines.Count == 0)
                    continue; // a transfer between cash / bank accounts

                foreach (var line in counterLines)
                {
                    if (!byId.TryGetValue(line.AccountId, out var account))
                        continue;

                    // credit minus debit of the counter-line = the cash it brought in (negative: the cash it took out)
                    var cash = line.Credit - line.Debit;
                    if (cash == 0m)
                        continue;

                    var key = (account.EffectiveCashFlowCategory, account.Id);
                    var bucket = cash > 0m ? inflow : outflow;
                    bucket[key] = bucket.GetValueOrDefault(key) + Math.Abs(cash);
                }
            }

            CashFlowSectionDto Section(CashFlowCategory category)
            {
                var ids = inflow.Keys.Concat(outflow.Keys).Where(k => k.Item1 == category).Select(k => k.Item2).Distinct();
                var lines = ids.Select(id =>
                {
                    var a = byId[id];
                    var i = inflow.GetValueOrDefault((category, id));
                    var o = outflow.GetValueOrDefault((category, id));
                    return new CashFlowLineDto { AccountId = a.Id, AccountNumber = a.AccountNumber, AccountName = a.Name, Inflow = i, Outflow = o, Net = i - o };
                }).OrderBy(l => l.AccountNumber).ToList();

                return new CashFlowSectionDto
                {
                    Category = category.ToString(),
                    Lines = lines,
                    TotalInflow = lines.Sum(l => l.Inflow),
                    TotalOutflow = lines.Sum(l => l.Outflow),
                    Net = lines.Sum(l => l.Net),
                };
            }

            statement.Operating = Section(CashFlowCategory.Operating);
            statement.Investing = Section(CashFlowCategory.Investing);
            statement.Financing = Section(CashFlowCategory.Financing);
            statement.NetChange = statement.Operating.Net + statement.Investing.Net + statement.Financing.Net;
            statement.Difference = statement.OpeningCash + statement.NetChange - statement.ClosingCash;
            statement.IsReconciled = statement.Difference == 0m;
            return statement;
        }
    }
}
