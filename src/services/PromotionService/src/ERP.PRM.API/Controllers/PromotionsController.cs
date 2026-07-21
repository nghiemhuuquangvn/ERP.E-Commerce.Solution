using ERP.PRM.Application.Commands;
using ERP.PRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRM.API.Controllers;

[ApiController]
[Route("api/v1/promotions")]
public class PromotionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public PromotionsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_View))
            return Forbid();
        return Ok(await _mediator.Send(new GetActivePromotionsQuery(), ct));
    }

    [HttpGet("all")]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        return Ok(await _mediator.Send(new GetAllPromotionsQuery(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_View))
            return Forbid();
        var result = await _mediator.Send(new GetPromotionByIdQuery { PromotionId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePromotionCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        var result = await _mediator.Send(new ActivatePromotionCommand { PromotionId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        var result = await _mediator.Send(new PausePromotionCommand { PromotionId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        var result = await _mediator.Send(new CancelPromotionCommand { PromotionId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/generate-coupons")]
    public async Task<IActionResult> GenerateCoupons(Guid id, [FromBody] GenerateBulkCouponsCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        command.PromotionId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("applicable")]
    public async Task<IActionResult> GetApplicable([FromBody] GetApplicablePromotionsQuery query, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_View))
            return Forbid();
        return Ok(await _mediator.Send(query, ct));
    }

    /// <summary>
    /// Genuinely public/customer-facing: the Storefront's cart page (the natural call
    /// site for coupon validation — see storefront src/router/index.ts, which gates
    /// only /checkout and /order-confirmation behind requiresAuth, NOT /cart) is
    /// reachable pre-login, so a customer must be able to check whether a coupon code
    /// is valid before authenticating. Read-only — no state change, no staff data
    /// exposed — so [AllowAnonymous] here is intentional, not an oversight.
    /// </summary>
    [HttpPost("validate-coupon")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    /// <summary>
    /// Unlike validate-coupon, this is a real state change (consumes/decrements the
    /// coupon), so it must not be reachable anonymously — that would let anyone burn
    /// out a limited-use coupon with no identity at all. It intentionally does NOT
    /// require a staff promotions:view/manage permission: the Storefront's checkout
    /// flow (which requires login — see CheckoutPage.vue using authStore.user.id)
    /// is the real caller, so any authenticated customer may redeem for themselves.
    /// </summary>
    [HttpPost("redeem-coupon")]
    public async Task<IActionResult> RedeemCoupon([FromBody] RedeemCouponCommand command, CancellationToken ct)
    {
        if (!_userContext.IsAuthenticated)
            return Unauthorized();
        return Ok(await _mediator.Send(command, ct));
    }
}

/// <summary>
/// Most severe finding of the audit that added these checks: Earn/Redeem previously
/// trusted a caller-supplied userId with NO check at all — any (even unauthenticated)
/// caller could mint or spend loyalty points for ANY other user by just changing the
/// route parameter. No Storefront/mobile-customer/mobile-staff app in this repo calls
/// any Loyalty endpoint today (verified by search — GetBalance/Earn/Redeem are all
/// dead/unreachable from current frontend code), so there is no evidence of a
/// "customer redeems their own points at checkout" self-service flow to preserve.
/// Given that, and that Promotions_Manage is already staff-only per RoleDefinitions,
/// these are treated as a staff-only action (e.g. customer service manually adjusting
/// a customer's points) — the permission check alone is the fix here, matching the
/// same reasoning the plan calls out for this exact case. If a genuine customer
/// self-service flow is added later, it will need a separate, additional
/// ownership-or-elevated-permission path (mirroring PreferencesController), not a
/// relaxation of this check.
/// </summary>
[ApiController]
[Route("api/v1/loyalty")]
public class LoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public LoyaltyController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetBalance(Guid userId, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_View))
            return Forbid();

        var result = await _mediator.Send(new GetLoyaltyBalanceQuery { UserId = userId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{userId:guid}/earn")]
    public async Task<IActionResult> Earn(Guid userId, [FromBody] EarnLoyaltyPointsCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_Manage))
            return Forbid();

        command.UserId = userId;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{userId:guid}/redeem")]
    public async Task<IActionResult> Redeem(Guid userId, [FromBody] RedeemLoyaltyPointsCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_Manage))
            return Forbid();

        command.UserId = userId;
        return Ok(await _mediator.Send(command, ct));
    }
}
