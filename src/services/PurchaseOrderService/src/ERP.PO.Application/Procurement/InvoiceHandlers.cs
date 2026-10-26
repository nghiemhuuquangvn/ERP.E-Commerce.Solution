using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Enums;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.PO.Application.Procurement;

// ─── DTOs ─────────────────────────────────────────────────────────────────

public record SupplierInvoiceLineDto(string ProductCode, decimal Quantity, decimal UnitPrice);
public record InvoiceMatchLineDto(
    string ProductCode, decimal Ordered, decimal Received, decimal Returned, decimal PreviouslyInvoiced, decimal Invoiced,
    decimal OrderPrice, decimal InvoicedPrice, decimal PriceVariancePercent, decimal QuantityVariance, string Verdict);
public record SupplierInvoiceDto(
    Guid Id, string InvoiceNumber, Guid SupplierId, string SupplierName, Guid PurchaseOrderId, string PONumber, DateTime InvoiceDate, DateTime DueDate,
    string CurrencyCode, decimal NetAmount, decimal TaxAmount, decimal TotalAmount, string Status, string? HoldReasons, string? ApprovalReason,
    string? RejectionReason, string? ApprovedBy, DateTime? ApprovedAt, decimal ReceivedValue, DateTime CreatedAt,
    List<SupplierInvoiceLineDto> Lines, List<InvoiceMatchLineDto> MatchLines);
public record SupplierInvoiceSummaryDto(
    Guid Id, string InvoiceNumber, string SupplierName, string PONumber, DateTime InvoiceDate, DateTime DueDate, string CurrencyCode, decimal TotalAmount, string Status, string? HoldReasons);
public record PurchasingSettingsDto(decimal QuantityTolerancePercent, decimal PriceTolerancePercent, bool AutoApproveMatched);

/// <summary>Per order line: what was ordered, received, rejected, returned and invoiced, so a screen can prefill an invoice or limit a return.</summary>
public record OrderProcurementLineDto(string ProductCode, string ProductName, decimal Ordered, decimal Received, decimal Rejected, decimal Returned, decimal Invoiced, decimal Returnable, decimal Billable, decimal UnitPrice);
public record OrderProcurementDto(Guid PurchaseOrderId, string PONumber, Guid SupplierId, string CurrencyCode, List<OrderProcurementLineDto> Lines);

internal static class InvoiceMapper
{
    public static SupplierInvoiceDto ToDto(SupplierInvoice i, string supplierName) => new(
        i.Id, i.InvoiceNumber, i.SupplierId, supplierName, i.PurchaseOrderId, i.PONumber, i.InvoiceDate, i.DueDate, i.CurrencyCode, i.NetAmount, i.TaxAmount,
        i.TotalAmount, i.Status.ToString(), i.HoldReasons, i.ApprovalReason, i.RejectionReason, i.ApprovedBy, i.ApprovedAt, i.ReceivedValue, i.CreatedAt,
        i.Lines.Select(l => new SupplierInvoiceLineDto(l.ProductCode, l.Quantity, l.UnitPrice)).ToList(),
        i.MatchLines.Select(m => new InvoiceMatchLineDto(m.ProductCode, m.Ordered, m.Received, m.Returned, m.PreviouslyInvoiced, m.Invoiced, m.OrderPrice,
            m.InvoicedPrice, m.PriceVariancePercent, m.QuantityVariance, m.Verdict)).ToList());

    public static string CurrencyOf(PurchaseOrder order) => order.Items.FirstOrDefault()?.UnitPrice.Currency ?? order.TotalAmount.Currency;

    /// <summary>Runs the stored-invoice match against the order, the receipts, the returns and the other invoices of the same order.</summary>
    public static async Task<InvoiceMatchOutcome> RunMatchAsync(SupplierInvoice invoice, PurchaseOrder order, IProcurementStore store, Guid tenantId, CancellationToken ct)
    {
        var settings = await store.GetSettingsAsync(tenantId, ct);
        var invoiced = await store.InvoicedQuantitiesAsync(order.Id, invoice.Id, ct);
        var returned = await store.ReturnedQuantitiesAsync(order.Id, ct);

        var orderLines = order.Items.Select(i => new InvoiceMatchOrderLine(
            i.ProductCode, i.Quantity, i.ReceivedQuantity, returned.GetValueOrDefault(i.ProductCode), invoiced.GetValueOrDefault(i.ProductCode), i.UnitPrice.Amount)).ToList();
        var inputLines = invoice.Lines.Select(l => new InvoiceMatchInputLine(l.ProductCode, l.Quantity, l.UnitPrice)).ToList();

        var outcome = InvoiceMatcher.Match(orderLines, inputLines, new InvoiceMatchTolerance(settings.QuantityTolerancePercent, settings.PriceTolerancePercent));
        var poCurrency = CurrencyOf(order);
        if (!string.Equals(poCurrency, invoice.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            outcome = outcome with { IsMatched = false, Reasons = outcome.Reasons.Append($"the invoice is in {invoice.CurrencyCode} but the order is in {poCurrency}").ToList() };
        return outcome;
    }
}

// ─── commands / queries ───────────────────────────────────────────────────

public record SupplierInvoiceLineInput(string ProductCode, decimal Quantity, decimal UnitPrice);

public record RecordSupplierInvoiceCommand(
    Guid PurchaseOrderId, string InvoiceNumber, DateTime InvoiceDate, DateTime? DueDate, string? CurrencyCode, decimal TaxAmount,
    List<SupplierInvoiceLineInput> Lines) : IRequest<SupplierInvoiceDto>;
public record MatchSupplierInvoiceCommand(Guid InvoiceId) : IRequest<SupplierInvoiceDto>;
public record ApproveSupplierInvoiceCommand(Guid InvoiceId, string? Reason, bool CanApproveVariance) : IRequest<SupplierInvoiceDto>;
public record RejectSupplierInvoiceCommand(Guid InvoiceId, string Reason) : IRequest<SupplierInvoiceDto>;
public record GetSupplierInvoiceQuery(Guid InvoiceId) : IRequest<SupplierInvoiceDto?>;
public record ListSupplierInvoicesQuery(string? Status, Guid? PurchaseOrderId) : IRequest<List<SupplierInvoiceSummaryDto>>;
public record GetOrderProcurementQuery(Guid PurchaseOrderId) : IRequest<OrderProcurementDto?>;
public record GetPurchasingSettingsQuery : IRequest<PurchasingSettingsDto>;
public record UpdatePurchasingSettingsCommand(decimal QuantityTolerancePercent, decimal PriceTolerancePercent, bool AutoApproveMatched) : IRequest<PurchasingSettingsDto>;

// ─── handlers ─────────────────────────────────────────────────────────────

public class RecordSupplierInvoiceCommandHandler : IRequestHandler<RecordSupplierInvoiceCommand, SupplierInvoiceDto>
{
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public RecordSupplierInvoiceCommandHandler(IProcurementStore store, IUserContextService user) { _store = store; _user = user; }

    public async Task<SupplierInvoiceDto> Handle(RecordSupplierInvoiceCommand request, CancellationToken ct)
    {
        var user = _user.GetCurrentUser();
        var tenantId = new Guid(user.TenantId);

        var order = await _store.GetOrderAsync(request.PurchaseOrderId, ct) ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");
        var supplier = await _store.GetSupplierAsync(order.SupplierId, ct);
        if (order.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Rejected)
            throw new BusinessRuleException("Purchasing", $"A purchase order that is {order.Status} cannot be invoiced.");
        if (await _store.InvoiceNumberExistsAsync(order.SupplierId, request.InvoiceNumber?.Trim() ?? string.Empty, ct))
            throw new BusinessRuleException("Purchasing", $"Invoice {request.InvoiceNumber} of this supplier is already recorded.");

        var dueDate = request.DueDate ?? request.InvoiceDate.AddDays(supplier?.PaymentTermDays ?? 30);
        var invoice = Rules.Run(() =>
        {
            var i = SupplierInvoice.Record(request.InvoiceNumber ?? string.Empty, order.SupplierId, order.Id, order.PONumber, tenantId, request.InvoiceDate, dueDate,
                request.CurrencyCode ?? InvoiceMapper.CurrencyOf(order), request.TaxAmount,
                (request.Lines ?? new()).Select(l => (l.ProductCode, l.Quantity, l.UnitPrice)));
            i.CreatedBy = user.UserId;
            return i;
        });

        _store.Add(invoice);
        invoice.ApplyMatch(await InvoiceMapper.RunMatchAsync(invoice, order, _store, tenantId, ct));

        // A tenant may let a matched invoice go through without a person.
        var settings = await _store.GetSettingsAsync(tenantId, ct);
        if (settings.AutoApproveMatched && invoice.Status == SupplierInvoiceStatus.Matched)
            invoice.Approve("system (auto-approve)", overrideVariance: false, reason: null);

        await _store.SaveChangesAsync(ct);
        return InvoiceMapper.ToDto(invoice, supplier?.Name ?? string.Empty);
    }
}

public class MatchSupplierInvoiceCommandHandler : IRequestHandler<MatchSupplierInvoiceCommand, SupplierInvoiceDto>
{
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public MatchSupplierInvoiceCommandHandler(IProcurementStore store, IUserContextService user) { _store = store; _user = user; }

    public async Task<SupplierInvoiceDto> Handle(MatchSupplierInvoiceCommand request, CancellationToken ct)
    {
        var tenantId = new Guid(_user.GetCurrentUser().TenantId);
        var invoice = await _store.GetInvoiceAsync(request.InvoiceId, ct) ?? throw new KeyNotFoundException($"Supplier invoice {request.InvoiceId} not found");
        var order = await _store.GetOrderAsync(invoice.PurchaseOrderId, ct) ?? throw new KeyNotFoundException("The purchase order of the invoice was not found");

        var outcome = await InvoiceMapper.RunMatchAsync(invoice, order, _store, tenantId, ct);
        Rules.Run(() => invoice.ApplyMatch(outcome));

        var settings = await _store.GetSettingsAsync(tenantId, ct);
        if (settings.AutoApproveMatched && invoice.Status == SupplierInvoiceStatus.Matched)
            invoice.Approve("system (auto-approve)", overrideVariance: false, reason: null);

        await _store.SaveChangesAsync(ct);
        return InvoiceMapper.ToDto(invoice, (await _store.GetSupplierAsync(invoice.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class ApproveSupplierInvoiceCommandHandler : IRequestHandler<ApproveSupplierInvoiceCommand, SupplierInvoiceDto>
{
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public ApproveSupplierInvoiceCommandHandler(IProcurementStore store, IUserContextService user) { _store = store; _user = user; }

    public async Task<SupplierInvoiceDto> Handle(ApproveSupplierInvoiceCommand request, CancellationToken ct)
    {
        var user = _user.GetCurrentUser();
        var invoice = await _store.GetInvoiceAsync(request.InvoiceId, ct) ?? throw new KeyNotFoundException($"Supplier invoice {request.InvoiceId} not found");

        Rules.Run(() => invoice.Approve(user.UserId, request.CanApproveVariance, request.Reason));
        invoice.UpdatedBy = user.UserId;
        await _store.SaveChangesAsync(ct);
        return InvoiceMapper.ToDto(invoice, (await _store.GetSupplierAsync(invoice.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class RejectSupplierInvoiceCommandHandler : IRequestHandler<RejectSupplierInvoiceCommand, SupplierInvoiceDto>
{
    private readonly IProcurementStore _store;
    public RejectSupplierInvoiceCommandHandler(IProcurementStore store) => _store = store;

    public async Task<SupplierInvoiceDto> Handle(RejectSupplierInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _store.GetInvoiceAsync(request.InvoiceId, ct) ?? throw new KeyNotFoundException($"Supplier invoice {request.InvoiceId} not found");
        Rules.Run(() => invoice.Reject(request.Reason));
        await _store.SaveChangesAsync(ct);
        return InvoiceMapper.ToDto(invoice, (await _store.GetSupplierAsync(invoice.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class GetSupplierInvoiceQueryHandler : IRequestHandler<GetSupplierInvoiceQuery, SupplierInvoiceDto?>
{
    private readonly IProcurementStore _store;
    public GetSupplierInvoiceQueryHandler(IProcurementStore store) => _store = store;

    public async Task<SupplierInvoiceDto?> Handle(GetSupplierInvoiceQuery request, CancellationToken ct)
    {
        var invoice = await _store.GetInvoiceAsync(request.InvoiceId, ct);
        return invoice == null ? null : InvoiceMapper.ToDto(invoice, (await _store.GetSupplierAsync(invoice.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class ListSupplierInvoicesQueryHandler : IRequestHandler<ListSupplierInvoicesQuery, List<SupplierInvoiceSummaryDto>>
{
    private readonly IProcurementStore _store;
    public ListSupplierInvoicesQueryHandler(IProcurementStore store) => _store = store;

    public async Task<List<SupplierInvoiceSummaryDto>> Handle(ListSupplierInvoicesQuery request, CancellationToken ct)
    {
        SupplierInvoiceStatus? status = Enum.TryParse<SupplierInvoiceStatus>(request.Status, true, out var s) ? s : null;
        var list = await _store.ListInvoicesAsync(status, request.PurchaseOrderId, ct);
        var names = await _store.GetSupplierNamesAsync(list.Select(i => i.SupplierId), ct);
        return list.Select(i => new SupplierInvoiceSummaryDto(i.Id, i.InvoiceNumber, names.GetValueOrDefault(i.SupplierId, string.Empty), i.PONumber,
            i.InvoiceDate, i.DueDate, i.CurrencyCode, i.TotalAmount, i.Status.ToString(), i.HoldReasons)).ToList();
    }
}

public class GetOrderProcurementQueryHandler : IRequestHandler<GetOrderProcurementQuery, OrderProcurementDto?>
{
    private readonly IProcurementStore _store;
    public GetOrderProcurementQueryHandler(IProcurementStore store) => _store = store;

    public async Task<OrderProcurementDto?> Handle(GetOrderProcurementQuery request, CancellationToken ct)
    {
        var order = await _store.GetOrderAsync(request.PurchaseOrderId, ct);
        if (order == null) return null;

        var invoiced = await _store.InvoicedQuantitiesAsync(order.Id, null, ct);
        var returned = await _store.ReturnedQuantitiesAsync(order.Id, ct);
        var lines = order.Items.Select(i =>
        {
            var ret = returned.GetValueOrDefault(i.ProductCode);
            var inv = invoiced.GetValueOrDefault(i.ProductCode);
            return new OrderProcurementLineDto(i.ProductCode, i.ProductName, i.Quantity, i.ReceivedQuantity, i.RejectedQuantity, ret, inv,
                Math.Max(0m, i.ReceivedQuantity - ret), Math.Max(0m, i.ReceivedQuantity - ret - inv), i.UnitPrice.Amount);
        }).ToList();
        return new OrderProcurementDto(order.Id, order.PONumber, order.SupplierId, InvoiceMapper.CurrencyOf(order), lines);
    }
}

public class GetPurchasingSettingsQueryHandler : IRequestHandler<GetPurchasingSettingsQuery, PurchasingSettingsDto>
{
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public GetPurchasingSettingsQueryHandler(IProcurementStore store, IUserContextService user) { _store = store; _user = user; }

    public async Task<PurchasingSettingsDto> Handle(GetPurchasingSettingsQuery request, CancellationToken ct)
    {
        var s = await _store.GetSettingsAsync(new Guid(_user.GetCurrentUser().TenantId), ct);
        return new PurchasingSettingsDto(s.QuantityTolerancePercent, s.PriceTolerancePercent, s.AutoApproveMatched);
    }
}

public class UpdatePurchasingSettingsCommandHandler : IRequestHandler<UpdatePurchasingSettingsCommand, PurchasingSettingsDto>
{
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public UpdatePurchasingSettingsCommandHandler(IProcurementStore store, IUserContextService user) { _store = store; _user = user; }

    public async Task<PurchasingSettingsDto> Handle(UpdatePurchasingSettingsCommand request, CancellationToken ct)
    {
        var s = await _store.GetSettingsAsync(new Guid(_user.GetCurrentUser().TenantId), ct);
        Rules.Run(() => s.Update(request.QuantityTolerancePercent, request.PriceTolerancePercent, request.AutoApproveMatched));
        await _store.SaveChangesAsync(ct);
        return new PurchasingSettingsDto(s.QuantityTolerancePercent, s.PriceTolerancePercent, s.AutoApproveMatched);
    }
}
