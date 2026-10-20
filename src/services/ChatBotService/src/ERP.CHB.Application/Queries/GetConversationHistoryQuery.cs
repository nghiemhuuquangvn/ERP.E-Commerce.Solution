using ERP.CHB.Application.Dtos;
using ERP.CHB.Application.Interfaces;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.CHB.Application.Queries;

/// <param name="GuestSessionId">Required to read a guest (anonymous) conversation.</param>
public record GetConversationHistoryQuery(Guid ConversationId, string? GuestSessionId = null) : IRequest<ConversationHistoryDto>;

public class GetConversationHistoryQueryHandler : IRequestHandler<GetConversationHistoryQuery, ConversationHistoryDto>
{
    private readonly IConversationRepository _repository;
    private readonly UserContext _userContext;

    public GetConversationHistoryQueryHandler(IConversationRepository repository, UserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    public async Task<ConversationHistoryDto> Handle(GetConversationHistoryQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetByIdAsync(request.ConversationId, cancellationToken)
            ?? throw new EntityNotFoundException("Conversation", request.ConversationId);

        if (conversation.CreatedBy is not null &&
            (!_userContext.IsAuthenticated || _userContext.UserId != conversation.CreatedBy))
            throw new UnauthorizedAccessException("This conversation does not belong to the current caller.");

        // Guest conversations have no owner account: only the browser session that started
        // it (its GuestSessionId) may read it — previously any signed-in user of any tenant
        // could read every guest conversation by id.
        if (conversation.CreatedBy is null &&
            (string.IsNullOrEmpty(request.GuestSessionId)
             || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(request.GuestSessionId),
                    System.Text.Encoding.UTF8.GetBytes(conversation.GuestSessionId ?? string.Empty))))
            throw new UnauthorizedAccessException("This conversation does not belong to the current caller.");

        var dto = new ConversationDto(conversation.Id, conversation.Channel, conversation.Provider,
            conversation.Status, conversation.StartedAt, conversation.ClosedAt, conversation.EscalatedCaseId);

        var messages = conversation.Messages
            .Where(m => m.Role != Domain.Enums.MessageRole.Tool)
            .Select(m => new MessageDto(m.Id, m.Role, m.Content, m.ToolName, m.CreatedAt))
            .ToList();

        return new ConversationHistoryDto(dto, messages);
    }
}

public record ListMyConversationsQuery(Domain.Enums.ConversationChannel? Channel) : IRequest<IReadOnlyList<ConversationDto>>;

public class ListMyConversationsQueryHandler : IRequestHandler<ListMyConversationsQuery, IReadOnlyList<ConversationDto>>
{
    private readonly IConversationRepository _repository;
    private readonly UserContext _userContext;

    public ListMyConversationsQueryHandler(IConversationRepository repository, UserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<ConversationDto>> Handle(ListMyConversationsQuery request, CancellationToken cancellationToken)
    {
        if (!_userContext.IsAuthenticated)
            throw new UnauthorizedAccessException("Listing conversation history requires authentication.");

        var tenantId = Guid.Parse(_userContext.TenantId);
        var conversations = await _repository.GetByOwnerAsync(tenantId, _userContext.UserId, null, cancellationToken);

        return conversations
            .Where(c => request.Channel == null || c.Channel == request.Channel)
            .OrderByDescending(c => c.StartedAt)
            .Select(c => new ConversationDto(c.Id, c.Channel, c.Provider, c.Status, c.StartedAt, c.ClosedAt, c.EscalatedCaseId))
            .ToList();
    }
}
