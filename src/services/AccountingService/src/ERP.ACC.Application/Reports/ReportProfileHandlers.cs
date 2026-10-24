using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Reports
{
    /// <summary>The header, signatories and amount unit of a tenant's statements (ROADMAP P1-2e).</summary>
    public class ReportProfileDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PreparedBy { get; set; } = string.Empty;
        public string ChiefAccountant { get; set; } = string.Empty;
        public string Director { get; set; } = string.Empty;

        /// <summary>"Vnd", "Thousand" or "Million".</summary>
        public string AmountUnit { get; set; } = nameof(Domain.Enums.AmountUnit.Vnd);
    }

    public class GetReportProfileQuery : IRequest<ReportProfileDto> { }

    public class UpdateReportProfileCommand : IRequest<ReportProfileDto>
    {
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public string? Address { get; set; }
        public string? PreparedBy { get; set; }
        public string? ChiefAccountant { get; set; }
        public string? Director { get; set; }

        /// <summary>"Vnd", "Thousand" or "Million".</summary>
        public string AmountUnit { get; set; } = nameof(Domain.Enums.AmountUnit.Vnd);
    }

    public static class ReportProfileMapper
    {
        public static ReportProfileDto Map(ReportProfile? profile) => profile == null
            ? new ReportProfileDto()
            : new ReportProfileDto
            {
                CompanyName = profile.CompanyName,
                TaxCode = profile.TaxCode,
                Address = profile.Address,
                PreparedBy = profile.PreparedBy,
                ChiefAccountant = profile.ChiefAccountant,
                Director = profile.Director,
                AmountUnit = ((AmountUnit)profile.AmountUnit).ToString(),
            };

        /// <summary>"vnd", "thousand", "million" (any case); null when unknown.</summary>
        public static AmountUnit? ParseUnit(string? text) =>
            Enum.TryParse<AmountUnit>(text, ignoreCase: true, out var unit) && Enum.IsDefined(typeof(AmountUnit), unit) ? unit : null;
    }

    public class GetReportProfileQueryHandler : IRequestHandler<GetReportProfileQuery, ReportProfileDto>
    {
        private readonly IReportProfileRepository _profiles;

        public GetReportProfileQueryHandler(IReportProfileRepository profiles) => _profiles = profiles;

        public async Task<ReportProfileDto> Handle(GetReportProfileQuery request, CancellationToken cancellationToken) =>
            ReportProfileMapper.Map(await _profiles.GetAsync(cancellationToken));
    }

    public class UpdateReportProfileCommandHandler : IRequestHandler<UpdateReportProfileCommand, ReportProfileDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly ITenantProvider _tenant;

        public UpdateReportProfileCommandHandler(IAccountingUnitOfWork uow, ITenantProvider tenant)
        {
            _uow = uow;
            _tenant = tenant;
        }

        public async Task<ReportProfileDto> Handle(UpdateReportProfileCommand request, CancellationToken cancellationToken)
        {
            var unit = ReportProfileMapper.ParseUnit(request.AmountUnit)
                ?? throw new BusinessRuleException("Accounting", "The amount unit must be Vnd, Thousand or Million.");

            var profile = await _uow.ReportProfiles.GetAsync(cancellationToken);
            if (profile == null)
            {
                var tenantId = _tenant.CurrentTenantId ?? throw new BusinessRuleException("Accounting", "No tenant in the request.");
                profile = ReportProfile.Create(tenantId);
                await _uow.ReportProfiles.AddAsync(profile, cancellationToken);
            }

            try
            {
                profile.Set(request.CompanyName, request.TaxCode, request.Address, request.PreparedBy, request.ChiefAccountant, request.Director, unit);
            }
            catch (ArgumentException ex)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return ReportProfileMapper.Map(profile);
        }
    }
}
