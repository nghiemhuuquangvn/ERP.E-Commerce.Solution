using ERP.Shared.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.Shared.UnitTests.Authorization;

/// <summary>Risk R19 (found by the browser walk-through): the SignalR JS client sends the ticket in the Authorization header on negotiate and in ?access_token= afterwards.</summary>
public class HubTokenResolverTests
{
    private readonly HubTicketStore _tickets = new(new MemoryCache(new MemoryCacheOptions()));

    private DefaultHttpContext Request(string path, string? query = null, string? bearer = null, bool withStore = true)
    {
        var services = new ServiceCollection();
        if (withStore) services.AddSingleton<IHubTicketStore>(_tickets);
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Path = path;
        if (query != null) http.Request.QueryString = new QueryString("?access_token=" + query);
        if (bearer != null) http.Request.Headers.Authorization = "Bearer " + bearer;
        return http;
    }

    [Fact]
    public void The_negotiate_request_carries_the_ticket_in_the_authorization_header()
    {
        var ticket = _tickets.IssueTicket("real.jwt.token");

        HubTokenResolver.Resolve(Request("/hubs/notifications/negotiate", bearer: ticket)).Should().Be("real.jwt.token");
    }

    [Fact]
    public void The_websocket_request_carries_the_ticket_in_the_query_and_a_ticket_works_once()
    {
        var ticket = _tickets.IssueTicket("real.jwt.token");

        HubTokenResolver.Resolve(Request("/hubs/chat", query: ticket)).Should().Be("real.jwt.token");
        HubTokenResolver.Resolve(Request("/hubs/chat", query: ticket)).Should().Be(ticket);   // consumed: no longer a ticket, so it is just an (invalid) token
    }

    [Fact]
    public void A_value_that_is_not_a_ticket_is_used_as_it_is_and_other_routes_are_left_alone()
    {
        HubTokenResolver.Resolve(Request("/hubs/chat", bearer: "a.real.jwt")).Should().Be("a.real.jwt");
        HubTokenResolver.Resolve(Request("/hubs/chat", query: "abc", withStore: false)).Should().Be("abc");
        HubTokenResolver.Resolve(Request("/api/v1/orders", bearer: "x")).Should().BeNull();
        HubTokenResolver.Resolve(Request("/hubs/chat")).Should().BeNull();
    }
}
