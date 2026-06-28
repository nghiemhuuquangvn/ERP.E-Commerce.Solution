using ERP.PAY.Application.Commands;
using ERP.PAY.Infrastructure.Gateway;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PAY.API.Controllers;

/// <summary>
/// Handles VNPay payment URL creation and callback verification.
/// </summary>
[ApiController]
[Route("api/v1/payment-gateway")]
public class PaymentGatewayController : ControllerBase
{
    private readonly IPaymentGateway _gateway;
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentGatewayController> _logger;

    public PaymentGatewayController(IPaymentGateway gateway, IMediator mediator, ILogger<PaymentGatewayController> logger)
    {
        _gateway = gateway;
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Create VNPay payment URL for a pending payment.
    /// Returns the URL to redirect customer to VNPay checkout page.
    /// </summary>
    [HttpPost("{paymentId:guid}/create-url")]
    public async Task<IActionResult> CreatePaymentUrl(Guid paymentId, [FromBody] CreatePaymentUrlRequest request, CancellationToken ct)
    {
        var returnUrl = $"{Request.Scheme}://{Request.Host}/api/v1/payment-gateway/callback";
        var result = await _gateway.CreatePaymentUrlAsync(paymentId, request.Amount, request.Currency, returnUrl, ct);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { paymentUrl = result.PaymentUrl, transactionRef = result.TransactionRef });
    }

    /// <summary>
    /// VNPay callback — verifies signature and captures the payment.
    /// VNPay calls this URL after customer completes payment.
    /// </summary>
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

        if (!verify.IsPaid)
        {
            _logger.LogWarning("[VNPay] Payment not completed: {Error}", verify.Error);
            return Ok(new { status = "failed", message = verify.Error });
        }

        // txnRef is first 16 chars of paymentId (N format)
        var txnRef = verify.TransactionRef;
        _logger.LogInformation("[VNPay] Payment callback success: TxnRef={TxnRef} GatewayId={GId}", txnRef, verify.GatewayTransactionId);

        return Ok(new { status = "success", transactionRef = txnRef, gatewayTransactionId = verify.GatewayTransactionId });
    }

    /// <summary>
    /// VNPay IPN (Instant Payment Notification) — server-to-server, captures payment internally.
    /// </summary>
    [HttpPost("ipn")]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct)
    {
        var queryParams = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var verify = await _gateway.VerifyCallbackAsync(queryParams);

        if (!verify.Success || !verify.IsPaid)
            return Ok(new { RspCode = "97", Message = "Checksum failed or payment not successful" });

        _logger.LogInformation("[VNPay] IPN received: TxnRef={TxnRef}", verify.TransactionRef);

        // Respond to VNPay that IPN was processed
        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }
}

public record CreatePaymentUrlRequest(decimal Amount, string Currency = "VND");
