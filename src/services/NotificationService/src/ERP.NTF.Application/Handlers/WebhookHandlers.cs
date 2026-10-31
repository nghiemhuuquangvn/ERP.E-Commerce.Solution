using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Queries;
using ERP.NTF.Application.Webhooks;
using ERP.NTF.Domain.Entities;
using ERP.Shared.Security;
using MediatR;

namespace ERP.NTF.Application.Handlers;

internal static class WebhookMapper
{
    internal static WebhookDto ToDto(WebhookEndpoint w, WebhookDelivery? last = null) => new()
    {
        Id = w.Id,
        Name = w.Name,
        Url = w.Url,
        IsActive = w.IsActive,
        HasSecret = w.HasSecret,
        EventTypes = w.EventTypes.ToList(),
        CreatedAt = w.CreatedAt,
        LastDelivery = last == null ? null : DeliveryToDto(last)
    };

    internal static WebhookDeliveryDto DeliveryToDto(WebhookDelivery d) => new()
    {
        Id = d.Id,
        EventType = d.EventType,
        Success = d.Success,
        StatusCode = d.StatusCode,
        Error = d.Error,
        DurationMs = d.DurationMs,
        CreatedAt = d.CreatedAt
    };
}

public class RegisterWebhookCommandHandler : IRequestHandler<RegisterWebhookCommand, WebhookDto>
{
    private readonly IWebhookRepository _webhooks;
    private readonly ISecretProtector _protector;
    public RegisterWebhookCommandHandler(IWebhookRepository webhooks, ISecretProtector protector) { _webhooks = webhooks; _protector = protector; }

    public async Task<WebhookDto> Handle(RegisterWebhookCommand request, CancellationToken cancellationToken)
    {
        var name = WebhookRules.Check(request.Name, request.Url, request.Secret, request.EventTypes, out var events);
        var sealedSecret = string.IsNullOrEmpty(request.Secret) ? null : SecretEnvelope.Seal(_protector, request.Secret);
        var endpoint = WebhookEndpoint.Create(name, request.Url.Trim(), sealedSecret, events);
        await _webhooks.AddAsync(endpoint, cancellationToken);
        return WebhookMapper.ToDto(endpoint);
    }
}

public class UpdateWebhookCommandHandler : IRequestHandler<UpdateWebhookCommand, WebhookDto>
{
    private readonly IWebhookRepository _webhooks;
    private readonly ISecretProtector _protector;
    public UpdateWebhookCommandHandler(IWebhookRepository webhooks, ISecretProtector protector) { _webhooks = webhooks; _protector = protector; }

    public async Task<WebhookDto> Handle(UpdateWebhookCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _webhooks.GetByIdAsync(request.WebhookId, cancellationToken)
            ?? throw new KeyNotFoundException($"Webhook {request.WebhookId} not found.");
        var name = WebhookRules.Check(request.Name, request.Url, request.Secret, request.EventTypes, out var events);

        endpoint.Update(name, request.Url.Trim(), events);
        // The secret changes only when asked: a new value replaces it, ClearSecret removes it, an empty field keeps it.
        if (request.ClearSecret) endpoint.ClearSecret();
        else if (!string.IsNullOrEmpty(request.Secret)) endpoint.SetSecret(SecretEnvelope.Seal(_protector, request.Secret));
        else if (endpoint.HasSecret && !SecretEnvelope.IsSealed(endpoint.Secret))
            endpoint.SetSecret(SecretEnvelope.Seal(_protector, endpoint.Secret!));   // a row from before P2-1b: sealed on its next save

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
        if (!await _webhooks.DeleteAsync(request.WebhookId, cancellationToken))
            throw new KeyNotFoundException($"Webhook {request.WebhookId} not found.");
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
            ?? throw new KeyNotFoundException($"Webhook {request.WebhookId} not found.");
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
        var last = await _webhooks.GetLastDeliveriesAsync(all.Select(w => w.Id), cancellationToken);
        return all.Select(w => WebhookMapper.ToDto(w, last.GetValueOrDefault(w.Id))).ToList();
    }
}

public class GetWebhookQueryHandler : IRequestHandler<GetWebhookQuery, WebhookDto?>
{
    private readonly IWebhookRepository _webhooks;
    public GetWebhookQueryHandler(IWebhookRepository webhooks) => _webhooks = webhooks;

    public async Task<WebhookDto?> Handle(GetWebhookQuery request, CancellationToken cancellationToken)
    {
        var endpoint = await _webhooks.GetByIdAsync(request.WebhookId, cancellationToken);
        if (endpoint == null) return null;
        var last = await _webhooks.GetLastDeliveriesAsync(new[] { endpoint.Id }, cancellationToken);
        return WebhookMapper.ToDto(endpoint, last.GetValueOrDefault(endpoint.Id));
    }
}

public class ListWebhookDeliveriesQueryHandler : IRequestHandler<ListWebhookDeliveriesQuery, WebhookDeliveryPage>
{
    private readonly IWebhookRepository _webhooks;
    public ListWebhookDeliveriesQueryHandler(IWebhookRepository webhooks) => _webhooks = webhooks;

    public async Task<WebhookDeliveryPage> Handle(ListWebhookDeliveriesQuery request, CancellationToken cancellationToken)
    {
        if (await _webhooks.GetByIdAsync(request.WebhookId, cancellationToken) == null)
            throw new KeyNotFoundException($"Webhook {request.WebhookId} not found.");
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var (items, total) = await _webhooks.ListDeliveriesAsync(request.WebhookId, (page - 1) * size, size, cancellationToken);
        return new WebhookDeliveryPage { Items = items.Select(WebhookMapper.DeliveryToDto).ToList(), TotalCount = total, PageNumber = page, PageSize = size };
    }
}

public class GetWebhookEventTypesQueryHandler : IRequestHandler<GetWebhookEventTypesQuery, List<WebhookEventTypeDto>>
{
    public Task<List<WebhookEventTypeDto>> Handle(GetWebhookEventTypesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(WebhookEventTypes.Subscribable.ToList());
}

/// <summary>The "send test" button: a sample event through the same sender as real ones, so the answer (and the delivery log) is the truth.</summary>
public class TestWebhookCommandHandler : IRequestHandler<TestWebhookCommand, WebhookTestResult>
{
    private readonly IWebhookRepository _webhooks;
    private readonly IWebhookSender _sender;
    public TestWebhookCommandHandler(IWebhookRepository webhooks, IWebhookSender sender) { _webhooks = webhooks; _sender = sender; }

    public async Task<WebhookTestResult> Handle(TestWebhookCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _webhooks.GetByIdAsync(request.WebhookId, cancellationToken)
            ?? throw new KeyNotFoundException($"Webhook {request.WebhookId} not found.");

        var result = await _sender.SendAsync(endpoint, WebhookEventTypes.Test,
            new { Message = "This is a test event from the ERP notification service.", SentAt = DateTime.UtcNow }, cancellationToken);
        return new WebhookTestResult { Success = result.Success, StatusCode = result.StatusCode, DurationMs = result.DurationMs, Error = result.Error };
    }
}
