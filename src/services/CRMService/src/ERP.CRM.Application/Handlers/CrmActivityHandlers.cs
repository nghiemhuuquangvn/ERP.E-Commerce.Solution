using ERP.CRM.Application.Commands;
using ERP.CRM.Application.DTOs;
using ERP.CRM.Application.Interfaces;
using ERP.CRM.Application.Queries;
using ERP.CRM.Domain.Entities;
using ERP.CRM.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.CRM.Application.Handlers;

// ─── Activity Handlers ────────────────────────────────────────────────────────

public class CreateActivityCommandHandler : IRequestHandler<CreateActivityCommand, ActivityDto>
{
    private readonly IActivityRepository _activities;
    private readonly ILogger<CreateActivityCommandHandler> _logger;

    public CreateActivityCommandHandler(IActivityRepository activities, ILogger<CreateActivityCommandHandler> logger)
    {
        _activities = activities;
        _logger = logger;
    }

    public async Task<ActivityDto> Handle(CreateActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = Activity.Create(
            request.ContactId,
            (ActivityType)request.ActivityType,
            request.Subject,
            request.Notes,
            request.ScheduledAt,
            request.OpportunityId,
            request.CaseId,
            request.AssignedToUserId,
            request.Source);

        await _activities.AddAsync(activity, cancellationToken);
        _logger.LogInformation("Activity {ActivityId} ({Type}) created for Contact {ContactId}", activity.Id, activity.ActivityType, request.ContactId);
        return MapToDto(activity);
    }

    internal static ActivityDto MapToDto(Activity a) => new(
        a.Id, a.ContactId, a.OpportunityId, a.CaseId,
        ((ActivityType)a.ActivityType).ToString(),
        a.Subject, a.Notes,
        ((ActivityStatus)a.Status).ToString(),
        a.ScheduledAt, a.CompletedAt, a.AssignedToUserId, a.Source, a.CreatedAt);
}

public class CompleteActivityCommandHandler : IRequestHandler<CompleteActivityCommand, ActivityDto>
{
    private readonly IActivityRepository _activities;
    private readonly ILogger<CompleteActivityCommandHandler> _logger;

    public CompleteActivityCommandHandler(IActivityRepository activities, ILogger<CompleteActivityCommandHandler> logger)
    {
        _activities = activities;
        _logger = logger;
    }

    public async Task<ActivityDto> Handle(CompleteActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await _activities.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Activity {request.ActivityId} not found.");

        activity.Complete(request.Notes);
        await _activities.UpdateAsync(activity, cancellationToken);
        _logger.LogInformation("Activity {ActivityId} completed", request.ActivityId);
        return CreateActivityCommandHandler.MapToDto(activity);
    }
}

public class CancelActivityCommandHandler : IRequestHandler<CancelActivityCommand, ActivityDto>
{
    private readonly IActivityRepository _activities;
    private readonly ILogger<CancelActivityCommandHandler> _logger;

    public CancelActivityCommandHandler(IActivityRepository activities, ILogger<CancelActivityCommandHandler> logger)
    {
        _activities = activities;
        _logger = logger;
    }

    public async Task<ActivityDto> Handle(CancelActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await _activities.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Activity {request.ActivityId} not found.");

        activity.Cancel();
        await _activities.UpdateAsync(activity, cancellationToken);
        _logger.LogInformation("Activity {ActivityId} cancelled", request.ActivityId);
        return CreateActivityCommandHandler.MapToDto(activity);
    }
}

public class UpdateActivityNotesCommandHandler : IRequestHandler<UpdateActivityNotesCommand, ActivityDto>
{
    private readonly IActivityRepository _activities;

    public UpdateActivityNotesCommandHandler(IActivityRepository activities) => _activities = activities;

    public async Task<ActivityDto> Handle(UpdateActivityNotesCommand request, CancellationToken cancellationToken)
    {
        var activity = await _activities.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Activity {request.ActivityId} not found.");

        activity.UpdateNotes(request.Notes);
        await _activities.UpdateAsync(activity, cancellationToken);
        return CreateActivityCommandHandler.MapToDto(activity);
    }
}

public class GetActivityQueryHandler : IRequestHandler<GetActivityQuery, ActivityDto?>
{
    private readonly IActivityRepository _activities;
    public GetActivityQueryHandler(IActivityRepository activities) => _activities = activities;

    public async Task<ActivityDto?> Handle(GetActivityQuery request, CancellationToken cancellationToken)
    {
        var activity = await _activities.GetByIdAsync(request.ActivityId, cancellationToken);
        return activity is null ? null : CreateActivityCommandHandler.MapToDto(activity);
    }
}

public class ListActivitiesQueryHandler : IRequestHandler<ListActivitiesQuery, PagedResult<ActivityDto>>
{
    private readonly IActivityRepository _activities;
    public ListActivitiesQueryHandler(IActivityRepository activities) => _activities = activities;

    public async Task<PagedResult<ActivityDto>> Handle(ListActivitiesQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _activities.GetPagedAsync(
            request.ContactId, request.OpportunityId, request.ActivityType,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<ActivityDto>(
            items.Select(CreateActivityCommandHandler.MapToDto), total, request.Page, request.PageSize);
    }
}

// ─── Contact Tag Handlers ─────────────────────────────────────────────────────

public class AddContactTagCommandHandler : IRequestHandler<AddContactTagCommand, ContactTagsDto>
{
    private readonly IContactTagRepository _tags;
    private readonly ILogger<AddContactTagCommandHandler> _logger;

    public AddContactTagCommandHandler(IContactTagRepository tags, ILogger<AddContactTagCommandHandler> logger)
    {
        _tags = tags;
        _logger = logger;
    }

    public async Task<ContactTagsDto> Handle(AddContactTagCommand request, CancellationToken cancellationToken)
    {
        var existing = await _tags.GetByContactIdAsync(request.ContactId, cancellationToken);
        var normalizedTag = request.Tag.Trim().ToLowerInvariant();

        if (!existing.Any(t => t.Tag == normalizedTag))
        {
            var tag = ContactTag.Create(request.ContactId, request.Tag);
            await _tags.AddAsync(tag, cancellationToken);
            _logger.LogInformation("Tag '{Tag}' added to Contact {ContactId}", normalizedTag, request.ContactId);
        }

        var allTags = await _tags.GetByContactIdAsync(request.ContactId, cancellationToken);
        return new ContactTagsDto(request.ContactId, allTags.Select(t => t.Tag));
    }
}

public class RemoveContactTagCommandHandler : IRequestHandler<RemoveContactTagCommand, ContactTagsDto>
{
    private readonly IContactTagRepository _tags;
    private readonly ILogger<RemoveContactTagCommandHandler> _logger;

    public RemoveContactTagCommandHandler(IContactTagRepository tags, ILogger<RemoveContactTagCommandHandler> logger)
    {
        _tags = tags;
        _logger = logger;
    }

    public async Task<ContactTagsDto> Handle(RemoveContactTagCommand request, CancellationToken cancellationToken)
    {
        await _tags.DeleteAsync(request.ContactId, request.Tag, cancellationToken);
        _logger.LogInformation("Tag '{Tag}' removed from Contact {ContactId}", request.Tag, request.ContactId);

        var remaining = await _tags.GetByContactIdAsync(request.ContactId, cancellationToken);
        return new ContactTagsDto(request.ContactId, remaining.Select(t => t.Tag));
    }
}

public class GetContactTagsQueryHandler : IRequestHandler<GetContactTagsQuery, ContactTagsDto>
{
    private readonly IContactTagRepository _tags;
    public GetContactTagsQueryHandler(IContactTagRepository tags) => _tags = tags;

    public async Task<ContactTagsDto> Handle(GetContactTagsQuery request, CancellationToken cancellationToken)
    {
        var tags = await _tags.GetByContactIdAsync(request.ContactId, cancellationToken);
        return new ContactTagsDto(request.ContactId, tags.Select(t => t.Tag));
    }
}

public class ListContactsByTagQueryHandler : IRequestHandler<ListContactsByTagQuery, PagedResult<ContactDto>>
{
    private readonly IContactTagRepository _tags;
    private readonly IContactRepository _contacts;

    public ListContactsByTagQueryHandler(IContactTagRepository tags, IContactRepository contacts)
    {
        _tags = tags;
        _contacts = contacts;
    }

    public async Task<PagedResult<ContactDto>> Handle(ListContactsByTagQuery request, CancellationToken cancellationToken)
    {
        var contactIds = (await _tags.GetContactIdsByTagAsync(request.Tag, cancellationToken)).ToList();
        var (items, total) = await _contacts.GetByIdsPagedAsync(contactIds, request.Page, request.PageSize, cancellationToken);
        return new PagedResult<ContactDto>(items.Select(CreateContactCommandHandler.MapToDto), total, request.Page, request.PageSize);
    }
}

public class GetAllTagsQueryHandler : IRequestHandler<GetAllTagsQuery, IEnumerable<string>>
{
    private readonly IContactTagRepository _tags;
    public GetAllTagsQueryHandler(IContactTagRepository tags) => _tags = tags;

    public async Task<IEnumerable<string>> Handle(GetAllTagsQuery request, CancellationToken cancellationToken)
        => await _tags.GetAllTagsAsync(cancellationToken);
}
