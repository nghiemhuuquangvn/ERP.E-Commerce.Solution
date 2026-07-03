using ERP.PAY.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ERP.PAY.Application.Services;

/// <summary>
/// Payment.OrderId is unique per payment (one payment row per order), and Payment
/// carries no CustomerId (that relationship is owned by CRM/SalesOrder) — so the only
/// signal available locally, without a cross-service call, is the transaction amount
/// itself. This is a real, if modest, AML-style large-transaction flag rather than a
/// velocity/device/IP check, which would need data this service does not have.
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly decimal _largeAmountThreshold;

    public FraudDetectionService(IConfiguration configuration)
    {
        _largeAmountThreshold = decimal.TryParse(configuration["Fraud:LargeAmountThreshold"], out var configured)
            ? configured
            : 50_000_000m; // default threshold, VND-scale
    }

    public Task<FraudCheckResult> EvaluateAsync(Guid orderId, decimal amount, CancellationToken ct = default)
    {
        if (amount >= _largeAmountThreshold)
        {
            return Task.FromResult(new FraudCheckResult(
                true, $"Amount {amount:N0} meets or exceeds the large-transaction review threshold ({_largeAmountThreshold:N0})"));
        }

        return Task.FromResult(new FraudCheckResult(false, null));
    }
}
