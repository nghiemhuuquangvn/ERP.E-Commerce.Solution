using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class ClosingPolicyDto
    {
        /// <summary>"Monthly" or "YearEnd".</summary>
        public string ClosingMode { get; set; } = nameof(Domain.Enums.ClosingMode.Monthly);

        /// <summary>1–12; the fiscal year ends on the last day of this month.</summary>
        public int FiscalYearEndMonth { get; set; } = 12;
    }

    public class GetClosingPolicyQuery : IRequest<ClosingPolicyDto> { }

    /// <summary>Sets how the tenant closes its books (ROADMAP P1-2d).</summary>
    public class UpdateClosingPolicyCommand : IRequest<ClosingPolicyDto>
    {
        /// <summary>"Monthly" or "YearEnd".</summary>
        public string ClosingMode { get; set; } = string.Empty;

        public int FiscalYearEndMonth { get; set; } = 12;
    }

    public class GetClosingPolicyQueryHandler : IRequestHandler<GetClosingPolicyQuery, ClosingPolicyDto>
    {
        private readonly IClosingPolicyRepository _policies;

        public GetClosingPolicyQueryHandler(IClosingPolicyRepository policies) => _policies = policies;

        public async Task<ClosingPolicyDto> Handle(GetClosingPolicyQuery request, CancellationToken cancellationToken) =>
            UpdateClosingPolicyCommandHandler.Map(await _policies.GetAsync(cancellationToken));
    }

    public class UpdateClosingPolicyCommandHandler : IRequestHandler<UpdateClosingPolicyCommand, ClosingPolicyDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly ITenantProvider _tenant;

        public UpdateClosingPolicyCommandHandler(IAccountingUnitOfWork uow, ITenantProvider tenant)
        {
            _uow = uow;
            _tenant = tenant;
        }

        public async Task<ClosingPolicyDto> Handle(UpdateClosingPolicyCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<ClosingMode>(request.ClosingMode, ignoreCase: true, out var mode) || !Enum.IsDefined(typeof(ClosingMode), mode))
                throw new BusinessRuleException("Accounting", "The closing mode must be Monthly or YearEnd.");
            if (request.FiscalYearEndMonth is < 1 or > 12)
                throw new BusinessRuleException("Accounting", "The fiscal year end month must be 1 to 12.");

            var policy = await _uow.ClosingPolicies.GetAsync(cancellationToken);
            var currentMode = policy != null ? (ClosingMode)policy.ClosingMode : ClosingMode.Monthly;
            var currentMonth = policy?.FiscalYearEndMonth ?? 12;

            if (mode != currentMode || request.FiscalYearEndMonth != currentMonth)
            {
                // a fiscal year closed half one way and half the other would be closed twice or not at all: change only between years
                var periods = await _uow.FiscalPeriods.GetAllAsync(cancellationToken);
                var lastClosed = periods.Where(p => p.Status == (int)FiscalPeriodStatus.Closed).OrderByDescending(p => p.EndDate).FirstOrDefault();
                if (lastClosed != null && !ClosingPolicy.EndsFiscalYear(lastClosed, currentMonth))
                    throw new BusinessRuleException("Accounting",
                        $"The fiscal year is in progress ({lastClosed.PeriodName} is closed but is not the last period of the year): change the closing mode after the year is closed, or reopen the closed periods first.");
            }

            if (policy == null)
            {
                var tenantId = _tenant.CurrentTenantId ?? throw new BusinessRuleException("Accounting", "No tenant in the request.");
                policy = ClosingPolicy.Create(tenantId, mode, request.FiscalYearEndMonth);
                await _uow.ClosingPolicies.AddAsync(policy, cancellationToken);
            }
            else
            {
                policy.Set(mode, request.FiscalYearEndMonth);
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return Map(policy);
        }

        internal static ClosingPolicyDto Map(ClosingPolicy? policy) => new()
        {
            ClosingMode = policy != null ? ((ClosingMode)policy.ClosingMode).ToString() : nameof(ClosingMode.Monthly),
            FiscalYearEndMonth = policy?.FiscalYearEndMonth ?? 12,
        };
    }
}
