using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.PO.Application.Procurement;

/// <summary>
/// The day range and the text a procurement list (RFQs, supplier invoices, vendor returns) can be narrowed by. <c>From</c> / <c>To</c> are
/// calendar days, both inclusive; the day a list is judged on is chosen by the store (RFQ and return: the day created; invoice: the invoice date).
/// </summary>
public sealed record ListWindow(DateOnly? From = null, DateOnly? To = null, string? Search = null)
{
    public static readonly ListWindow None = new();

    /// <summary>First instant of the range, or null.</summary>
    public DateTime? Start => From?.ToDateTime(TimeOnly.MinValue);

    /// <summary>First instant after the range (the day after <c>To</c>), or null.</summary>
    public DateTime? Before => To?.AddDays(1).ToDateTime(TimeOnly.MinValue);

    /// <summary>The search text, trimmed and lower case; null when there is none.</summary>
    public string? Text => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim().ToLower();

    /// <summary>Refuses a range that ends before it starts.</summary>
    public ListWindow Checked()
    {
        if (From.HasValue && To.HasValue && From > To)
            throw new BusinessRuleException("Purchasing", "The start date is after the end date.");
        return this;
    }

    /// <summary>A status word of a list filter; an unknown word is refused (it used to be ignored, so every row came back).</summary>
    public static TEnum? ParseStatus<TEnum>(string? status, string what) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        if (Enum.TryParse<TEnum>(status.Replace("_", "").Replace(" ", ""), true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new BusinessRuleException("Purchasing", $"Unknown {what} status '{status}'.");
    }
}
