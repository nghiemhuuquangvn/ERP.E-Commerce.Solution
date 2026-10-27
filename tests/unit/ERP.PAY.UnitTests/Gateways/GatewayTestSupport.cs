using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.PAY.UnitTests.Gateways;

/// <summary>Fakes and independent signature helpers for the gateway tests: the expected values are computed here, not with the code under test.</summary>
internal static class GatewayTestSupport
{
    public static string Sha256Hex(string key, string data)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    public static string Sha512Hex(string key, string data)
    {
        using var h = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

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
}
