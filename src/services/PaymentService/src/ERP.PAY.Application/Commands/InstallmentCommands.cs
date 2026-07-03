using ERP.PAY.Application.DTOs;
using MediatR;

namespace ERP.PAY.Application.Commands;

public class CreateInstallmentPlanCommand : IRequest<InstallmentPlanDto>
{
    public Guid PaymentId { get; set; }
    public int TotalInstallments { get; set; }
    public DateTime FirstDueDate { get; set; }
    public int IntervalDays { get; set; } = 30;
}

public class RecordInstallmentPaymentCommand : IRequest<InstallmentPlanDto>
{
    public Guid InstallmentPlanId { get; set; }
    public int SequenceNo { get; set; }
}
