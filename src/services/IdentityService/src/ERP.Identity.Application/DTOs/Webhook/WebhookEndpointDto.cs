namespace ERP.Identity.Application.DTOs.Webhook
{
    public record WebhookEndpointDto(
        Guid Id,
        Guid TenantId,
        string Name,
        string Url,
        bool HasSecret,
        bool IsActive,
        IReadOnlyList<string> EventTypes,
        DateTime CreatedAt);

    /// <summary>EventTypes null/empty means "every eligible event type" (see
    /// WebhookEndpoint.Matches) — TwoFactorCodeGeneratedDomainEvent is never eligible
    /// regardless of what's requested here.</summary>
    public record CreateWebhookEndpointRequest(string Name, string Url, string? Secret, List<string>? EventTypes);

    public record UpdateWebhookEndpointRequest(string Name, string Url, string? Secret, List<string>? EventTypes);
}
