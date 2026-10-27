using System.Net;
using System.Text;
using ERP.SHP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.SHP.UnitTests.Carriers;

internal static class CarrierTestSupport
{
    public static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

    /// <summary>An HttpClientFactory whose clients answer from <paramref name="respond"/> and remember the requests they saw.</summary>
    public static (IHttpClientFactory Factory, List<(HttpRequestMessage Request, string Body)> Seen) Http(Func<HttpRequestMessage, string, (HttpStatusCode, string)> respond)
    {
        var seen = new List<(HttpRequestMessage, string)>();
        var handler = new FakeHandler(async (request, ct) =>
        {
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            seen.Add((request, body));
            var (status, text) = respond(request, body);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return (factory.Object, seen);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => _send(request, ct);
    }

    public static NullLogger<T> Log<T>() => NullLogger<T>.Instance;

    public static readonly CarrierAddress Shop = new("Shop A", "0900000001", "1 Nguyen Hue", "Ben Nghe", "Quan 1", "Ho Chi Minh");
    public static readonly CarrierAddress Customer = new("An Nguyen", "0911111111", "2 Le Loi", "Phuong 1", "Quan 3", "Ho Chi Minh");

    public static CarrierBookRequest Book(decimal cod = 300_000m) => new(
        "SO-1001", Shop, Customer, 1200, 300_000m, cod, "call before delivery",
        new List<CarrierParcelItem> { new("Shirt", 2, 150_000m, 600) });

    public static CarrierWebhookRequest Post(string body, string? hash = null) =>
        new("POST", hash == null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["hash"] = hash }, body);
}
