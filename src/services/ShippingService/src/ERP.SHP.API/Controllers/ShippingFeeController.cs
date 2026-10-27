using Asp.Versioning;
using ERP.SHP.Application.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Authorization.ServiceAuth;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SHP.API.Controllers;

/// <summary>The delivery fee the customer pays (ROADMAP P1-11b): a preview for the storefront and the authoritative answer for the order service.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipping/fee")]
public class ShippingFeeController : ControllerBase
{
    private readonly ShippingFeeService _fees;

    public ShippingFeeController(ShippingFeeService fees) => _fees = fees;

    /// <summary>Preview at checkout. The order service recomputes the fee when the order is placed, so this number is never trusted.</summary>
    [HttpGet]
    [AuthorizeAnyAuthenticatedUser]
    public async Task<IActionResult> Preview([FromQuery] decimal merchandiseValue, CancellationToken ct) =>
        Ok(await _fees.QuoteAsync(merchandiseValue, ct));
}

/// <summary>Service-to-service: SaleOrderService asks for the fee when it creates an order (service token, the order's tenant).</summary>
[ApiController]
[Route("internal/v1/shipping-fee")]
public class InternalShippingFeeController : ControllerBase
{
    private readonly ShippingFeeService _fees;

    public InternalShippingFeeController(ShippingFeeService fees) => _fees = fees;

    [HttpGet]
    [AuthorizeInternalService("salesorder")]
    public async Task<IActionResult> Get([FromQuery] decimal merchandiseValue, CancellationToken ct) =>
        Ok(await _fees.QuoteAsync(merchandiseValue, ct));
}
