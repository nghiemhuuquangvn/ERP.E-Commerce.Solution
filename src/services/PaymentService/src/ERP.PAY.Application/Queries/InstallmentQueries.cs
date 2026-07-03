using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Handlers;
using ERP.PAY.Application.Interfaces;
using MediatR;

namespace ERP.PAY.Application.Queries;

public class GetInstallmentPlanByPaymentQuery : IRequest<InstallmentPlanDto?>
{
    public Guid PaymentId { get; set; }
}

public class GetInstallmentPlanByPaymentQueryHandler : IRequestHandler<GetInstallmentPlanByPaymentQuery, InstallmentPlanDto?>
{
    private readonly IInstallmentPlanRepository _plans;
    public GetInstallmentPlanByPaymentQueryHandler(IInstallmentPlanRepository plans) => _plans = plans;

    public async Task<InstallmentPlanDto?> Handle(GetInstallmentPlanByPaymentQuery request, CancellationToken cancellationToken)
    {
        var plan = await _plans.GetByPaymentIdAsync(request.PaymentId, cancellationToken);
        return plan == null ? null : CreateInstallmentPlanCommandHandler.MapToDto(plan);
    }
}
