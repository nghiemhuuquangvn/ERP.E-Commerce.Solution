namespace ERP.SO.Application.Interfaces;

/// <summary>
/// A customer's credit profile as the list shows it. <c>CustomerName</c> is the name on the customer's most recent order (the profile itself
/// carries none); it is empty for a customer who has never ordered.
/// </summary>
public sealed record CreditProfileRow(Guid CustomerId, string CustomerName, decimal CreditLimit, decimal CurrentBalance, DateTime UpdatedAt)
{
    /// <summary>What the customer may still order before approval is refused: limit minus open order value (may be negative when over the limit).</summary>
    public decimal AvailableCredit => CreditLimit - CurrentBalance;
}

/// <summary>The search text of the credit list: a customer id (exact) or part of a customer name (case-insensitive).</summary>
public sealed record CreditProfileSearch(string? Text, Guid? CustomerId)
{
    public static CreditProfileSearch Parse(string? search)
    {
        var text = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
        return new CreditProfileSearch(text, text != null && Guid.TryParse(text, out var id) ? id : null);
    }
}
