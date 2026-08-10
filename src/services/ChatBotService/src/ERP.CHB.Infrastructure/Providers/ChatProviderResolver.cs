using ERP.CHB.Application.Interfaces;
using ERP.CHB.Domain.Enums;
using ERP.Shared.Authorization;
using Microsoft.Extensions.Configuration;

namespace ERP.CHB.Infrastructure.Providers;

/// <summary>
/// Decides Claude vs ChatGPT purely from the caller's existing licensed_module JWT claims
/// (ModuleCodes.ChatBotPremium) — see docs/business/Identity/License.md and the plan doc's
/// "Đa nhà cung cấp LLM" section. No billing/subscription code lives here; this only reads
/// what IdentityService already embedded in the token at login/refresh.
/// </summary>
public class ChatProviderResolver : IChatProviderResolver
{
    private readonly IReadOnlyDictionary<AiProvider, IChatCompletionProvider> _providers;
    private readonly IConfiguration _configuration;

    public ChatProviderResolver(IEnumerable<IChatCompletionProvider> providers, IConfiguration configuration)
    {
        _providers = providers.ToDictionary(p => p.Provider);
        _configuration = configuration;
    }

    public AiProvider ResolveProvider(UserContext caller) =>
        caller.HasModule(ModuleCodes.ChatBotPremium) ? AiProvider.Claude : AiProvider.ChatGpt;

    public IChatCompletionProvider GetProvider(AiProvider provider) => _providers[provider];

    public string GetModel(AiProvider provider, ConversationChannel channel) => provider switch
    {
        AiProvider.Claude => channel == ConversationChannel.Admin
            ? _configuration["ChatBotSettings:AdminModel"] ?? "claude-sonnet-5"
            : _configuration["ChatBotSettings:StorefrontModel"] ?? "claude-haiku-4-5",
        AiProvider.ChatGpt => _configuration["ChatBotSettings:FreeModel"] ?? "gpt-4o-mini",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };
}
