using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class OffBalanceEntryDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }

        /// <summary>"In" or "Out".</summary>
        public string Direction { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }

        /// <summary>"Posted" or "Voided".</summary>
        public string Status { get; set; } = string.Empty;
        public string? VoidReason { get; set; }
    }

    public class OffBalanceLineDto
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Opening { get; set; }
        public decimal In { get; set; }
        public decimal Out { get; set; }
        public decimal Closing { get; set; }

        /// <summary>More went out than came in: the register is wrong (or an In entry is missing).</summary>
        public bool IsNegative { get; set; }
    }

    public class OffBalanceReportDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<OffBalanceLineDto> Lines { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }

    public class PostOffBalanceEntryCommand : IRequest<OffBalanceEntryDto>
    {
        public Guid AccountId { get; set; }

        /// <summary>Defaults to today.</summary>
        public DateTime? EntryDate { get; set; }

        /// <summary>"In" or "Out".</summary>
        public string Direction { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
    }

    public class VoidOffBalanceEntryCommand : IRequest<OffBalanceEntryDto>
    {
        public Guid EntryId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ListOffBalanceEntriesQuery : IRequest<List<OffBalanceEntryDto>>
    {
        public Guid? AccountId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class GetOffBalanceReportQuery : IRequest<OffBalanceReportDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    internal static class OffBalanceMapper
    {
        public static OffBalanceEntryDto Map(OffBalanceEntry e, Account? account) => new()
        {
            Id = e.Id,
            AccountId = e.AccountId,
            AccountNumber = account?.AccountNumber ?? string.Empty,
            AccountName = account?.Name ?? string.Empty,
            EntryDate = e.EntryDate,
            Direction = ((OffBalanceDirection)e.Direction).ToString(),
            Amount = e.Amount,
            Description = e.Description,
            Reference = e.Reference,
            Status = ((OffBalanceStatus)e.Status).ToString(),
            VoidReason = e.VoidReason,
        };

        /// <summary>A period that exists for the date and is closed locks the register like it locks the journal.</summary>
        public static async Task EnsureOpenAsync(IFiscalPeriodRepository periods, DateTime date, CancellationToken ct)
        {
            var period = await periods.GetContainingAsync(date, ct);
            if (period != null && period.Status == (int)FiscalPeriodStatus.Closed)
                throw new BusinessRuleException("Accounting", $"Fiscal period {period.PeriodName} is closed: the off-balance register of that date is locked.");
        }
    }

    public class PostOffBalanceEntryCommandHandler : IRequestHandler<PostOffBalanceEntryCommand, OffBalanceEntryDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public PostOffBalanceEntryCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<OffBalanceEntryDto> Handle(PostOffBalanceEntryCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<OffBalanceDirection>(request.Direction, ignoreCase: true, out var direction) || !Enum.IsDefined(typeof(OffBalanceDirection), direction))
                throw new BusinessRuleException("Accounting", "The direction must be In or Out.");
            if (request.Amount <= 0m)
                throw new BusinessRuleException("Accounting", "The amount must be greater than 0.");

            var account = await _uow.Accounts.GetByIdAsync(request.AccountId, cancellationToken)
                ?? throw new BusinessRuleException("Accounting", $"Account {request.AccountId} does not exist.");
            if (account.AccountType != (int)AccountType.OffBalance)
                throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} is not an off-balance account (001 – 008): the register takes off-balance accounts only; use a journal entry for the others.");
            if (!account.IsActive)
                throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} is inactive.");

            var date = (request.EntryDate ?? DateTime.UtcNow).Date;
            await OffBalanceMapper.EnsureOpenAsync(_uow.FiscalPeriods, date, cancellationToken);

            var entry = OffBalanceEntry.Create(account.Id, date, direction, request.Amount, request.Description, request.Reference);
            await _uow.OffBalanceEntries.AddAsync(entry, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return OffBalanceMapper.Map(entry, account);
        }
    }

    public class VoidOffBalanceEntryCommandHandler : IRequestHandler<VoidOffBalanceEntryCommand, OffBalanceEntryDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public VoidOffBalanceEntryCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<OffBalanceEntryDto> Handle(VoidOffBalanceEntryCommand request, CancellationToken cancellationToken)
        {
            var entry = await _uow.OffBalanceEntries.GetByIdAsync(request.EntryId, cancellationToken)
                ?? throw new KeyNotFoundException($"Off-balance entry {request.EntryId} not found.");
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new BusinessRuleException("Accounting", "A reason is required to void an off-balance entry.");
            if (entry.Status != (int)OffBalanceStatus.Posted)
                throw new BusinessRuleException("Accounting", "Only a posted off-balance entry can be voided.");

            await OffBalanceMapper.EnsureOpenAsync(_uow.FiscalPeriods, entry.EntryDate, cancellationToken);

            entry.Void(request.Reason);
            await _uow.SaveChangesAsync(cancellationToken);
            return OffBalanceMapper.Map(entry, await _uow.Accounts.GetByIdAsync(entry.AccountId, cancellationToken));
        }
    }

    public class ListOffBalanceEntriesQueryHandler : IRequestHandler<ListOffBalanceEntriesQuery, List<OffBalanceEntryDto>>
    {
        private readonly IOffBalanceRepository _entries;
        private readonly IAccountRepository _accounts;

        public ListOffBalanceEntriesQueryHandler(IOffBalanceRepository entries, IAccountRepository accounts)
        {
            _entries = entries;
            _accounts = accounts;
        }

        public async Task<List<OffBalanceEntryDto>> Handle(ListOffBalanceEntriesQuery request, CancellationToken cancellationToken)
        {
            var byId = (await _accounts.GetAllAsync(cancellationToken)).ToDictionary(a => a.Id);
            var entries = await _entries.ListAsync(request.AccountId, request.FromDate, request.ToDate, cancellationToken);
            return entries.Select(e => OffBalanceMapper.Map(e, byId.GetValueOrDefault(e.AccountId))).ToList();
        }
    }

    /// <summary>
    /// The off-balance register per account for a range: opening (everything posted before the range), what came in, what went out, closing.
    /// Voided entries never count. An account whose closing is negative is flagged — more went out than was ever recorded in.
    /// </summary>
    public class GetOffBalanceReportQueryHandler : IRequestHandler<GetOffBalanceReportQuery, OffBalanceReportDto>
    {
        private readonly IOffBalanceRepository _entries;
        private readonly IAccountRepository _accounts;

        public GetOffBalanceReportQueryHandler(IOffBalanceRepository entries, IAccountRepository accounts)
        {
            _entries = entries;
            _accounts = accounts;
        }

        public async Task<OffBalanceReportDto> Handle(GetOffBalanceReportQuery request, CancellationToken cancellationToken)
        {
            var from = request.FromDate.Date;
            var to = request.ToDate.Date;
            if (from > to)
                throw new BusinessRuleException("Accounting", "The start date is after the end date.");

            var accounts = (await _accounts.GetAllAsync(cancellationToken)).Where(a => a.AccountType == (int)AccountType.OffBalance).ToDictionary(a => a.Id);
            var posted = (await _entries.ListAsync(null, null, to, cancellationToken)).Where(e => e.Status == (int)OffBalanceStatus.Posted).ToList();

            var report = new OffBalanceReportDto { FromDate = from, ToDate = to };
            foreach (var group in posted.GroupBy(e => e.AccountId))
            {
                if (!accounts.TryGetValue(group.Key, out var account))
                    continue;
                var opening = group.Where(e => e.EntryDate < from).Sum(e => e.Effect);
                var inRange = group.Where(e => e.EntryDate >= from).ToList();
                var received = inRange.Where(e => e.Direction == (int)OffBalanceDirection.In).Sum(e => e.Amount);
                var released = inRange.Where(e => e.Direction == (int)OffBalanceDirection.Out).Sum(e => e.Amount);
                var closing = opening + received - released;
                if (opening == 0m && received == 0m && released == 0m && closing == 0m)
                    continue;

                report.Lines.Add(new OffBalanceLineDto
                {
                    AccountNumber = account.AccountNumber, AccountName = account.Name,
                    Opening = opening, In = received, Out = released, Closing = closing, IsNegative = closing < 0m,
                });
            }

            report.Lines = report.Lines.OrderBy(l => l.AccountNumber, StringComparer.Ordinal).ToList();
            if (report.Lines.Any(l => l.IsNegative))
                report.Notes.Add("An account has a negative closing balance: more went out than was recorded in. Check the entries.");
            report.Notes.Add("Off-balance accounts are tracked outside the double entry; they are not part of the balance sheet totals.");
            return report;
        }
    }
}
