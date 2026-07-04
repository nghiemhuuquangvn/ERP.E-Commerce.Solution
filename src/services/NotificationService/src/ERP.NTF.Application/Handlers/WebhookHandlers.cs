using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Queries;
using ERP.NTF.Domain.Entities;
using MediatR;

namespace ERP.NTF.Application.Handlers;

internal static class WebhookMapper
{
    internal static WebhookDto ToDto(WebhookEndpoint w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        Url = w.Url,
        IsActive = w.IsActive,
        EventTypes = w.EventTypes.ToList(),
        CreatedAt = w.CreatedAt
    };
}

public class RegisterWebhookCommandHandler : IRequestHandler<RegisterWebhookCommand, WebhookDto>
{
    private readonly IWebhookRepository _webhooks;
    public RegisterWebhookCommandHandler(IWebhookRepository webhooks) => _webhooks = webhooks;

    public async Task<WebhookDto> Handle(RegisterWebhookCommand request, CancellationToken cancellationToken)
    {
        var endpoint = WebhookEndpoint.Create(request.Name, request.Url, request.Secret, request.EventTypes);
        await _webhooks.AddAsync(endpoint, cancellationToken);
        return WebhookMapper.ToDto(endpoint);
    }
}

public class UpdateWebhookCommandHandler : IRequestHandler<UpdateWebhookCommand, WebhookDto>
{
    private readonly IWebhookRepository _webhooks;
    public UpdateWebhookCommandHandler(IWebhookRepository webhooks) => _webhooks = webhooks;

    public async Task<WebhookDto> Handle(UpdateWebhookCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _webhooks.GetByIdAsync(request.WebhookId, cancellationToken)
            ?? throw new InvalidOperationException($"Webhook {request.WebhookId} not found.");
        endpoint.Update(request.Name, request.Url, request.Secret, request.EventTypes);
        await _webhooks.UpdateAsync(endpoint, cancellationToken);
        return WebhookMapper.ToDto(endpoint);
    }
}

public class DeleteWebhookCommandHandler : IRequestHandler<DeleteWebhookCommand, bool>
{
    private readonly IWebhookRepository _webhooks;
    public DeleteWebhookCommandHandler(IWebhookRepository webhooks) => _webhooks = webhooks;

    public async Task<bool> Handle(DeleteWebhookCommand request, CancellationToken cancellationToken)
    {
        await _webhooks.DeleteAsync(request.WebhookId, cancellationToken);
        return true;
    }
}

public class SetWebhookActiveCommandHandler : IRequestHandler<SetWebhookActiveCommand, WebhookDto>
{
    private readonly IWebhookRepository _webhooks;
    public SetWebhookActiveCommandHandler(IWebhookRepository webhooks) => _webhooks = webhooks;

    public async Task<WebhookDto> Handle(SetWebhookActiveCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _webhooks.GetByIdAsync(request.WebhookId, cancellationToken)
            ?? throw new InvalidOperationException($"Webhook {request.WebhookId} not found.");
        if (request.IsActive) endpoint.Activate(); else endpoint.Deactivate();
        await _webhooks.UpdateAsync(endpoint, cancellationToken);
        return WebhookMapper.ToDto(endpoint);
    }
}

public class ListWebhooksQueryHandler : IRequestHandler<ListWebhooksQuery, List<WebhookDto>>
{
    private readonly IWebhookRepository _webhooks;
    public ListWebhooksQueryHandler(IWebhookRepository webhooks) => _webhooks = webhooks;

    public async Task<List<WebhookDto>> Handle(ListWebhooksQuery request, CancellationToken cancellationToken)
    {
        var all = await _webhooks.GetAllAsync(cancellationToken);
        return all.Select(WebhookMapper.ToDto).ToList();
    }
}

public class GetWebhookQueryHandler : IRequestHandler<GetWebhookQuery, WebhookDto?>
{
    private readonly IWebhookRepository _webhooks;
    public GetWebhookQueryHandler(IWebhookRepository webhooks) => _webhooks = webhooks;

    public async Task<WebhookDto?> Handle(GetWebhookQuery request, CancellationToken cancellationToken)
    {
        var endpoint = await _webhooks.GetByIdAsync(request.WebhookId, cancellationToken);
        return endpoint == null ? null : WebhookMapper.ToDto(endpoint);
    }
}
