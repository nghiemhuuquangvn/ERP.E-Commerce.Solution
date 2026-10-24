using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    /// <summary>
    /// Closes a fiscal period (ROADMAP P1-2): checks the books, writes the period-end transfer (every revenue / expense account to the income summary
    /// account, the summary to retained earnings: VAS 911 -> 421) in the period itself, then locks the period. One unit of work, so the entry and the
    /// lock commit together. With the YearEnd closing policy (P1-2d) only the last period of the fiscal year writes the entry.
    /// </summary>
    public class CloseFiscalPeriodCommandHandler : IRequestHandler<CloseFiscalPeriodCommand, FiscalPeriodDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;

        public CloseFiscalPeriodCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting)
        {
            _uow = uow;
            _posting = posting;
        }

        public async Task<FiscalPeriodDto> Handle(CloseFiscalPeriodCommand request, CancellationToken cancellationToken)
        {
            var period = await _uow.FiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new KeyNotFoundException($"Fiscal period {request.FiscalPeriodId} not found.");
            if (period.Status != (int)FiscalPeriodStatus.Open)
                throw new BusinessRuleException("Accounting", $"Fiscal period {period.PeriodName} is already closed.");

            var periods = await _uow.FiscalPeriods.GetAllAsync(cancellationToken);
            var earlierOpen = periods.Where(p => p.Id != period.Id && p.StartDate < period.StartDate && p.Status == (int)FiscalPeriodStatus.Open)
                .OrderBy(p => p.StartDate).FirstOrDefault();
            if (earlierOpen != null)
                throw new BusinessRuleException("Accounting", $"Close {earlierOpen.PeriodName} first: periods are closed in order.");

            var entries = await _uow.JournalEntries.GetByFiscalPeriodIdAsync(period.Id, cancellationToken);
            var drafts = entries.Count(e => e.Status == (int)JournalStatus.Draft);
            if (drafts > 0)
                throw new BusinessRuleException("Accounting", $"Fiscal period {period.PeriodName} has {drafts} draft journal entr{(drafts == 1 ? "y" : "ies")}; post or delete them first.");

            var balances = await _uow.JournalLines.GetAccountBalancesAsync(null, null, period.Id, cancellationToken);
            if (balances.Sum(b => b.TotalDebit) != balances.Sum(b => b.TotalCredit))
                throw new BusinessRuleException("Accounting", $"The trial balance of {period.PeriodName} does not balance; it cannot be closed.");

            // Monthly (the default): every period writes its closing entry. YearEnd: only the last period of the fiscal year does, and it closes
            // everything not yet closed, so the balance it reads is the cumulative one up to the period end (months closed earlier are already zero).
            var policy = await _uow.ClosingPolicies.GetAsync(cancellationToken);
            var yearEndMode = policy != null && policy.ClosingMode == (int)ClosingMode.YearEnd;
            var closesYear = yearEndMode && ClosingPolicy.EndsFiscalYear(period, policy!.FiscalYearEndMonth);

            if (request.PostClosingEntries && (!yearEndMode || closesYear))
            {
                var closingBalances = closesYear
                    ? await _uow.JournalLines.GetAccountBalancesAsync(null, period.EndDate.Date, null, cancellationToken)
                    : balances;
                var closing = await BuildClosingRequestAsync(period, closingBalances, closesYear, cancellationToken);
                if (closing != null)
                {
                    try
                    {
                        var posted = await _posting.PostAsync(closing, cancellationToken);
                        if (posted == null)
                            throw new BusinessRuleException("Accounting", "Automatic posting is switched off (Accounting:AutoPosting:Enabled), so the closing entry cannot be written.");
                    }
                    catch (PostingConfigurationException ex)
                    {
                        throw new BusinessRuleException("Accounting", $"The closing entry needs the posting keys IncomeSummary and RetainedEarnings: {ex.Message}");
                    }
                }
            }

            period.Close();
            await _uow.FiscalPeriods.UpdateAsync(period, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return OpenFiscalPeriodCommandHandler.MapFiscalPeriodToDto(period);
        }

        /// <summary>Null when no revenue or expense account moved in the period.</summary>
        private async Task<PostingRequest?> BuildClosingRequestAsync(FiscalPeriod period, List<AccountBalanceSummary> balances, bool yearEnd, CancellationToken ct)
        {
            var accounts = await _uow.Accounts.GetAllAsync(ct);
            var parentIds = await AccountRules.ParentIdsAsync(_uow.Accounts, ct);
            var byId = balances.ToDictionary(b => b.AccountId);
            var lines = new List<PostingLine>();
            var total = 0m; // debit minus credit of all closed accounts: positive = net loss

            foreach (var account in accounts.Where(a => a.AccountType is (int)AccountType.Revenue or (int)AccountType.Expense).OrderBy(a => a.AccountNumber))
            {
                if (!byId.TryGetValue(account.Id, out var balance) || parentIds.Contains(account.Id))
                    continue;

                var net = Math.Round(balance.TotalDebit - balance.TotalCredit, 2);
                if (net == 0m)
                    continue;

                total += net;
                var description = yearEnd ? $"Year-end closing {period.PeriodName}" : $"Closing {period.PeriodName}";
                lines.Add(net > 0m
                    ? new PostingLine(null, account.Id, 0m, net, description)
                    : new PostingLine(null, account.Id, -net, 0m, description));
            }

            if (lines.Count == 0)
                return null;

            // Everything that was closed lands on the income summary account: a loss as a debit, a profit as a credit ...
            var summary = Math.Abs(total);
            lines.Add(total > 0m
                ? PostingLine.Dr(PostingKeys.IncomeSummary, summary, $"Result of {period.PeriodName}")
                : PostingLine.Cr(PostingKeys.IncomeSummary, summary, $"Result of {period.PeriodName}"));

            // ... and moves on to retained earnings (skipped when the profile maps both keys to one account, e.g. the simple chart).
            if (total != 0m && !await SameAccountAsync(ct))
            {
                lines.Add(total > 0m
                    ? PostingLine.Cr(PostingKeys.IncomeSummary, summary, "Transfer to retained earnings")
                    : PostingLine.Dr(PostingKeys.IncomeSummary, summary, "Transfer to retained earnings"));
                lines.Add(total > 0m
                    ? PostingLine.Dr(PostingKeys.RetainedEarnings, summary, "Loss carried forward")
                    : PostingLine.Cr(PostingKeys.RetainedEarnings, summary, "Profit carried forward"));
            }

            return new PostingRequest(JournalEntry.PeriodClosingType, period.Id, period.EndDate.Date, yearEnd ? $"Year-end closing {period.PeriodName}" : $"Period-end closing {period.PeriodName}", lines);
        }

        private async Task<bool> SameAccountAsync(CancellationToken ct)
        {
            var mappings = await _uow.PostingMappings.GetAllAsync(ct);
            var summary = mappings.FirstOrDefault(m => m.PostingKey == PostingKeys.IncomeSummary)?.AccountId;
            var retained = mappings.FirstOrDefault(m => m.PostingKey == PostingKeys.RetainedEarnings)?.AccountId;
            // Unmapped keys are mapped by the engine on the first posting; until then the transfer lines are simply written (they net to zero on a shared account).
            return summary != null && retained != null && summary == retained;
        }
    }

    /// <summary>Reopens the latest closed period: its closing entry is voided so the period can be corrected and closed again.</summary>
    public class ReopenFiscalPeriodCommandHandler : IRequestHandler<ReopenFiscalPeriodCommand, FiscalPeriodDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public ReopenFiscalPeriodCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<FiscalPeriodDto> Handle(ReopenFiscalPeriodCommand request, CancellationToken cancellationToken)
        {
            var period = await _uow.FiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new KeyNotFoundException($"Fiscal period {request.FiscalPeriodId} not found.");
            if (period.Status != (int)FiscalPeriodStatus.Closed)
                throw new BusinessRuleException("Accounting", $"Fiscal period {period.PeriodName} is not closed.");
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new BusinessRuleException("Accounting", "A reason is required to reopen a fiscal period.");

            var periods = await _uow.FiscalPeriods.GetAllAsync(cancellationToken);
            var later = periods.Where(p => p.Id != period.Id && p.StartDate > period.StartDate && p.Status == (int)FiscalPeriodStatus.Closed)
                .OrderBy(p => p.StartDate).FirstOrDefault();
            if (later != null)
                throw new BusinessRuleException("Accounting", $"{later.PeriodName} is closed after this period; reopen it first.");

            var closing = await _uow.JournalEntries.GetBySourceKeyAsync(JournalEntry.BuildSourceKey(JournalEntry.PeriodClosingType, period.Id), cancellationToken);
            if (closing != null && closing.Status == (int)JournalStatus.Posted)
            {
                closing.VoidForReopen($"Period {period.PeriodName} reopened: {request.Reason}");
                await _uow.JournalEntries.UpdateAsync(closing, cancellationToken);
            }

            period.Reopen();
            await _uow.FiscalPeriods.UpdateAsync(period, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return OpenFiscalPeriodCommandHandler.MapFiscalPeriodToDto(period);
        }
    }
}
