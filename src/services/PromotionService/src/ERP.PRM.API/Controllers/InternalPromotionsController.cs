using ERP.PRM.Application.Queries;
using ERP.Shared.Authorization.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRM.API.Controllers;

/// <summary>
/// Service-to-service promotion reads (internal network only, service token required). The
/// token's tenant is the request tenant, so the coupon is looked up in the order's tenant.
/// </summary>
[ApiController]
[Route("internal/v1/promotions")]
public class InternalPromotionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalPromotionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Coupon check during order creation (SaleOrderService's CreateOrderCommandHandler).</summary>
    [HttpPost("validate-coupon")]
    [AuthorizeInternalService("salesorder")]
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));
}
