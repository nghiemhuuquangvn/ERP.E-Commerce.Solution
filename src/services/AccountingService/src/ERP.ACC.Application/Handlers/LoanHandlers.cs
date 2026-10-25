using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class LoanInstallmentDto
    {
        public Guid Id { get; set; }
        public DateTime DueDate { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestAmount { get; set; }
        public DateTime? PaidOn { get; set; }
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
    }

    public class RecordLoanRepaymentCommand : IRequest<LoanDto>
    {
        public Guid LoanId { get; set; }
        public Guid InstallmentId { get; set; }

        /// <summary>The day the principal was repaid; defaults to today.</summary>
        public DateTime? PaidOn { get; set; }
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
        public static LoanDto Map(Loan loan, DateTime today) => new()
        {
            Id = loan.Id,
            LoanNumber = loan.LoanNumber,
            Lender = loan.Lender,
            Principal = loan.Principal,
            InterestRatePercent = loan.InterestRatePercent,
            StartDate = loan.StartDate,
            Status = ((LoanStatus)loan.Status).ToString(),
            Notes = loan.Description,
            CancelReason = loan.CancelReason,
            Outstanding = loan.OutstandingAt(today),
            DueLater = loan.LongTermAt(today),
            DueWithinTwelveMonths = loan.OutstandingAt(today) - loan.LongTermAt(today),
            Installments = loan.Installments.OrderBy(i => i.DueDate).Select(i => new LoanInstallmentDto
            {
                Id = i.Id, DueDate = i.DueDate, PrincipalAmount = i.PrincipalAmount, InterestAmount = i.InterestAmount, PaidOn = i.PaidOn,
            }).ToList(),
        };
    }

    public class CreateLoanCommandHandler : IRequestHandler<CreateLoanCommand, LoanDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public CreateLoanCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<LoanDto> Handle(CreateLoanCommand request, CancellationToken cancellationToken)
        {
            var existing = await _uow.Loans.ListAsync(cancellationToken);
            if (existing.Any(l => string.Equals(l.LoanNumber, request.LoanNumber?.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new BusinessRuleException("Accounting", $"A loan numbered {request.LoanNumber} already exists.");

            Loan loan;
            try
            {
                loan = Loan.Create(request.LoanNumber ?? string.Empty, request.Lender ?? string.Empty, request.Principal, request.InterestRatePercent, request.StartDate,
                    request.Schedule?.Select(s => (s.DueDate, s.PrincipalAmount, s.InterestAmount)), request.MaturityDate, request.Notes);
            }
            catch (ArgumentException ex)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }

            await _uow.Loans.AddAsync(loan, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return LoanMapper.Map(loan, DateTime.UtcNow.Date);
        }
    }

    public class RecordLoanRepaymentCommandHandler : IRequestHandler<RecordLoanRepaymentCommand, LoanDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public RecordLoanRepaymentCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<LoanDto> Handle(RecordLoanRepaymentCommand request, CancellationToken cancellationToken)
        {
            var loan = await _uow.Loans.GetByIdAsync(request.LoanId, cancellationToken)
                ?? throw new KeyNotFoundException($"Loan {request.LoanId} not found.");
            try
            {
                loan.RecordRepayment(request.InstallmentId, request.PaidOn ?? DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return LoanMapper.Map(loan, DateTime.UtcNow.Date);
        }
    }

    public class CancelLoanCommandHandler : IRequestHandler<CancelLoanCommand, LoanDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public CancelLoanCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

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

            await _uow.SaveChangesAsync(cancellationToken);
            return LoanMapper.Map(loan, DateTime.UtcNow.Date);
        }
    }

    public class ListLoansQueryHandler : IRequestHandler<ListLoansQuery, List<LoanDto>>
    {
        private readonly ILoanRepository _loans;

        public ListLoansQueryHandler(ILoanRepository loans) => _loans = loans;

        public async Task<List<LoanDto>> Handle(ListLoansQuery request, CancellationToken cancellationToken) =>
            (await _loans.ListAsync(cancellationToken)).Select(l => LoanMapper.Map(l, DateTime.UtcNow.Date)).ToList();
    }

    public class GetLoanQueryHandler : IRequestHandler<GetLoanQuery, LoanDto?>
    {
        private readonly ILoanRepository _loans;

        public GetLoanQueryHandler(ILoanRepository loans) => _loans = loans;

        public async Task<LoanDto?> Handle(GetLoanQuery request, CancellationToken cancellationToken)
        {
            var loan = await _loans.GetByIdAsync(request.LoanId, cancellationToken);
            return loan == null ? null : LoanMapper.Map(loan, DateTime.UtcNow.Date);
        }
    }
}
