using ERP.Shared.Core.Domain;

namespace ERP.SHP.Domain.Entities;

/// <summary>One carrier notification already applied (unique per carrier, tracking code, status and action time): a retry changes nothing.</summary>
public class CarrierWebhookLog : BaseEntity
{
    public string CarrierCode { get; private set; } = string.Empty;
    public string TrackingCode { get; private set; } = string.Empty;
    public string CarrierStatus { get; private set; } = string.Empty;
    public DateTime ActionTime { get; private set; }
    public DateTime ReceivedAt { get; private set; }

    protected CarrierWebhookLog() { }

    public static CarrierWebhookLog Create(Guid tenantId, string carrierCode, string trackingCode, string carrierStatus, DateTime actionTime) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CarrierCode = carrierCode,
        TrackingCode = trackingCode,
        CarrierStatus = carrierStatus,
        ActionTime = actionTime,
        ReceivedAt = DateTime.UtcNow,
        CreatedBy = "Carrier",
        CreatedAt = DateTime.UtcNow
    };
}
