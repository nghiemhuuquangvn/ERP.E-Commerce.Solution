using ERP.CHB.Application.Dtos;
using ERP.CHB.Application.Interfaces;
using ERP.CHB.Domain.Entities;
using ERP.CHB.Domain.Enums;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.CHB.Application.Commands;

/// <summary>GuestTenantId/GuestSessionId only apply to an unauthenticated Storefront guest — see StartConversationCommandHandler for how they combine with UserContext. GuestTenantId is optional and only cross-checked: the guest's tenant is the request host's.</summary>
public record StartConversationCommand(ConversationChannel Channel, Guid? GuestTenantId, string? GuestSessionId) : IRequest<ConversationDto>;

public class StartConversationCommandHandler : IRequestHandler<StartConversationCommand, ConversationDto>
{
    private readonly IConversationRepository _repository;
    private readonly IChatProviderResolver _providerResolver;
    private readonly UserContext _userContext;
    private readonly ITenantProvider _tenantProvider;

    public StartConversationCommandHandler(IConversationRepository repository, IChatProviderResolver providerResolver, UserContext userContext, ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
        _repository = repository;
        _providerResolver = providerResolver;
        _userContext = userContext;
    }

    public async Task<ConversationDto> Handle(StartConversationCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId;
        string? userId;
        string? guestSessionId;
        AiProvider provider;

        if (_userContext.IsAuthenticated)
        {
            if (request.Channel == ConversationChannel.Admin && !_userContext.HasPermission("chatbot:assistant:use"))
                throw new UnauthorizedAccessException("Missing permission: chatbot:assistant:use");

            // No HasModule(ModuleCodes.ChatBot) check here — LicenseEnforcementMiddleware
            // (ModuleIdentity(ModuleCodes.ChatBot), registered in Program.cs) already rejects
            // an unlicensed tenant with a proper 403 before the request ever reaches this
            // handler, same as every other service's module gate.
            tenantId = Guid.Parse(_userContext.TenantId);
            userId = _userContext.UserId;
            guestSessionId = null;
            provider = _providerResolver.ResolveProvider(_userContext);
        }
        else
        {
            // Anonymous Storefront guest: no JWT, so no LicensedModules/TenantId to read.
            // Guests only ever get the free provider — CHB_PREMIUM (paid Claude tier) is
            // never granted to an unauthenticated caller, and always require a client-supplied
            // GuestSessionId to key the conversation.
            if (request.Channel != ConversationChannel.Storefront)
                throw new UnauthorizedAccessException("The Admin assistant requires authentication.");
            if (string.IsNullOrWhiteSpace(request.GuestSessionId))
                throw new BusinessRuleException("GuestConversationRequiresIdentity", "Guest conversations require a GuestSessionId.");

            // The guest belongs to the tenant of the storefront host (Gateway X-Tenant-Id /
            // ChatHub host resolution) — never to a tenant id the client sends. A client value
            // that disagrees is rejected rather than silently overridden.
            if (_tenantProvider.CurrentTenantId is not { } hostTenant)
                throw new BusinessRuleException("GuestTenantUnknown", "The storefront host does not belong to a known tenant.");
            if (request.GuestTenantId is { } claimed && claimed != hostTenant)
                throw new UnauthorizedAccessException("GuestTenantId does not match the storefront host.");

            tenantId = hostTenant;
            userId = null;
            guestSessionId = request.GuestSessionId;
            provider = AiProvider.ChatGpt;
        }

        var conversation = Conversation.Start(tenantId, request.Channel, provider, userId, guestSessionId);
        await _repository.AddAsync(conversation, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToDto(conversation);
    }

    internal static ConversationDto ToDto(Conversation c) =>
        new(c.Id, c.Channel, c.Provider, c.Status, c.StartedAt, c.ClosedAt, c.EscalatedCaseId);
}
