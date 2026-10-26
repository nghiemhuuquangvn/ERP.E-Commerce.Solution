using Asp.Versioning;
using ERP.PAY.Application.Handlers;
using ERP.PAY.Application.Interfaces;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PAY.API.Controllers;

/// <summary>
/// Online payments (P1-10): which methods a shop offers, starting the payment of an order, its status, and what the gateways send back
/// (the customer's browser return and the server-to-server notification). VNPay, MoMo and ZaloPay share all of it; the provider is a route segment.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payment-gateway")]
public class PaymentGatewayController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IGatewayNotificationService _notifications;
    private readonly IPaymentRepository _payments;
    private readonly UserContext _userContext;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentGatewayController> _logger;

    public PaymentGatewayController(IMediator mediator, IGatewayNotificationService notifications, IPaymentRepository payments,
        UserContext userContext, IConfiguration config, ILogger<PaymentGatewayController> logger)
    {
        _mediator = mediator;
        _notifications = notifications;
        _payments = payments;
        _userContext = userContext;
        _config = config;
        _logger = logger;
    }

    /// <summary>The payment methods this shop offers at checkout: cash on delivery (if on) and each gateway that is available, switched on and has keys.</summary>
    [HttpGet("methods")]
    [AuthorizeAnyAuthenticatedUser]
    public async Task<IActionResult> Methods(CancellationToken ct) => Ok(await _mediator.Send(new GetPaymentMethodsQuery(), ct));

    /// <summary>
    /// Starts the gateway payment of the caller's own order and returns the page to send the customer to. Safe to call again while the payment is
    /// still Pending (a new reference per attempt). Staff with <c>payments:create</c> may start it for any order.
    /// </summary>
    [HttpPost("orders/{orderId:guid}/checkout")]
    [AuthorizeAnyAuthenticatedUser]
    public async Task<IActionResult> Checkout(Guid orderId, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new StartCheckoutCommand
            {
                OrderId = orderId,
                CallerId = CallerId(),
                CallerIsStaff = _userContext.HasPermission("payments:create"),
                PublicBaseUrl = PublicBaseUrl()
            }, ct);
            return Ok(new { paymentUrl = result.PaymentUrl, transactionRef = result.TransactionRef, provider = result.Provider });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Status of the payment of an order (for the result page and the mobile app). Only the order's customer, or staff with <c>payments:view</c>.</summary>
    [HttpGet("orders/{orderId:guid}/status")]
    [AuthorizeAnyAuthenticatedUser]
    public async Task<IActionResult> Status(Guid orderId, CancellationToken ct)
    {
        var status = await _mediator.Send(new GetOrderPaymentStatusQuery
        {
            OrderId = orderId,
            CallerId = CallerId(),
            CallerIsStaff = _userContext.HasPermission("payments:view")
        }, ct);
        return status == null ? NotFound() : Ok(status);
    }

    /// <summary>Staff: the gateway page of one payment (kept from before P1-10; it is a checkout for the payment's order).</summary>
    [HttpPost("{paymentId:guid}/create-url")]
    [AuthorizePermission("payments:create")]
    public async Task<IActionResult> CreatePaymentUrl(Guid paymentId, CancellationToken ct)
    {
        var payment = await _payments.GetByIdAsync(paymentId, ct);
        return payment == null ? NotFound() : await Checkout(payment.OrderId, ct);
    }

    // ── What the gateways send back ─────────────────────────────────────────────

    /// <summary>
    /// The customer's browser returns from the gateway. The signature is verified (and the payment applied, idempotently), then the browser goes
    /// to the shop's result page. Carries no bearer token: the gateway's signature is the proof, so it is anonymous by design.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider, CancellationToken ct)
    {
        var result = await Process(provider, ct);
        var status = result.Outcome switch
        {
            NotificationOutcome.Paid => "paid",
            NotificationOutcome.Failed => "failed",
            _ => "invalid"
        };
        // A relative redirect: it stays on the host the customer is already on (never a host taken from the request).
        var target = result.OrderId is { } orderId ? $"/payment/result?orderId={orderId}&status={status}" : $"/payment/result?status={status}";
        return Redirect(target);
    }

    /// <summary>
    /// The gateway's own server-to-server notification (MoMo POST JSON, ZaloPay POST JSON, VNPay GET). Anonymous by design; the signature
    /// is the proof. The answer is in the shape each gateway expects.
    /// </summary>
    [AllowAnonymous]
    [AcceptVerbs("GET", "POST")]
    [Route("ipn/{provider}")]
    public async Task<IActionResult> Ipn(string provider, CancellationToken ct)
    {
        var result = await Process(provider, ct);
        var reply = result.Reply;
        return reply.Body == null ? StatusCode(reply.StatusCode) : StatusCode(reply.StatusCode, reply.Body);
    }

    // The two routes VNPay used before MoMo and ZaloPay existed.
    [AllowAnonymous]
    [HttpGet("callback")]
    public Task<IActionResult> LegacyCallback(CancellationToken ct) => Callback("VNPay", ct);

    [AllowAnonymous]
    [AcceptVerbs("GET", "POST")]
    [Route("ipn")]
    public Task<IActionResult> LegacyIpn(CancellationToken ct) => Ipn("VNPay", ct);

    private async Task<GatewayNotificationResult> Process(string provider, CancellationToken ct)
    {
        var query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var body = string.Empty;
        if (Request.ContentLength is > 0)
        {
            using var reader = new StreamReader(Request.Body);
            body = await reader.ReadToEndAsync(ct);
        }

        var result = await _notifications.ProcessAsync(provider, new GatewayCallback(Request.Method, query, body), ct);
        _logger.LogInformation("[{Provider}] notification {Outcome}", provider, result.Outcome);
        return result;
    }

    private Guid CallerId() => Guid.TryParse(_userContext.UserId, out var id) ? id : Guid.Empty;

    /// <summary>
    /// The public address gateways must use to reach this shop: <c>Payment:PublicBaseUrl</c> when set (a tunnel in development, the shop's
    /// domain behind a proxy), else the host the customer's request came in on.
    /// </summary>
    private string PublicBaseUrl()
    {
        var configured = _config["Payment:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');

        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
        return $"{scheme}://{host}";
    }
}
