using Asp.Versioning;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Authorization.ServiceAuth;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SHP.API.Controllers;

/// <summary>The delivery fee the customer pays (ROADMAP P1-11b / P1-11c): a preview for the storefront and the authoritative answer for the order service.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipping/fee")]
public class ShippingFeeController : ControllerBase
{
    private readonly ShippingFeeService _fees;

    public ShippingFeeController(ShippingFeeService fees) => _fees = fees;

    /// <summary>Preview at checkout (flat fee only: no province, no products). The order service recomputes the fee when the order is placed, so this number is never trusted.</summary>
    [HttpGet]
    [AuthorizeAnyAuthenticatedUser]
    public async Task<IActionResult> Preview([FromQuery] decimal merchandiseValue, CancellationToken ct) =>
        Ok(await _fees.QuoteAsync(merchandiseValue, ct));

    /// <summary>Preview at checkout with the delivery province and the cart: the shop's price list applies. Never trusted by the order.</summary>
    [HttpPost]
    [AuthorizeAnyAuthenticatedUser]
    public async Task<IActionResult> PreviewFor([FromBody] ShippingFeeRequest request, CancellationToken ct) =>
        Ok(await _fees.QuoteAsync(request, ct));
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

    [HttpPost]
    [AuthorizeInternalService("salesorder")]
    public async Task<IActionResult> Post([FromBody] ShippingFeeRequest request, CancellationToken ct) =>
        Ok(await _fees.QuoteAsync(request, ct));
}

/// <summary>The shop's delivery price list: parcels to these provinces in this weight band cost this much; the flat fee covers the rest.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipping/fee-rules")]
public class ShippingFeeRulesController : ControllerBase
{
    private readonly IShippingFeeRuleService _rules;

    public ShippingFeeRulesController(IShippingFeeRuleService rules) => _rules = rules;

    [HttpGet]
    [AuthorizePermission("shipping:view")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _rules.ListAsync(ct));

    [HttpPost]
    [AuthorizePermission("shipping:carriers:manage")]
    public async Task<IActionResult> Create([FromBody] SaveShippingFeeRuleRequest request, CancellationToken ct) =>
        Ok(await _rules.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission("shipping:carriers:manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveShippingFeeRuleRequest request, CancellationToken ct) =>
        Ok(await _rules.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission("shipping:carriers:manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _rules.DeleteAsync(id, ct);
        return NoContent();
    }
}
