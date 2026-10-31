using System.Text.RegularExpressions;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.NTF.Application.Handlers;

/// <summary>
/// Editing the message templates (ROADMAP P2-1b). A template is either the PLATFORM's default or the shop's OWN row with the same code (which wins).
/// A shop never edits the platform row: it overrides it (a copy it owns), changes the copy, and deleting the copy brings the default back.
/// Only the platform tenant edits the platform rows, and nobody deletes them (they can be switched off).
/// </summary>
internal static class TemplateRules
{
    private static readonly Regex CodePattern = new("^[A-Z0-9_]{2,100}$", RegexOptions.Compiled);
    private static readonly Regex Placeholder = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    internal static string NormalizeCode(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodePattern.IsMatch(c))
            throw new BusinessRuleException("Template.Code", "The code is 2-100 characters: capital letters, digits and underscores (ORDER_SHIPPED).");
        return c;
    }

    internal static (string Name, string Subject, string Body, string? Variables) CheckContent(string? name, string? subject, string? body, string? variables)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new BusinessRuleException("Template.Name", "A template needs a name of up to 200 characters.");
        if ((subject ?? string.Empty).Length > 500)
            throw new BusinessRuleException("Template.Subject", "The subject can be at most 500 characters.");
        if (string.IsNullOrWhiteSpace(body))
            throw new BusinessRuleException("Template.Body", "The message text is required.");
        var vars = string.IsNullOrWhiteSpace(variables) ? null
            : string.Join(",", variables.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase));
        return (name.Trim(), (subject ?? string.Empty).Trim(), body, vars);
    }

    internal static bool IsPlatform(NotificationTemplate t) => t.TenantId == WellKnownTenants.Platform;

    /// <summary>Throws unless the caller may change this row.</summary>
    internal static void EnsureEditable(NotificationTemplate t, Guid? callerTenant)
    {
        if (IsPlatform(t) && callerTenant != WellKnownTenants.Platform)
            throw new BusinessRuleException("Template.Platform", "This is the platform's default template. Override it to change the wording for your company.");
    }

    internal static NotificationTemplateDto ToDto(NotificationTemplate t) => new()
    {
        Id = t.Id,
        Code = t.Code,
        Name = t.Name,
        Channel = t.Channel.ToString(),
        Subject = t.Subject,
        Body = t.Body,
        Variables = t.Variables,
        IsActive = t.IsActive,
        IsPlatformDefault = IsPlatform(t)
    };

    /// <summary>The {placeholders} of a text, in order of appearance, without repeats.</summary>
    internal static List<string> PlaceholdersOf(params string?[] texts) =>
        texts.Where(x => !string.IsNullOrEmpty(x)).SelectMany(x => Placeholder.Matches(x!).Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

public class UpdateTemplateCommandHandler : IRequestHandler<UpdateTemplateCommand, NotificationTemplateDto>
{
    private readonly ITemplateRepository _templates;
    private readonly ICurrentTenantAccessor _tenant;
    public UpdateTemplateCommandHandler(ITemplateRepository templates, ICurrentTenantAccessor tenant) { _templates = templates; _tenant = tenant; }

    public async Task<NotificationTemplateDto> Handle(UpdateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _templates.GetByIdAsync(request.TemplateId, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {request.TemplateId} not found.");
        TemplateRules.EnsureEditable(template, _tenant.CurrentTenantId);
        var (name, subject, body, variables) = TemplateRules.CheckContent(request.Name, request.Subject, request.Body, request.Variables);

        template.Update(name, subject, body, variables);
        await _templates.UpdateAsync(template, cancellationToken);
        return TemplateRules.ToDto(template);
    }
}

public class SetTemplateActiveCommandHandler : IRequestHandler<SetTemplateActiveCommand, NotificationTemplateDto>
{
    private readonly ITemplateRepository _templates;
    private readonly ICurrentTenantAccessor _tenant;
    public SetTemplateActiveCommandHandler(ITemplateRepository templates, ICurrentTenantAccessor tenant) { _templates = templates; _tenant = tenant; }

    public async Task<NotificationTemplateDto> Handle(SetTemplateActiveCommand request, CancellationToken cancellationToken)
    {
        var template = await _templates.GetByIdAsync(request.TemplateId, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {request.TemplateId} not found.");
        TemplateRules.EnsureEditable(template, _tenant.CurrentTenantId);

        if (request.IsActive) template.Activate(); else template.Deactivate();
        await _templates.UpdateAsync(template, cancellationToken);
        return TemplateRules.ToDto(template);
    }
}

public class DeleteTemplateCommandHandler : IRequestHandler<DeleteTemplateCommand, bool>
{
    private readonly ITemplateRepository _templates;
    public DeleteTemplateCommandHandler(ITemplateRepository templates) => _templates = templates;

    public async Task<bool> Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _templates.GetByIdAsync(request.TemplateId, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {request.TemplateId} not found.");
        if (TemplateRules.IsPlatform(template))
            throw new BusinessRuleException("Template.Platform", "A platform template cannot be deleted; switch it off instead.");

        await _templates.DeleteAsync(template, cancellationToken);   // a shop's own copy: the platform default is used again
        return true;
    }
}

public class OverrideTemplateCommandHandler : IRequestHandler<OverrideTemplateCommand, NotificationTemplateDto>
{
    private readonly ITemplateRepository _templates;
    private readonly ICurrentTenantAccessor _tenant;
    public OverrideTemplateCommandHandler(ITemplateRepository templates, ICurrentTenantAccessor tenant) { _templates = templates; _tenant = tenant; }

    public async Task<NotificationTemplateDto> Handle(OverrideTemplateCommand request, CancellationToken cancellationToken)
    {
        var source = await _templates.GetByIdAsync(request.TemplateId, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {request.TemplateId} not found.");
        var caller = _tenant.CurrentTenantId;
        if (!TemplateRules.IsPlatform(source))
            throw new BusinessRuleException("Template.Override", "Only a platform default can be overridden; this template is already your own.");
        if (caller == null || caller == WellKnownTenants.Platform)
            throw new BusinessRuleException("Template.Override", "The platform edits its default templates directly.");
        if (await _templates.ExistsForTenantAsync(source.Code, caller.Value, cancellationToken))
            throw new BusinessRuleException("Template.Override", "Your company already has its own template with this code.");

        var copy = NotificationTemplate.Create(source.Code, source.Name, source.Channel, source.Subject, source.Body, source.Variables);
        await _templates.AddAsync(copy, cancellationToken);
        return TemplateRules.ToDto(copy);
    }
}

/// <summary>Shows what a text becomes with sample values, and which {placeholders} no sample (declared variable) covers. Changes nothing.</summary>
public class PreviewTemplateCommandHandler : IRequestHandler<PreviewTemplateCommand, TemplatePreviewResult>
{
    public Task<TemplatePreviewResult> Handle(PreviewTemplateCommand request, CancellationToken cancellationToken)
    {
        var values = request.Variables ?? new Dictionary<string, string>();
        var known = new HashSet<string>(values.Keys, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(new TemplatePreviewResult
        {
            Subject = NotificationTemplate.Fill(request.Subject ?? string.Empty, values),
            Body = NotificationTemplate.Fill(request.Body ?? string.Empty, values),
            UnknownPlaceholders = TemplateRules.PlaceholdersOf(request.Subject, request.Body).Where(p => !known.Contains(p)).ToList()
        });
    }
}
