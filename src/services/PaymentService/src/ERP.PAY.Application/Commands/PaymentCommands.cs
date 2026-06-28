using ERP.PAY.Application.DTOs;
using ERP.PAY.Domain.Enums;
using MediatR;

namespace ERP.PAY.Application.Commands;

public class InitiatePaymentCommand : IRequest<PaymentDto>
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentMethod Method { get; set; }
}

public class AuthorizePaymentCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
    public string GatewayTransactionId { get; set; } = string.Empty;
    public string GatewayProvider { get; set; } = string.Empty;
}

public class CapturePaymentCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
}

public class RefundPaymentCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ProcessSettlementCommand : IRequest<SettlementDto>
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}

public class CancelPaymentCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
}
