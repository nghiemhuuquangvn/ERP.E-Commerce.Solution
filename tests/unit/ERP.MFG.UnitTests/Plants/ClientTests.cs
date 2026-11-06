using System.Net;
using System.Text;
using ERP.MFG.Infrastructure.Clients;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.MFG.UnitTests.Plants;

public class ClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        public string? LastBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static ITenantProvider Tenant(Guid? id)
    {
        var t = new Mock<ITenantProvider>();
        t.SetupGet(x => x.CurrentTenantId).Returns(id);
        return t.Object;
    }

    private static (WarehouseLookupClient Client, StubHandler Handler) Warehouse(Func<HttpRequestMessage, HttpResponseMessage> respond, Guid? tenant, IMemoryCache? cache = null)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://warehouse/") };
        return (new WarehouseLookupClient(http, cache ?? new MemoryCache(new MemoryCacheOptions()), Tenant(tenant), NullLogger<WarehouseLookupClient>.Instance), handler);
    }

    private static readonly Guid W1 = Guid.NewGuid();

    private string Answer => $$"""{"items":[{"id":"{{W1}}","code":"WH-RM","name":"Raw","isActive":true}]}""";

    [Fact]
    public async Task A_Good_Answer_Is_Read_And_The_Ids_Are_Sent()
    {
        var (client, handler) = Warehouse(_ => Json(Answer), Guid.NewGuid());

        var result = await client.LookupAsync([W1]);

        result.Should().ContainSingle().Which.Code.Should().Be("WH-RM");
        handler.LastBody.Should().Contain(W1.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task INT_Any_Failure_Status_Fails_Closed_With_MFG_INT_001(HttpStatusCode status)
    {
        var (client, _) = Warehouse(_ => Json("{}", status), Guid.NewGuid());

        var act = () => client.LookupAsync([W1]);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_INT_001");
    }

    [Fact]
    public async Task An_Unreachable_Service_Or_Garbage_Fails_Closed_And_Is_Never_An_Empty_List()
    {
        var (down, _) = Warehouse(_ => throw new HttpRequestException("connection refused"), Guid.NewGuid());
        var (garbage, _) = Warehouse(_ => Json("not json"), Guid.NewGuid());

        (await ((Func<Task>)(() => down.LookupAsync([W1]))).Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_INT_001");
        (await ((Func<Task>)(() => garbage.LookupAsync([W1]))).Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_INT_001");
    }

    [Fact]
    public async Task The_Second_Identical_Call_Of_The_Same_Tenant_Comes_From_The_Cache()
    {
        var tenant = Guid.NewGuid();
        var (client, handler) = Warehouse(_ => Json(Answer), tenant);

        await client.LookupAsync([W1]);
        await client.LookupAsync([W1]);

        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_Failure_Is_Not_Cached_So_The_Next_Call_Tries_Again()
    {
        var fail = true;
        var (client, handler) = Warehouse(_ => fail ? Json("{}", HttpStatusCode.BadGateway) : Json(Answer), Guid.NewGuid());

        await ((Func<Task>)(() => client.LookupAsync([W1]))).Should().ThrowAsync<BusinessRuleException>();
        fail = false;

        (await client.LookupAsync([W1])).Should().ContainSingle();
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Another_Tenant_Does_Not_Read_The_Cached_Answer_Of_This_One()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var (a, handlerA) = Warehouse(_ => Json(Answer), Guid.NewGuid(), cache);
        var (b, handlerB) = Warehouse(_ => Json(Answer), Guid.NewGuid(), cache);

        await a.LookupAsync([W1]);
        await b.LookupAsync([W1]);

        handlerA.Calls.Should().Be(1);
        handlerB.Calls.Should().Be(1, "the cache key carries the tenant");
    }

    [Fact]
    public async Task Without_A_Tenant_Nothing_Is_Cached()
    {
        var (client, handler) = Warehouse(_ => Json(Answer), tenant: null);

        await client.LookupAsync([W1]);
        await client.LookupAsync([W1]);

        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Convert_Maps_404_To_Null_And_400_To_A_Business_Refusal()
    {
        static UnitOfMeasureClient Make(HttpStatusCode code, string body) => new(
            new HttpClient(new StubHandler(_ => Json(body, code))) { BaseAddress = new Uri("http://md/") },
            new MemoryCache(new MemoryCacheOptions()), Tenant(Guid.NewGuid()), NullLogger<UnitOfMeasureClient>.Instance);

        (await Make(HttpStatusCode.NotFound, "{}").ConvertAsync(Guid.NewGuid(), Guid.NewGuid(), 1)).Should().BeNull();
        (await ((Func<Task>)(() => Make(HttpStatusCode.BadRequest, "{}").ConvertAsync(Guid.NewGuid(), Guid.NewGuid(), 1))).Should().ThrowAsync<BusinessRuleException>())
            .Which.RuleName.Should().Be("MFG_VALIDATION");
        (await Make(HttpStatusCode.OK, """{"quantity":2500,"toDecimalPlaces":0}""").ConvertAsync(Guid.NewGuid(), Guid.NewGuid(), 2.5m))!.Quantity.Should().Be(2500m);
    }
}
