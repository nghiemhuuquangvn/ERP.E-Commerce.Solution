using ERP.PRM.Application.Commands;
using ERP.PRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
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
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetActivePromotionsQuery(), ct));

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
    public async Task<IActionResult> GetApplicable([FromBody] GetApplicablePromotionsQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    [HttpPost("validate-coupon")]
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    [HttpPost("redeem-coupon")]
    public async Task<IActionResult> RedeemCoupon([FromBody] RedeemCouponCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));
}

[ApiController]
[Route("api/v1/loyalty")]
public class LoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;
    public LoyaltyController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetBalance(Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLoyaltyBalanceQuery { UserId = userId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{userId:guid}/earn")]
    public async Task<IActionResult> Earn(Guid userId, [FromBody] EarnLoyaltyPointsCommand command, CancellationToken ct)
    {
        command.UserId = userId;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{userId:guid}/redeem")]
    public async Task<IActionResult> Redeem(Guid userId, [FromBody] RedeemLoyaltyPointsCommand command, CancellationToken ct)
    {
        command.UserId = userId;
        return Ok(await _mediator.Send(command, ct));
    }
}
