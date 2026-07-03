namespace ERP.PAY.Application.Interfaces;

public record FraudCheckResult(bool IsSuspicious, string? Reason);

public interface IFraudDetectionService
{
    Task<FraudCheckResult> EvaluateAsync(Guid orderId, decimal amount, CancellationToken ct = default);
}
