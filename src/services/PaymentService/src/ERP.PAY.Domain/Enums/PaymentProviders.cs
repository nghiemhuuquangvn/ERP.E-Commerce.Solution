namespace ERP.PAY.Domain.Enums;

/// <summary>The payment choices a customer can make at checkout. "Unspecified" (or null) is an order that did not say: it keeps the pre-P1-10 behaviour.</summary>
public static class PaymentProviders
{
    public const string Cod = "COD";
    public const string VnPay = "VNPay";
    public const string MoMo = "MoMo";
    public const string ZaloPay = "ZaloPay";
    public const string Unspecified = "Unspecified";

    public static readonly string[] Gateways = { VnPay, MoMo, ZaloPay };

    public static bool IsGateway(string? provider) => Gateways.Contains(provider, StringComparer.OrdinalIgnoreCase);

    /// <summary>Canonical spelling of a provider code, or null when it is not one of ours.</summary>
    public static string? Normalize(string? provider) =>
        new[] { Cod, VnPay, MoMo, ZaloPay, Unspecified }.FirstOrDefault(p => string.Equals(p, provider?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static PaymentMethod MethodFor(string? provider) => Normalize(provider) switch
    {
        Cod => PaymentMethod.Cash,
        VnPay or MoMo or ZaloPay => PaymentMethod.EWallet,
        _ => PaymentMethod.CreditCard,   // Unspecified: what the saga always used before P1-10
    };
}
