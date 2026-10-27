using Asp.Versioning;
using ERP.SHP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SHP.API.Controllers;

/// <summary>
/// Where the carriers (GHTK, Viettel Post, GHN) post their status changes. Anonymous by design — a carrier holds no token of ours — and safe because
/// <see cref="ICarrierWebhookService"/> finds the shop from the tracking code and verifies the shared secret that shop put in the callback address
/// BEFORE anything is applied. The shop gives the carrier <c>https://&lt;shop host&gt;/api/v1/shipping-webhooks/&lt;carrier&gt;?hash=&lt;secret&gt;</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipping-webhooks")]
[AllowAnonymous]
public class CarrierWebhooksController : ControllerBase
{
    private readonly ICarrierWebhookService _webhooks;

    public CarrierWebhooksController(ICarrierWebhookService webhooks) => _webhooks = webhooks;

    [HttpPost("{carrier}")]
    public async Task<IActionResult> Receive(string carrier, CancellationToken ct)
    {
        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync(ct);

        var query = Request.Query.ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var result = await _webhooks.ProcessAsync(carrier, new CarrierWebhookRequest(Request.Method, query, body), ct);
        return StatusCode(result.Reply.StatusCode, result.Reply.Body);
    }
}
