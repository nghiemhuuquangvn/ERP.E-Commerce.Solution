using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Shared.Authorization;

/// <summary>
/// Exchanges a normal (potentially large — this platform's JWTs can carry 240+
/// permission claims, 10KB+) bearer token for a short single-use opaque ticket, so a
/// SignalR browser client's WebSocket URL (?access_token=...) stays small. Kestrel's
/// default MaxRequestLineSize is 8KB — a full JWT alone can already exceed that before
/// even reaching any external proxy, causing a 414 on the WS handshake. See
/// docs/business/ChatBot/README.md for the fuller writeup; ChatBotService is the only
/// current consumer (its ChatHub, via AuthenticationExtensions' "/hubs" fallback below),
/// but this lives in Shared since any future SignalR hub in another service would hit
/// the identical problem.
/// </summary>
public interface IHubTicketStore
{
    string IssueTicket(string bearerToken);
    bool TryConsumeTicket(string ticket, out string bearerToken);
}

public class HubTicketStore : IHubTicketStore
{
    private static readonly TimeSpan TicketTtl = TimeSpan.FromSeconds(30);
    private readonly IMemoryCache _cache;

    public HubTicketStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string IssueTicket(string bearerToken)
    {
        var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        _cache.Set(CacheKey(ticket), bearerToken, TicketTtl);
        return ticket;
    }

    public bool TryConsumeTicket(string ticket, out string bearerToken)
    {
        var key = CacheKey(ticket);
        if (_cache.TryGetValue(key, out string? token) && token is not null)
        {
            // Single-use: a ticket only exists to bridge the negotiate/WS handshake
            // that happens immediately after issuance, never meant to be reusable.
            _cache.Remove(key);
            bearerToken = token;
            return true;
        }

        bearerToken = string.Empty;
        return false;
    }

    private static string CacheKey(string ticket) => $"hub-ticket:{ticket}";
}
