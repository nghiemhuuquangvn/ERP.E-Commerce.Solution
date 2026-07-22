using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Application.Features.Suppliers.Queries;
using ERP.Shared.Authorization;

namespace ERP.PO.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SuppliersController> _logger;
    private readonly IUserContextService _userContextService;

    public SuppliersController(
        IMediator mediator,
        ILogger<SuppliersController> logger,
        IUserContextService userContextService)
    {
        _mediator = mediator;
        _logger = logger;
        _userContextService = userContextService;
    }

    /// <summary>
    /// Create a new supplier
    /// Yêu cầu quyền: supplier:create
    /// Roles: Admin, Manager
    /// </summary>
    /// <param name="request">Supplier creation details</param>
    /// <returns>Created supplier</returns>
    [HttpPost]
    [AuthorizePermission(PermissionConstants.Supplier_Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierDto request)
    {
        var user = _userContextService.GetCurrentUser();
        _logger.LogInformation("User {UserId} from tenant {TenantId} creating supplier {Code}",
            user.UserId, user.TenantId, request.Code);

        var command = new CreateSupplierCommand
        {
            Code = request.Code,
            Name = request.Name,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address
        };

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetSupplierById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get supplier by ID
    /// Yêu cầu quyền: supplier:read
    /// </summary>
    /// <param name="id">Supplier ID</param>
    /// <returns>Supplier details</returns>
    [HttpGet("{id}")]
    [AuthorizePermission(PermissionConstants.Supplier_Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSupplierById(Guid id)
    {
        _logger.LogInformation("Fetching supplier {SupplierId}", id);

        var query = new GetSupplierByIdQuery { SupplierId = id };
        var result = await _mediator.Send(query);

        if (result == null)
        {
            _logger.LogWarning("Supplier {SupplierId} not found", id);
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get all suppliers with optional filtering
    /// Yêu cầu quyền: supplier:read
    /// </summary>
    /// <param name="isActive">Filter by active status (optional)</param>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    /// <returns>List of suppliers</returns>
    [HttpGet]
    [AuthorizePermission(PermissionConstants.Supplier_Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] bool? isActive = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        _logger.LogInformation("Fetching suppliers - IsActive: {IsActive}, Page: {PageNumber}, PageSize: {PageSize}",
            isActive, pageNumber, pageSize);

        var query = new GetSuppliersQuery
        {
            IsActive = isActive,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Update supplier details
    /// Yêu cầu quyền: supplier:update
    /// Roles: Admin, Manager
    /// </summary>
    /// <param name="id">Supplier ID</param>
    /// <param name="request">Supplier update details</param>
    /// <returns>Updated supplier</returns>
    [HttpPut("{id}")]
    [AuthorizePermission(PermissionConstants.Supplier_Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] UpdateSupplierDto request)
    {
        var user = _userContextService.GetCurrentUser();
        _logger.LogInformation("User {UserId} from tenant {TenantId} updating supplier {SupplierId}",
            user.UserId, user.TenantId, id);

        var command = new UpdateSupplierCommand
        {
            SupplierId = id,
            Name = request.Name,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            UpdatedBy = user.UserId
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a supplier
    /// Yêu cầu quyền: supplier:deactivate
    /// Roles: Admin
    /// </summary>
    /// <param name="id">Supplier ID</param>
    /// <returns>No content</returns>
    [HttpPost("{id}/deactivate")]
    [AuthorizePermission(PermissionConstants.Supplier_Deactivate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateSupplier(Guid id)
    {
        var user = _userContextService.GetCurrentUser();
        _logger.LogInformation("User {UserId} from tenant {TenantId} deactivating supplier {SupplierId}",
            user.UserId, user.TenantId, id);

        var command = new DeactivateSupplierCommand
        {
            SupplierId = id,
            DeactivatedBy = user.UserId
        };

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Rate a supplier (1-5) with an optional comment
    /// Yêu cầu quyền: supplier:rate
    /// </summary>
    [HttpPost("{id}/ratings")]
    [AuthorizePermission(PermissionConstants.Supplier_Rate)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RateSupplier(Guid id, [FromBody] RateSupplierRequestDto request)
    {
        var command = new RateSupplierCommand { SupplierId = id, Rating = request.Rating, Comment = request.Comment };
        var ratingId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetSupplierRatings), new { id }, new { id = ratingId });
    }

    /// <summary>
    /// Get a supplier's ratings and average score
    /// Yêu cầu quyền: supplier:read
    /// </summary>
    [HttpGet("{id}/ratings")]
    [AuthorizePermission(PermissionConstants.Supplier_Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierRatings(Guid id)
    {
        var result = await _mediator.Send(new GetSupplierRatingsQuery { SupplierId = id });
        return Ok(result);
    }
}

public class RateSupplierRequestDto
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
