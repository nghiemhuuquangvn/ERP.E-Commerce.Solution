namespace ERP.SHP.Application.DTOs;

public record CarrierRuleDto(
    Guid Id, string Name, int Priority, bool IsActive, string? MatchProvinces, int? MinWeightGrams, int? MaxWeightGrams,
    decimal? MinOrderValue, decimal? MaxOrderValue, bool? CodOnly, string CarrierCode);

public record SaveCarrierRuleRequest(
    string Name, int Priority, bool IsActive, string? MatchProvinces, int? MinWeightGrams, int? MaxWeightGrams,
    decimal? MinOrderValue, decimal? MaxOrderValue, bool? CodOnly, string CarrierCode);

/// <summary>Which carrier a parcel would go to, and what the carriers ask for it (the rules tester in the admin screen).</summary>
public record CarrierChoicePreviewDto(string? RuleCarrierCode, string? RuleName, List<CarrierQuotePreviewDto> Quotes);

public record CarrierQuotePreviewDto(string CarrierCode, string CarrierName, bool Success, decimal Fee, string? Error);
