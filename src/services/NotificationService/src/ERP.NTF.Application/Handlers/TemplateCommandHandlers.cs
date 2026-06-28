using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using MediatR;

namespace ERP.NTF.Application.Handlers;

public class CreateTemplateCommandHandler : IRequestHandler<CreateTemplateCommand, NotificationTemplateDto>
{
    private readonly ITemplateRepository _templates;

    public CreateTemplateCommandHandler(ITemplateRepository templates) => _templates = templates;

    public async Task<NotificationTemplateDto> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = NotificationTemplate.Create(
            request.Code,
            request.Name,
            request.Channel,
            request.Subject,
            request.Body,
            request.Variables);

        await _templates.AddAsync(template, cancellationToken);

        return new NotificationTemplateDto
        {
            Id = template.Id,
            Code = template.Code,
            Name = template.Name,
            Channel = template.Channel.ToString(),
            Subject = template.Subject,
            Body = template.Body,
            Variables = template.Variables,
            IsActive = template.IsActive
        };
    }
}

public class UpdatePreferenceCommandHandler : IRequestHandler<UpdatePreferenceCommand, NotificationPreferenceDto>
{
    private readonly IPreferenceRepository _preferences;

    public UpdatePreferenceCommandHandler(IPreferenceRepository preferences) => _preferences = preferences;

    public async Task<NotificationPreferenceDto> Handle(UpdatePreferenceCommand request, CancellationToken cancellationToken)
    {
        var preference = await _preferences.GetByUserIdAsync(request.UserId, cancellationToken);

        if (preference == null)
        {
            preference = NotificationPreference.CreateDefault(request.UserId);
            preference.UpdateChannels(request.EmailEnabled, request.SmsEnabled, request.PushEnabled, request.InAppEnabled);
            await _preferences.AddAsync(preference, cancellationToken);
        }
        else
        {
            preference.UpdateChannels(request.EmailEnabled, request.SmsEnabled, request.PushEnabled, request.InAppEnabled);
            await _preferences.UpdateAsync(preference, cancellationToken);
        }

        return new NotificationPreferenceDto
        {
            UserId = preference.UserId,
            EmailEnabled = preference.EmailEnabled,
            SmsEnabled = preference.SmsEnabled,
            PushEnabled = preference.PushEnabled,
            InAppEnabled = preference.InAppEnabled
        };
    }
}

public class UnsubscribeCommandHandler : IRequestHandler<UnsubscribeCommand, bool>
{
    private readonly IPreferenceRepository _preferences;

    public UnsubscribeCommandHandler(IPreferenceRepository preferences) => _preferences = preferences;

    public async Task<bool> Handle(UnsubscribeCommand request, CancellationToken cancellationToken)
    {
        var preference = await _preferences.GetByUserIdAsync(request.UserId, cancellationToken);
        if (preference == null) return false;

        preference.OptOut();
        await _preferences.UpdateAsync(preference, cancellationToken);
        return true;
    }
}
