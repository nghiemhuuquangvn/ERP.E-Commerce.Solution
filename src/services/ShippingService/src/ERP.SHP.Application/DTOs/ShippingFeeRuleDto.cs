namespace ERP.SHP.Application.DTOs;

/// <summary>One row of the shop's delivery price list.</summary>
public record ShippingFeeRuleDto(Guid Id, string Name, int Priority, bool IsActive, string? MatchProvinces, int? MinWeightGrams, int? MaxWeightGrams, decimal Fee);

public record SaveShippingFeeRuleRequest(string Name, int Priority, bool IsActive, string? MatchProvinces, int? MinWeightGrams, int? MaxWeightGrams, decimal Fee);

/// <summary>What the customer is about to buy and where it goes; every part but the value of the goods is optional (an old caller gets the flat fee).</summary>
public class ShippingFeeRequest
{
    /// <summary>The goods after any coupon, without delivery.</summary>
    public decimal MerchandiseValue { get; set; }
    /// <summary>The province of the delivery address (the order's State).</summary>
    public string? Province { get; set; }
    public List<ShippingFeeItem> Items { get; set; } = new();
}

public record ShippingFeeItem(Guid ProductId, int Quantity);
