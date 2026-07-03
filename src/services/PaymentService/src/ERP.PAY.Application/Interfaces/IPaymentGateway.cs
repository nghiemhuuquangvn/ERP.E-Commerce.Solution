namespace ERP.PAY.Application.Interfaces;

public record GatewayCreateResult(bool Success, string PaymentUrl, string TransactionRef, string? Error);
public record GatewayVerifyResult(bool Success, bool IsPaid, string TransactionRef, string? GatewayTransactionId, string? Error);
public record GatewayRefundResult(bool Success, string? GatewayRefundId, string? Error);

public interface IPaymentGateway
{
    Task<GatewayCreateResult> CreatePaymentUrlAsync(Guid paymentId, decimal amount, string currency, string returnUrl, CancellationToken ct = default);
    Task<GatewayVerifyResult> VerifyCallbackAsync(IReadOnlyDictionary<string, string> queryParams);
    Task<GatewayRefundResult> RefundAsync(string gatewayTransactionId, string transactionRef, decimal amount, string reason, CancellationToken ct = default);
}
