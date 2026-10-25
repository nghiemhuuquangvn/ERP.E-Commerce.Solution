using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.Handlers
{
    public class LoanInstallmentDto
    {
        public Guid Id { get; set; }
        public DateTime DueDate { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestAmount { get; set; }
        public DateTime? PaidOn { get; set; }

        /// <summary>Interest of the installment already accrued to the books and not yet paid.</summary>
        public decimal AccruedInterest { get; set; }

        /// <summary>The interest owed on this installment if it were repaid today (by the loan's day count); the default of the interest paid. 0 once repaid.</summary>
        public decimal InterestDueToday { get; set; }
    }

    public class LoanDto
    {
        public Guid Id { get; set; }
        public string LoanNumber { get; set; } = string.Empty;
        public string Lender { get; set; } = string.Empty;
        public decimal Principal { get; set; }
        public decimal InterestRatePercent { get; set; }
        public DateTime StartDate { get; set; }

        /// <summary>"Active", "Repaid" or "Cancelled".</summary>
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? CancelReason { get; set; }

        /// <summary>True when the drawdown is in the journal, so repayments are posted too; false for a loan booked by hand.</summary>
        public bool PostedToLedger { get; set; }

        /// <summary>0 = the scheduled interest split by days, 1 = actual/365, 2 = actual/360.</summary>
        public int DayCountBasis { get; set; }

        /// <summary>The asset account the money is received on and repaid from; null = the Bank key of the posting profile.</summary>
        public Guid? ReceivingAccountId { get; set; }
        public string? ReceivingAccountNumber { get; set; }
        public string? ReceivingAccountName { get; set; }

        /// <summary>Principal still owed today, and the parts due within 12 months and later.</summary>
        public decimal Outstanding { get; set; }
        public decimal DueWithinTwelveMonths { get; set; }
        public decimal DueLater { get; set; }
        public List<LoanInstallmentDto> Installments { get; set; } = new();
    }

    public class LoanInstallmentInput
    {
        public DateTime DueDate { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestAmount { get; set; }
    }

    public class CreateLoanCommand : IRequest<LoanDto>
    {
        public string LoanNumber { get; set; } = string.Empty;
        public string Lender { get; set; } = string.Empty;
        public decimal Principal { get; set; }
        public decimal InterestRatePercent { get; set; }
        public DateTime StartDate { get; set; }

        /// <summary>Used only when no schedule is given: one installment of the whole principal on this date.</summary>
        public DateTime? MaturityDate { get; set; }
        public List<LoanInstallmentInput> Schedule { get; set; } = new();
        public string? Notes { get; set; }

        /// <summary>Post the drawdown (Dr Bank / Cr Loan payable). Set false for a loan already booked by hand: it then stays out of the ledger postings.</summary>
        public bool PostDrawdown { get; set; } = true;

        /// <summary>The asset account (bank or cash) that receives the money and repays it; empty = the Bank key of the posting profile.</summary>
        public Guid? ReceivingAccountId { get; set; }

        /// <summary>0 (default) = the scheduled interest split by days; 1 = actual/365; 2 = actual/360 (outstanding principal × annual rate per day).</summary>
        public int DayCountBasis { get; set; }
    }

    public class PostLoanDrawdownCommand : IRequest<LoanDto>
    {
        public Guid LoanId { get; set; }

        /// <summary>Sets the receiving account of the loan before it is posted; empty keeps the loan's current one.</summary>
        public Guid? ReceivingAccountId { get; set; }
    }

    /// <summary>Accrues the interest of every posted active loan up to a date (Dr interest expense / Cr interest payable); safe to run again for the same date.</summary>
    public class AccrueLoanInterestCommand : IRequest<LoanInterestAccrualDto>
    {
        public DateTime AsOfDate { get; set; }

        /// <summary>The month-end job sets it: when the period of the date is closed nothing is posted (a user's entry would fall into the current period, a back-dated accrual must not).</summary>
        public bool SkipClosedPeriod { get; set; }
    }

    public class LoanInterestAccrualLineDto
    {
        public Guid LoanId { get; set; }
        public string LoanNumber { get; set; } = string.Empty;
        public DateTime InstallmentDueDate { get; set; }
        public decimal Amount { get; set; }

        /// <summary>False when the posting profile was incomplete: the interest stays un-accrued for the next run.</summary>
        public bool Posted { get; set; }
    }

    public class LoanInterestAccrualDto
    {
        public DateTime AsOfDate { get; set; }
        public decimal TotalAccrued { get; set; }

        /// <summary>True when nothing was accrued because the period of the date is closed (only with SkipClosedPeriod).</summary>
        public bool SkippedClosedPeriod { get; set; }
        public List<LoanInterestAccrualLineDto> Lines { get; set; } = new();
    }

    public class RecordLoanRepaymentCommand : IRequest<LoanDto>
    {
        public Guid LoanId { get; set; }
        public Guid InstallmentId { get; set; }

        /// <summary>The day the principal was repaid; defaults to today.</summary>
        public DateTime? PaidOn { get; set; }

        /// <summary>The interest actually paid; empty = the scheduled interest of the installment.</summary>
        public decimal? InterestPaid { get; set; }
    }

    public class CancelLoanCommand : IRequest<LoanDto>
    {
        public Guid LoanId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ListLoansQuery : IRequest<List<LoanDto>> { }

    public class GetLoanQuery : IRequest<LoanDto?>
    {
        public Guid LoanId { get; set; }
    }

    internal static class LoanMapper
    {
        public static LoanDto Map(Loan loan, DateTime today, IReadOnlyDictionary<Guid, Account>? accounts = null) => new()
        {
            ReceivingAccountId = loan.ReceivingAccountId,
            ReceivingAccountNumber = loan.ReceivingAccountId.HasValue && accounts != null && accounts.TryGetValue(loan.ReceivingAccountId.Value, out var ra) ? ra.AccountNumber : null,
            ReceivingAccountName = loan.ReceivingAccountId.HasValue && accounts != null && accounts.TryGetValue(loan.ReceivingAccountId.Value, out var rn) ? rn.Name : null,
            Id = loan.Id,
            LoanNumber = loan.LoanNumber,
            Lender = loan.Lender,
            Principal = loan.Principal,
            InterestRatePercent = loan.InterestRatePercent,
            StartDate = loan.StartDate,
            Status = ((LoanStatus)loan.Status).ToString(),
            Notes = loan.Description,
            CancelReason = loan.CancelReason,
            PostedToLedger = loan.PostedToLedger,
            DayCountBasis = loan.DayCountBasis,
            Outstanding = loan.OutstandingAt(today),
            DueLater = loan.LongTermAt(today),
            DueWithinTwelveMonths = loan.OutstandingAt(today) - loan.LongTermAt(today),
            Installments = loan.Installments.OrderBy(i => i.DueDate).Select(i => new LoanInstallmentDto
            {
                Id = i.Id, DueDate = i.DueDate, PrincipalAmount = i.PrincipalAmount, InterestAmount = i.InterestAmount, PaidOn = i.PaidOn, AccruedInterest = i.PaidOn.HasValue ? 0m : i.AccruedInterest,
                InterestDueToday = i.PaidOn.HasValue ? 0m : loan.DayCountBasis == 0 ? i.InterestAmount : loan.InterestToDate(i, today),
            }).ToList(),
        };
    }

    internal static class LoanAccounts
    {
        /// <summary>The receiving account must exist, take postings (active, not a parent, not off-balance) and be an asset (a bank or cash account).</summary>
        public static async Task<Account?> EnsureReceivingAsync(IAccountingUnitOfWork uow, Guid? accountId, CancellationToken ct)
        {
            if (!accountId.HasValue || accountId == Guid.Empty)
                return null;
            var account = await uow.Accounts.GetByIdAsync(accountId.Value, ct)
                ?? throw new BusinessRuleException("Accounting", $"Account {accountId} does not exist.");
            AccountRules.EnsurePostable(account, await AccountRules.ParentIdsAsync(uow.Accounts, ct));
            if (account.AccountType != (int)AccountType.Asset)
                throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} is not an asset account: choose the bank or cash account that receives the loan.");
            return account;
        }

        public static async Task<Dictionary<Guid, Account>> LoadAsync(IAccountRepository accounts, IEnumerable<Loan> loans, CancellationToken ct)
        {
            var ids = loans.Where(l => l.ReceivingAccountId.HasValue).Select(l => l.ReceivingAccountId!.Value).Distinct().ToList();
            return ids.Count == 0 ? new Dictionary<Guid, Account>() : (await accounts.GetByIdsAsync(ids, ct)).ToDictionary(a => a.Id);
        }
    }

    public class CreateLoanCommandHandler : IRequestHandler<CreateLoanCommand, LoanDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<CreateLoanCommandHandler> _logger;

        public CreateLoanCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<CreateLoanCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<LoanDto> Handle(CreateLoanCommand request, CancellationToken cancellationToken)
        {
            var existing = await _uow.Loans.ListAsync(cancellationToken);
            if (existing.Any(l => string.Equals(l.LoanNumber, request.LoanNumber?.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new BusinessRuleException("Accounting", $"A loan numbered {request.LoanNumber} already exists.");

            Loan loan;
            try
            {
                loan = Loan.Create(request.LoanNumber ?? string.Empty, request.Lender ?? string.Empty, request.Principal, request.InterestRatePercent, request.StartDate,
                    request.Schedule?.Select(s => (s.DueDate, s.PrincipalAmount, s.InterestAmount)), request.MaturityDate, request.Notes, request.DayCountBasis);
            }
            catch (ArgumentException ex)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }

            var receiving = await LoanAccounts.EnsureReceivingAsync(_uow, request.ReceivingAccountId, cancellationToken);
            loan.SetReceivingAccount(receiving?.Id);
            await _uow.Loans.AddAsync(loan, cancellationToken);
            if (request.PostDrawdown)
                await LoanPosting.PostDrawdownAsync(_posting, _logger, loan, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return LoanMapper.Map(loan, DateTime.UtcNow.Date, receiving == null ? null : new Dictionary<Guid, Account> { [receiving.Id] = receiving });
        }
    }

    public class RecordLoanRepaymentCommandHandler : IRequestHandler<RecordLoanRepaymentCommand, LoanDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<RecordLoanRepaymentCommandHandler> _logger;

        public RecordLoanRepaymentCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<RecordLoanRepaymentCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<LoanDto> Handle(RecordLoanRepaymentCommand request, CancellationToken cancellationToken)
        {
            var loan = await _uow.Loans.GetByIdAsync(request.LoanId, cancellationToken)
                ?? throw new KeyNotFoundException($"Loan {request.LoanId} not found.");
            LoanInstallment installment;
            try
            {
                installment = loan.RecordRepayment(request.InstallmentId, request.PaidOn ?? DateTime.UtcNow, request.InterestPaid);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }

            // a loan booked by hand keeps its repayments by hand too
            var repayment = loan.PostedToLedger ? PostingRules.ForLoanRepayment(loan, installment, installment.PaidOn ?? DateTime.UtcNow) : null;
            if (repayment != null)
                await _posting.TryPostAsync(_logger, repayment, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return LoanMapper.Map(loan, DateTime.UtcNow.Date, await LoanAccounts.LoadAsync(_uow.Accounts, new[] { loan }, cancellationToken));
        }
    }

    public class CancelLoanCommandHandler : IRequestHandler<CancelLoanCommand, LoanDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<CancelLoanCommandHandler> _logger;

        public CancelLoanCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<CancelLoanCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<LoanDto> Handle(CancelLoanCommand request, CancellationToken cancellationToken)
        {
            var loan = await _uow.Loans.GetByIdAsync(request.LoanId, cancellationToken)
                ?? throw new KeyNotFoundException($"Loan {request.LoanId} not found.");
            try
            {
                loan.Cancel(request.Reason);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }

            // nothing was repaid (Cancel refuses otherwise), so the drawdown is the only entry to take back
            if (loan.PostedToLedger)
                await _posting.TryReverseAsync(_logger, PostingRules.Loan, loan.Id, $"Loan {loan.LoanNumber} cancelled: {request.Reason}", DateTime.UtcNow.Date, cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);
            return LoanMapper.Map(loan, DateTime.UtcNow.Date);
        }
    }

    public class PostLoanDrawdownCommandHandler : IRequestHandler<PostLoanDrawdownCommand, LoanDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PostLoanDrawdownCommandHandler> _logger;

        public PostLoanDrawdownCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PostLoanDrawdownCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<LoanDto> Handle(PostLoanDrawdownCommand request, CancellationToken cancellationToken)
        {
            var loan = await _uow.Loans.GetByIdAsync(request.LoanId, cancellationToken)
                ?? throw new KeyNotFoundException($"Loan {request.LoanId} not found.");
            if (loan.Status != (int)LoanStatus.Active || loan.Installments.Any(i => i.PaidOn.HasValue))
                throw new BusinessRuleException("Accounting", "Only an active loan with no repayment can be posted: book the repayments of an older loan by hand.");
            if (loan.PostedToLedger)
                throw new BusinessRuleException("Accounting", "The drawdown of this loan is already posted.");

            if (request.ReceivingAccountId.HasValue)
                loan.SetReceivingAccount((await LoanAccounts.EnsureReceivingAsync(_uow, request.ReceivingAccountId, cancellationToken))?.Id);

            if (!await LoanPosting.PostDrawdownAsync(_posting, _logger, loan, cancellationToken))
                throw new BusinessRuleException("Accounting", "The drawdown could not be posted: complete the posting profile (Bank and Loan payable accounts).");
            await _uow.SaveChangesAsync(cancellationToken);
            return LoanMapper.Map(loan, DateTime.UtcNow.Date);
        }
    }

    public class AccrueLoanInterestCommandHandler : IRequestHandler<AccrueLoanInterestCommand, LoanInterestAccrualDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<AccrueLoanInterestCommandHandler> _logger;

        public AccrueLoanInterestCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<AccrueLoanInterestCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<LoanInterestAccrualDto> Handle(AccrueLoanInterestCommand request, CancellationToken cancellationToken)
        {
            var day = request.AsOfDate.Date;
            var result = new LoanInterestAccrualDto { AsOfDate = day };
            if (request.SkipClosedPeriod)
            {
                var period = await _uow.FiscalPeriods.GetContainingAsync(day, cancellationToken);
                if (period != null && period.Status != (int)FiscalPeriodStatus.Open)
                {
                    _logger.LogWarning("Loan interest to {Day:yyyy-MM-dd} not accrued: fiscal period {Period} is closed", day, period.PeriodName);
                    result.SkippedClosedPeriod = true;
                    return result;
                }
            }
            // the list is read without tracking; each loan is loaded again so its accrued amounts are saved with the entries
            foreach (var summary in (await _uow.Loans.ListAsync(cancellationToken)).Where(l => l.PostedToLedger && l.Status == (int)LoanStatus.Active))
            {
                var loan = await _uow.Loans.GetByIdAsync(summary.Id, cancellationToken);
                if (loan == null)
                    continue;
                foreach (var (installment, amount) in loan.AccrueInterest(day))
                {
                    var entry = await _posting.TryPostAsync(_logger, PostingRules.ForLoanAccrual(loan, installment, amount, day), cancellationToken);
                    if (entry == null)
                        loan.UndoAccrual(installment, amount);   // not in the books: do not remember it as accrued
                    result.Lines.Add(new LoanInterestAccrualLineDto
                    {
                        LoanId = loan.Id, LoanNumber = loan.LoanNumber, InstallmentDueDate = installment.DueDate, Amount = amount, Posted = entry != null,
                    });
                    if (entry != null)
                        result.TotalAccrued += amount;
                }
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return result;
        }
    }

    internal static class LoanPosting
    {
        /// <summary>Posts the drawdown and marks the loan; false when the posting profile is incomplete (the loan is then left unposted).</summary>
        public static async Task<bool> PostDrawdownAsync(IJournalPostingService posting, ILogger logger, Loan loan, CancellationToken ct)
        {
            var entry = await posting.TryPostAsync(logger, PostingRules.ForLoanDrawdown(loan), ct);
            if (entry == null)
                return false;
            loan.MarkPosted();
            return true;
        }
    }

    public class ListLoansQueryHandler : IRequestHandler<ListLoansQuery, List<LoanDto>>
    {
        private readonly ILoanRepository _loans;
        private readonly IAccountRepository _accounts;

        public ListLoansQueryHandler(ILoanRepository loans, IAccountRepository accounts)
        {
            _loans = loans;
            _accounts = accounts;
        }

        public async Task<List<LoanDto>> Handle(ListLoansQuery request, CancellationToken cancellationToken)
        {
            var loans = await _loans.ListAsync(cancellationToken);
            var accounts = await LoanAccounts.LoadAsync(_accounts, loans, cancellationToken);
            return loans.Select(l => LoanMapper.Map(l, DateTime.UtcNow.Date, accounts)).ToList();
        }
    }

    public class GetLoanQueryHandler : IRequestHandler<GetLoanQuery, LoanDto?>
    {
        private readonly ILoanRepository _loans;
        private readonly IAccountRepository _accounts;

        public GetLoanQueryHandler(ILoanRepository loans, IAccountRepository accounts)
        {
            _loans = loans;
            _accounts = accounts;
        }

        public async Task<LoanDto?> Handle(GetLoanQuery request, CancellationToken cancellationToken)
        {
            var loan = await _loans.GetByIdAsync(request.LoanId, cancellationToken);
            return loan == null ? null : LoanMapper.Map(loan, DateTime.UtcNow.Date, await LoanAccounts.LoadAsync(_accounts, new[] { loan }, cancellationToken));
        }
    }
}
