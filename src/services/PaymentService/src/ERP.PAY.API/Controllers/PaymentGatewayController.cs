using Asp.Versioning;
using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Interfaces;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PAY.API.Controllers;

/// <summary>
/// Handles VNPay payment URL creation and callback verification.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payment-gateway")]
public class PaymentGatewayController : ControllerBase
{
    private readonly IPaymentGateway _gateway;
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentGatewayController> _logger;
    private readonly UserContext _userContext;

    public PaymentGatewayController(IPaymentGateway gateway, IMediator mediator, ILogger<PaymentGatewayController> logger, UserContext userContext)
    {
        _gateway = gateway;
        _mediator = mediator;
        _logger = logger;
        _userContext = userContext;
    }

    /// <summary>
    /// Create VNPay payment URL for a pending payment.
    /// Returns the URL to redirect customer to VNPay checkout page.
    /// Staff/service-initiated (no Storefront call site exists today — checkout only
    /// calls SaleOrderService's order-creation API), so gated the same as this
    /// service's other write actions rather than left open for an unauthenticated caller.
    /// </summary>
    [HttpPost("{paymentId:guid}/create-url")]
    public async Task<IActionResult> CreatePaymentUrl(Guid paymentId, [FromBody] CreatePaymentUrlRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:create"))
            return Forbid();

        var returnUrl = $"{Request.Scheme}://{Request.Host}/api/v1/payment-gateway/callback";
        var result = await _gateway.CreatePaymentUrlAsync(paymentId, request.Amount, request.Currency, returnUrl, ct);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        // Persist the reference VNPay will echo back on callback/IPN — previously
        // nothing stored this, so the webhook had no reliable way to identify which
        // Payment a callback belonged to.
        await _mediator.Send(new AssignTransactionRefCommand { PaymentId = paymentId, TransactionRef = result.TransactionRef }, ct);

        return Ok(new { paymentUrl = result.PaymentUrl, transactionRef = result.TransactionRef });
    }

    /// <summary>
    /// VNPay callback — verifies signature and captures the payment.
    /// VNPay calls this URL after customer completes payment.
    /// </summary>
    // Browser redirect from VNPay — carries no bearer token. Authenticity comes from the
    // VNPay signature verified below, so it must stay reachable under the FallbackPolicy.
    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> VnPayCallback(CancellationToken ct)
    {
        var queryParams = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());

        var verify = await _gateway.VerifyCallbackAsync(queryParams);

        if (!verify.Success)
        {
            _logger.LogWarning("[VNPay] Invalid callback signature");
            return BadRequest("Invalid payment signature");
        }

        var payment = await _mediator.Send(new ProcessGatewayCallbackCommand
        {
            TransactionRef = verify.TransactionRef,
            IsPaid = verify.IsPaid,
            GatewayTransactionId = verify.GatewayTransactionId,
            GatewayProvider = "VNPay",
            FailureReason = verify.Error
        }, ct);

        if (!verify.IsPaid)
        {
            _logger.LogWarning("[VNPay] Payment not completed: {Error}", verify.Error);
            return Ok(new { status = "failed", message = verify.Error });
        }

        _logger.LogInformation("[VNPay] Payment callback success: TxnRef={TxnRef} GatewayId={GId}", verify.TransactionRef, verify.GatewayTransactionId);

        return Ok(new { status = "success", transactionRef = verify.TransactionRef, gatewayTransactionId = verify.GatewayTransactionId, paymentStatus = payment.Status });
    }

    /// <summary>
    /// VNPay IPN (Instant Payment Notification) — server-to-server, captures payment internally.
    /// External webhook call from VNPay's servers, not a logged-in user or staff member — it
    /// cannot carry a staff/customer JWT bearer token in a real deployment, so a HasPermission
    /// check would break the webhook flow rather than secure it. Explicitly [AllowAnonymous]
    /// (rather than silently unprotected-by-omission) because the real security boundary here
    /// is VNPay's checksum, already verified below via IPaymentGateway.VerifyCallbackAsync
    /// (same signature-verification call the "callback" action above uses) — requests that
    /// fail checksum verification are rejected before ProcessGatewayCallbackCommand runs.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("ipn")]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct)
    {
        var queryParams = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var verify = await _gateway.VerifyCallbackAsync(queryParams);

        if (!verify.Success)
            return Ok(new { RspCode = "97", Message = "Checksum failed" });

        try
        {
            await _mediator.Send(new ProcessGatewayCallbackCommand
            {
                TransactionRef = verify.TransactionRef,
                IsPaid = verify.IsPaid,
                GatewayTransactionId = verify.GatewayTransactionId,
                GatewayProvider = "VNPay",
                FailureReason = verify.Error
            }, ct);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("[VNPay] IPN could not be applied: {Error}", ex.Message);
            return Ok(new { RspCode = "01", Message = "Order not found" });
        }

        _logger.LogInformation("[VNPay] IPN received: TxnRef={TxnRef}", verify.TransactionRef);

        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }
}

public record CreatePaymentUrlRequest(decimal Amount, string Currency = "VND");
