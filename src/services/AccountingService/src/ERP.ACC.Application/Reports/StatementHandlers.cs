using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Reports
{
    /// <summary>The balance sheet B01-DN as of a date, optionally next to a comparison date.</summary>
    public class GetBalanceSheetStatementQuery : IRequest<FinancialStatementDto>
    {
        public DateTime AsOfDate { get; set; }
        public DateTime? CompareAsOfDate { get; set; }
    }

    /// <summary>The income statement B02-DN for a range, optionally next to a comparison range. Period-closing entries are left out.</summary>
    public class GetIncomeStatementQuery : IRequest<FinancialStatementDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime? CompareFromDate { get; set; }
        public DateTime? CompareToDate { get; set; }
    }

    public class GetBalanceSheetStatementQueryHandler : IRequestHandler<GetBalanceSheetStatementQuery, FinancialStatementDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;

        public GetBalanceSheetStatementQueryHandler(IAccountRepository accounts, IJournalLineRepository lines)
        {
            _accounts = accounts;
            _lines = lines;
        }

        public async Task<FinancialStatementDto> Handle(GetBalanceSheetStatementQuery request, CancellationToken cancellationToken)
        {
            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var current = (await _lines.GetAccountBalancesAsync(null, request.AsOfDate.Date, null, cancellationToken)).ToDictionary(b => b.AccountId);
            Dictionary<Guid, AccountBalanceSummary>? prior = null;
            if (request.CompareAsOfDate.HasValue)
                prior = (await _lines.GetAccountBalancesAsync(null, request.CompareAsOfDate.Value.Date, null, cancellationToken)).ToDictionary(b => b.AccountId);

            var statement = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.BalanceSheet)!, accounts, current, prior);
            statement.ToDate = request.AsOfDate.Date;
            statement.CompareToDate = request.CompareAsOfDate?.Date;
            return statement;
        }
    }

    public class GetIncomeStatementQueryHandler : IRequestHandler<GetIncomeStatementQuery, FinancialStatementDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;

        public GetIncomeStatementQueryHandler(IAccountRepository accounts, IJournalLineRepository lines)
        {
            _accounts = accounts;
            _lines = lines;
        }

        public async Task<FinancialStatementDto> Handle(GetIncomeStatementQuery request, CancellationToken cancellationToken)
        {
            if (request.FromDate.Date > request.ToDate.Date)
                throw new BusinessRuleException("Accounting", "The start date is after the end date.");
            if (request.CompareFromDate.HasValue != request.CompareToDate.HasValue)
                throw new BusinessRuleException("Accounting", "A comparison needs both its start and its end date.");
            if (request.CompareFromDate.HasValue && request.CompareFromDate.Value.Date > request.CompareToDate!.Value.Date)
                throw new BusinessRuleException("Accounting", "The comparison start date is after its end date.");

            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var current = (await _lines.GetActivityBalancesAsync(request.FromDate, request.ToDate, JournalEntry.PeriodClosingType, cancellationToken))
                .ToDictionary(b => b.AccountId);
            Dictionary<Guid, AccountBalanceSummary>? prior = null;
            if (request.CompareFromDate.HasValue)
                prior = (await _lines.GetActivityBalancesAsync(request.CompareFromDate, request.CompareToDate, JournalEntry.PeriodClosingType, cancellationToken))
                    .ToDictionary(b => b.AccountId);

            var statement = StatementEngine.Evaluate(StatementLayouts.Find(StatementLayouts.IncomeStatement)!, accounts, current, prior);
            statement.FromDate = request.FromDate.Date;
            statement.ToDate = request.ToDate.Date;
            statement.CompareFromDate = request.CompareFromDate?.Date;
            statement.CompareToDate = request.CompareToDate?.Date;
            return statement;
        }
    }
}
