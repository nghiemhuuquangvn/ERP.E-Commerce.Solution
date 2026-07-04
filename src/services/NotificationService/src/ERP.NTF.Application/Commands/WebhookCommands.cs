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
    public string? Secret { get; set; }
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
