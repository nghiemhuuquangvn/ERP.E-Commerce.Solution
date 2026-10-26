using Asp.Versioning;
using ERP.PO.Application.Procurement;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PO.Api.Controllers;

// RFQ and supplier quotations, supplier invoices with the three-way match, returns to vendor, the match settings (ROADMAP P1-8).
// Every action declares its permission with an attribute.

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/rfqs")]
public class RfqsController : ControllerBase
{
    private readonly IMediator _mediator;
    public RfqsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Rfq_Read)]
    public async Task<IActionResult> List([FromQuery] string? status = null) => Ok(await _mediator.Send(new ListRfqsQuery(status)));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Rfq_Read)]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _mediator.Send(new GetRfqQuery(id));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/comparison")]
    [AuthorizePermission(PermissionConstants.Rfq_Read)]
    public async Task<IActionResult> Compare(Guid id)
    {
        var result = await _mediator.Send(new CompareRfqQuery(id));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Rfq_Manage)]
    public async Task<IActionResult> Create([FromBody] CreateRfqCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/send")]
    [AuthorizePermission(PermissionConstants.Rfq_Manage)]
    public async Task<IActionResult> Send(Guid id) => Ok(await _mediator.Send(new SendRfqCommand(id)));

    [HttpPost("{id:guid}/cancel")]
    [AuthorizePermission(PermissionConstants.Rfq_Manage)]
    public async Task<IActionResult> Cancel(Guid id) => Ok(await _mediator.Send(new CancelRfqCommand(id)));

    [HttpPost("{id:guid}/quotations")]
    [AuthorizePermission(PermissionConstants.Rfq_Manage)]
    public async Task<IActionResult> RecordQuotation(Guid id, [FromBody] RecordQuotationRequest body) =>
        Ok(await _mediator.Send(new RecordQuotationCommand(id, body.SupplierId, body.QuoteNumber, body.ValidUntil, body.CurrencyCode, body.LeadTimeDays, body.Notes, body.UnitPrices)));

    [HttpPost("{id:guid}/suppliers/{supplierId:guid}/decline")]
    [AuthorizePermission(PermissionConstants.Rfq_Manage)]
    public async Task<IActionResult> Decline(Guid id, Guid supplierId) => Ok(await _mediator.Send(new DeclineRfqCommand(id, supplierId)));

    /// <summary>Award the RFQ to a quoted supplier: the RFQ closes and a Draft purchase order is created at the quoted prices.</summary>
    [HttpPost("{id:guid}/award")]
    [AuthorizePermission(PermissionConstants.Rfq_Manage)]
    public async Task<IActionResult> Award(Guid id, [FromBody] AwardRfqRequest body) => Ok(await _mediator.Send(new AwardRfqCommand(id, body.SupplierId)));
}

public record RecordQuotationRequest(Guid SupplierId, string QuoteNumber, DateTime ValidUntil, string CurrencyCode, int LeadTimeDays, string? Notes, Dictionary<Guid, decimal> UnitPrices);
public record AwardRfqRequest(Guid SupplierId);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/supplier-invoices")]
public class SupplierInvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public SupplierInvoicesController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    [AuthorizePermission(PermissionConstants.SupplierInvoice_Read)]
    public async Task<IActionResult> List([FromQuery] string? status = null, [FromQuery] Guid? purchaseOrderId = null) =>
        Ok(await _mediator.Send(new ListSupplierInvoicesQuery(status, purchaseOrderId)));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.SupplierInvoice_Read)]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _mediator.Send(new GetSupplierInvoiceQuery(id));
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Record the supplier's invoice for an order and match it at once (three-way: order, receipts, invoice).</summary>
    [HttpPost]
    [AuthorizePermission(PermissionConstants.SupplierInvoice_Manage)]
    public async Task<IActionResult> Record([FromBody] RecordSupplierInvoiceCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Match again (after a receipt, a return or a change of the tolerances).</summary>
    [HttpPost("{id:guid}/match")]
    [AuthorizePermission(PermissionConstants.SupplierInvoice_Manage)]
    public async Task<IActionResult> Match(Guid id) => Ok(await _mediator.Send(new MatchSupplierInvoiceCommand(id)));

    /// <summary>Approve for payment. A matched invoice needs supplier_invoice:approve; one on hold also needs supplier_invoice:approve_variance and a reason.</summary>
    [HttpPost("{id:guid}/approve")]
    [AuthorizePermission(PermissionConstants.SupplierInvoice_Approve)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveInvoiceRequest? body) =>
        Ok(await _mediator.Send(new ApproveSupplierInvoiceCommand(id, body?.Reason, _userContext.HasPermission(PermissionConstants.SupplierInvoice_ApproveVariance))));

    [HttpPost("{id:guid}/reject")]
    [AuthorizePermission(PermissionConstants.SupplierInvoice_Manage)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectInvoiceRequest body) => Ok(await _mediator.Send(new RejectSupplierInvoiceCommand(id, body.Reason)));
}

public record ApproveInvoiceRequest(string? Reason);
public record RejectInvoiceRequest(string Reason);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vendor-returns")]
public class VendorReturnsController : ControllerBase
{
    private readonly IMediator _mediator;
    public VendorReturnsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [AuthorizePermission(PermissionConstants.VendorReturn_Read)]
    public async Task<IActionResult> List([FromQuery] Guid? purchaseOrderId = null, [FromQuery] string? status = null) =>
        Ok(await _mediator.Send(new ListVendorReturnsQuery(purchaseOrderId, status)));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.VendorReturn_Read)]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _mediator.Send(new GetVendorReturnQuery(id));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [AuthorizePermission(PermissionConstants.VendorReturn_Manage)]
    public async Task<IActionResult> Create([FromBody] CreateVendorReturnCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/ship")]
    [AuthorizePermission(PermissionConstants.VendorReturn_Manage)]
    public async Task<IActionResult> Ship(Guid id) => Ok(await _mediator.Send(new ShipVendorReturnCommand(id)));

    [HttpPost("{id:guid}/cancel")]
    [AuthorizePermission(PermissionConstants.VendorReturn_Manage)]
    public async Task<IActionResult> Cancel(Guid id) => Ok(await _mediator.Send(new CancelVendorReturnCommand(id)));

    [HttpPost("{id:guid}/credit")]
    [AuthorizePermission(PermissionConstants.VendorReturn_Manage)]
    public async Task<IActionResult> Credit(Guid id, [FromBody] RecordCreditRequest body) =>
        Ok(await _mediator.Send(new RecordVendorCreditCommand(id, body.CreditNoteNumber, body.CreditAmount)));
}

public record RecordCreditRequest(string CreditNoteNumber, decimal CreditAmount);

[ApiController]
[ApiVersion("1.0")]
public class PurchasingProcurementController : ControllerBase
{
    private readonly IMediator _mediator;
    public PurchasingProcurementController(IMediator mediator) => _mediator = mediator;

    /// <summary>Per line of an order: ordered, received, rejected, returned, invoiced, what can still be returned and billed.</summary>
    [HttpGet("api/v{version:apiVersion}/purchase-orders/{id:guid}/procurement")]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Read)]
    public async Task<IActionResult> OrderProcurement(Guid id)
    {
        var result = await _mediator.Send(new GetOrderProcurementQuery(id));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("api/v{version:apiVersion}/purchasing-settings")]
    [AuthorizePermission(PermissionConstants.SupplierInvoice_Read)]
    public async Task<IActionResult> GetSettings() => Ok(await _mediator.Send(new GetPurchasingSettingsQuery()));

    [HttpPut("api/v{version:apiVersion}/purchasing-settings")]
    [AuthorizePermission(PermissionConstants.PurchasingSettings_Manage)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdatePurchasingSettingsCommand command) => Ok(await _mediator.Send(command));
}
