namespace ERP.PRD.Domain.Weights;

/// <summary>A variant's weight in grams, whatever unit it was typed in (ROADMAP P1-11b: the carriers want grams).</summary>
public static class WeightConverter
{
    /// <summary>Null when there is no usable weight: none, zero or negative, or a unit this does not know (the caller then falls back to the shop's default).</summary>
    public static int? ToGrams(decimal? weight, string? unit)
    {
        if (weight is not > 0) return null;
        var perUnit = (unit ?? "kg").Trim().ToLowerInvariant() switch
        {
            "kg" or "kgs" or "kilogram" or "kilograms" => 1000m,
            "g" or "gr" or "gram" or "grams" => 1m,
            "lb" or "lbs" or "pound" or "pounds" => 453.59237m,
            "oz" or "ounce" or "ounces" => 28.349523125m,
            _ => 0m,
        };
        if (perUnit == 0m) return null;
        var grams = (int)Math.Round(weight.Value * perUnit, MidpointRounding.AwayFromZero);
        return grams > 0 ? grams : 1;
    }
}
