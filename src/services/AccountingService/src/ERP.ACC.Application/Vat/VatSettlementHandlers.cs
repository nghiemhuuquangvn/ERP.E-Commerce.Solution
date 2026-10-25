using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.Vat
{
    public class VatSettlementDto
    {
        public Guid FiscalPeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime AsOfDate { get; set; }

        /// <summary>Credit balance of the VAT output account(s) at the end of the period (what is owed before the deduction).</summary>
        public decimal OutputVat { get; set; }

        /// <summary>Debit balance of the VAT input account(s) at the end of the period (what can be deducted).</summary>
        public decimal InputVat { get; set; }

        /// <summary>The part settled: the smaller of the two.</summary>
        public decimal Amount { get; set; }
        public decimal PayableAfter { get; set; }
        public decimal CarryForwardAfter { get; set; }

        /// <summary>True when input and output VAT share one account (the 14-account chart): they net by themselves and nothing is settled.</summary>
        public bool SharedAccount { get; set; }
        public bool Settled { get; set; }
        public string? EntryNumber { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class GetVatSettlementQuery : IRequest<VatSettlementDto>
    {
        public Guid FiscalPeriodId { get; set; }
    }

    /// <summary>Settles the VAT of a fiscal period: Dr VAT output / Cr VAT input for min(output, input). Refused when it was already settled (reverse that entry first), or the period is closed.</summary>
    public class SettleVatCommand : IRequest<VatSettlementDto>
    {
        public Guid FiscalPeriodId { get; set; }
    }

    internal static class VatSettlementMath
    {
        public const int MaxRounds = 5;

        public static async Task<VatSettlementDto> ComputeAsync(IAccountingUnitOfWork uow, FiscalPeriod period, CancellationToken ct)
        {
            var accounts = await uow.Accounts.GetAllAsync(ct);
            var mappings = await uow.PostingMappings.GetAllAsync(ct);
            var output = VatLedger.Resolve(accounts, mappings, PostingKeys.VatOutput);
            var input = VatLedger.Resolve(accounts, mappings, PostingKeys.VatInput);
            var dto = new VatSettlementDto { FiscalPeriodId = period.Id, PeriodName = period.PeriodName, AsOfDate = period.EndDate.Date };
            dto.SharedAccount = output != null && input != null && output.Id == input.Id;
            if (output == null || input == null)
            {
                dto.Notes.Add("The VAT output and VAT input keys are not mapped to accounts: complete the posting profile.");
                return dto;
            }

            var balances = await uow.JournalLines.GetAccountBalancesAsync(null, period.EndDate.Date, null, ct);
            var outIds = VatLedger.WithChildren(accounts, output).ToHashSet();
            var inIds = VatLedger.WithChildren(accounts, input).ToHashSet();
            dto.OutputVat = Math.Max(0m, balances.Where(b => outIds.Contains(b.AccountId)).Sum(b => b.TotalCredit - b.TotalDebit));
            dto.InputVat = Math.Max(0m, balances.Where(b => inIds.Contains(b.AccountId)).Sum(b => b.TotalDebit - b.TotalCredit));
            if (dto.SharedAccount)
            {
                dto.Notes.Add("Input and output VAT share one account on this chart: they net by themselves, nothing to settle.");
                return dto;
            }
            dto.Amount = Math.Min(dto.OutputVat, dto.InputVat);
            dto.PayableAfter = dto.OutputVat - dto.Amount;
            dto.CarryForwardAfter = dto.InputVat - dto.Amount;
            return dto;
        }

        /// <summary>The entry of the latest settlement round of the period and the first free round.</summary>
        public static async Task<(JournalEntry? Latest, int FreeRound)> RoundsAsync(IAccountingUnitOfWork uow, Guid periodId, CancellationToken ct)
        {
            JournalEntry? latest = null;
            for (var round = 0; round < MaxRounds; round++)
            {
                var id = PostingRules.ForVatSettlement(periodId, round, 1m, string.Empty, DateTime.UtcNow).SourceId;
                var entry = await uow.JournalEntries.GetBySourceKeyAsync(JournalEntry.BuildSourceKey(PostingRules.VatSettlement, id), ct);
                if (entry == null)
                    return (latest, round);
                latest = entry;
            }
            return (latest, MaxRounds);
        }
    }

    public class GetVatSettlementQueryHandler : IRequestHandler<GetVatSettlementQuery, VatSettlementDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public GetVatSettlementQueryHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<VatSettlementDto> Handle(GetVatSettlementQuery request, CancellationToken cancellationToken)
        {
            var period = await _uow.FiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new KeyNotFoundException($"Fiscal period {request.FiscalPeriodId} not found.");
            var dto = await VatSettlementMath.ComputeAsync(_uow, period, cancellationToken);
            var (latest, _) = await VatSettlementMath.RoundsAsync(_uow, period.Id, cancellationToken);
            if (latest != null && latest.Status != (int)JournalStatus.Reversed)
            {
                dto.Settled = true;
                dto.EntryNumber = latest.EntryNumber;
            }
            return dto;
        }
    }

    public class SettleVatCommandHandler : IRequestHandler<SettleVatCommand, VatSettlementDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<SettleVatCommandHandler> _logger;

        public SettleVatCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<SettleVatCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<VatSettlementDto> Handle(SettleVatCommand request, CancellationToken cancellationToken)
        {
            var period = await _uow.FiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new KeyNotFoundException($"Fiscal period {request.FiscalPeriodId} not found.");
            if (period.Status != (int)FiscalPeriodStatus.Open)
                throw new BusinessRuleException("Accounting", $"Fiscal period {period.PeriodName} is closed: reopen it before settling its VAT.");

            var (latest, round) = await VatSettlementMath.RoundsAsync(_uow, period.Id, cancellationToken);
            if (latest != null && latest.Status != (int)JournalStatus.Reversed)
                throw new BusinessRuleException("Accounting", $"The VAT of {period.PeriodName} is already settled by entry {latest.EntryNumber}: reverse that entry to settle it again.");
            if (round >= VatSettlementMath.MaxRounds)
                throw new BusinessRuleException("Accounting", "The VAT of this period was settled and reversed too many times.");

            var dto = await VatSettlementMath.ComputeAsync(_uow, period, cancellationToken);
            if (dto.Amount <= 0m)
            {
                dto.Notes.Add("Nothing to settle: there is no output VAT or no input VAT at the end of the period.");
                return dto;
            }

            var entry = await _posting.TryPostAsync(_logger, PostingRules.ForVatSettlement(period.Id, round, dto.Amount, period.PeriodName, period.EndDate), cancellationToken);
            if (entry == null)
                throw new BusinessRuleException("Accounting", "The VAT settlement could not be posted: complete the posting profile (VAT output and VAT input).");
            await _uow.SaveChangesAsync(cancellationToken);
            dto.Settled = true;
            dto.EntryNumber = entry.EntryNumber;
            dto.PayableAfter = dto.OutputVat - dto.Amount;
            dto.CarryForwardAfter = dto.InputVat - dto.Amount;
            return dto;
        }
    }
}
