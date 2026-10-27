using ERP.PAY.Application.DTOs;
using ERP.PAY.Domain.Enums;
using MediatR;

namespace ERP.PAY.Application.Commands;

public class InitiatePaymentCommand : IRequest<PaymentDto>
{
    public Guid OrderId { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentMethod Method { get; set; }
    /// <summary>What the customer chose at checkout (COD, VNPay, MoMo, ZaloPay, Unspecified). When set it decides the method; null keeps <see cref="Method"/>.</summary>
    public string? Provider { get; set; }
    public Guid? CustomerId { get; set; }
    public string? OrderNumber { get; set; }
}

public class AssignTransactionRefCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
    public string TransactionRef { get; set; } = string.Empty;
}

public class LinkInvoiceCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
}

/// <summary>
/// Single entry point for both the VNPay redirect callback and the server-to-server
/// IPN — centralizing here (rather than the controller calling Authorize/Capture
/// directly) makes replay-safety a property of the handler: a gateway retry that
/// arrives after the payment is already Captured/Failed is a no-op instead of an
/// InvalidOperationException from re-authorizing a non-Pending payment.
/// </summary>
public class ProcessGatewayCallbackCommand : IRequest<PaymentDto>
{
    public string TransactionRef { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? GatewayProvider { get; set; }
    public string? FailureReason { get; set; }
    /// <summary>What the gateway says was paid; when present it must equal the payment's amount or the callback is refused and the payment flagged.</summary>
    public decimal? Amount { get; set; }
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
