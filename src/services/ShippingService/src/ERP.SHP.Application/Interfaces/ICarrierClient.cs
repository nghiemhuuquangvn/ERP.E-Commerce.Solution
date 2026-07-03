namespace ERP.SHP.Application.Interfaces;

public record CarrierOrderRequest(
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string RecipientWardName,
    string RecipientDistrictName,
    string RecipientProvinceName,
    decimal Weight,
    decimal InsuranceValue,
    string Note,
    List<CarrierItem> Items);

public record CarrierItem(string Name, int Quantity, decimal Price);
public record CreateOrderResult(bool Success, string? TrackingCode, string? OrderCode, string? ExpectedDelivery, string? Error);
public record TrackingResult(bool Success, string Status, string? Description, List<TrackingLog> Logs, string? Error);
public record TrackingLog(DateTime Time, string Status, string Location);
public record FeeRequest(string ProvinceName, string DistrictName, decimal Weight, decimal InsuranceValue);
public record ShippingFeeResult(string ServiceName, decimal Fee, string EstimatedDelivery);

public interface ICarrierClient
{
    Task<CreateOrderResult> CreateOrderAsync(CarrierOrderRequest request, CancellationToken ct = default);
    Task<TrackingResult> TrackOrderAsync(string trackingCode, CancellationToken ct = default);
    Task<List<ShippingFeeResult>> CalculateFeeAsync(FeeRequest request, CancellationToken ct = default);
}
