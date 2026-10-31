using ERP.NTF.Application.DTOs;
using MediatR;

namespace ERP.NTF.Application.Commands;

public class RegisterWebhookCommand : IRequest<WebhookDto>
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public List<string> EventTypes { get; set; } = new();
}

public class UpdateWebhookCommand : IRequest<WebhookDto>
{
    public Guid WebhookId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    /// <summary>A new secret. Empty / null keeps the stored one (an edit must not wipe it); to remove it use <see cref="ClearSecret"/>.</summary>
    public string? Secret { get; set; }
    public bool ClearSecret { get; set; }
    public List<string> EventTypes { get; set; } = new();
}

public class DeleteWebhookCommand : IRequest<bool>
{
    public Guid WebhookId { get; set; }
}

public class SetWebhookActiveCommand : IRequest<WebhookDto>
{
    public Guid WebhookId { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Sends a sample event to the endpoint through the normal sender (and records it in the delivery log).</summary>
public class TestWebhookCommand : IRequest<WebhookTestResult>
{
    public Guid WebhookId { get; set; }
}
