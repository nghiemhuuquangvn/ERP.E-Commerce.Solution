using ERP.Shared.Core.Domain;

namespace ERP.PAY.Domain.Entities;

/// <summary>
/// One part of a payment's cash collected on delivery (ROADMAP R43): the money of ONE parcel. <see cref="ShipmentId"/> is the idempotency
/// key — a redelivered delivery message for the same parcel never captures twice.
/// </summary>
public class PaymentCapture : BaseEntity
{
    public Guid PaymentId { get; private set; }
    public Guid ShipmentId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CapturedAt { get; private set; }

    private PaymentCapture() { }

    public static PaymentCapture Create(Guid paymentId, Guid shipmentId, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        PaymentId = paymentId,
        ShipmentId = shipmentId,
        Amount = amount,
        CapturedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}
