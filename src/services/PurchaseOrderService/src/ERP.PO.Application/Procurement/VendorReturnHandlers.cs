using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.PO.Application.Procurement;

public record VendorReturnLineDto(string ProductCode, string ProductName, decimal Quantity, decimal UnitPrice);
public record VendorReturnDto(
    Guid Id, string ReturnNumber, Guid PurchaseOrderId, string PONumber, Guid SupplierId, string SupplierName, string CurrencyCode, string Reason, string Status,
    decimal Value, DateTime? ShippedAt, string? CreditNoteNumber, decimal CreditAmount, DateTime? CreditedAt, DateTime CreatedAt, List<VendorReturnLineDto> Lines);
public record VendorReturnSummaryDto(Guid Id, string ReturnNumber, string PONumber, string SupplierName, string CurrencyCode, string Status, decimal Value, DateTime CreatedAt);

public record VendorReturnLineInput(string ProductCode, decimal Quantity);
public record CreateVendorReturnCommand(Guid PurchaseOrderId, string Reason, List<VendorReturnLineInput> Lines) : IRequest<VendorReturnDto>;
public record ShipVendorReturnCommand(Guid ReturnId) : IRequest<VendorReturnDto>;
public record CancelVendorReturnCommand(Guid ReturnId) : IRequest<VendorReturnDto>;
public record RecordVendorCreditCommand(Guid ReturnId, string CreditNoteNumber, decimal CreditAmount) : IRequest<VendorReturnDto>;
public record GetVendorReturnQuery(Guid ReturnId) : IRequest<VendorReturnDto?>;
public record ListVendorReturnsQuery(Guid? PurchaseOrderId, string? Status) : IRequest<List<VendorReturnSummaryDto>>;

internal static class VendorReturnMapper
{
    public static VendorReturnDto ToDto(VendorReturn r, string supplierName) => new(
        r.Id, r.ReturnNumber, r.PurchaseOrderId, r.PONumber, r.SupplierId, supplierName, r.CurrencyCode, r.Reason, r.Status.ToString(), r.Value,
        r.ShippedAt, r.CreditNoteNumber, r.CreditAmount, r.CreditedAt, r.CreatedAt,
        r.Lines.Select(l => new VendorReturnLineDto(l.ProductCode, l.ProductName, l.Quantity, l.UnitPrice)).ToList());
}

public class CreateVendorReturnCommandHandler : IRequestHandler<CreateVendorReturnCommand, VendorReturnDto>
{
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public CreateVendorReturnCommandHandler(IProcurementStore store, IUserContextService user) { _store = store; _user = user; }

    public async Task<VendorReturnDto> Handle(CreateVendorReturnCommand request, CancellationToken ct)
    {
        var user = _user.GetCurrentUser();
        var tenantId = new Guid(user.TenantId);
        var order = await _store.GetOrderAsync(request.PurchaseOrderId, ct) ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        // returnable = accepted quantity received - what earlier (non-cancelled) returns already take back
        var already = await _store.ReturnedQuantitiesAsync(order.Id, ct);
        var returnable = order.Items.ToDictionary(i => i.ProductCode, i => Math.Max(0m, i.ReceivedQuantity - already.GetValueOrDefault(i.ProductCode)), StringComparer.OrdinalIgnoreCase);

        var lines = new List<(string, string, decimal, decimal)>();
        foreach (var l in request.Lines ?? new())
        {
            var item = order.Items.FirstOrDefault(i => i.ProductCode.Equals(l.ProductCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException("Purchasing", $"{l.ProductCode} is not on purchase order {order.PONumber}.");
            lines.Add((item.ProductCode, item.ProductName, l.Quantity, item.UnitPrice.Amount));
        }

        var number = $"VR-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var vendorReturn = Rules.Run(() =>
        {
            var r = VendorReturn.Create(number, order.Id, order.PONumber, order.SupplierId, tenantId, InvoiceMapper.CurrencyOf(order), request.Reason, lines, returnable);
            r.CreatedBy = user.UserId;
            return r;
        });

        _store.Add(vendorReturn);
        await _store.SaveChangesAsync(ct);
        return VendorReturnMapper.ToDto(vendorReturn, (await _store.GetSupplierAsync(order.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class ShipVendorReturnCommandHandler : IRequestHandler<ShipVendorReturnCommand, VendorReturnDto>
{
    private readonly IProcurementStore _store;
    public ShipVendorReturnCommandHandler(IProcurementStore store) => _store = store;

    public async Task<VendorReturnDto> Handle(ShipVendorReturnCommand request, CancellationToken ct)
    {
        var r = await _store.GetReturnAsync(request.ReturnId, ct) ?? throw new KeyNotFoundException($"Vendor return {request.ReturnId} not found");
        Rules.Run(r.Ship);
        await _store.SaveChangesAsync(ct);
        return VendorReturnMapper.ToDto(r, (await _store.GetSupplierAsync(r.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class CancelVendorReturnCommandHandler : IRequestHandler<CancelVendorReturnCommand, VendorReturnDto>
{
    private readonly IProcurementStore _store;
    public CancelVendorReturnCommandHandler(IProcurementStore store) => _store = store;

    public async Task<VendorReturnDto> Handle(CancelVendorReturnCommand request, CancellationToken ct)
    {
        var r = await _store.GetReturnAsync(request.ReturnId, ct) ?? throw new KeyNotFoundException($"Vendor return {request.ReturnId} not found");
        Rules.Run(r.Cancel);
        await _store.SaveChangesAsync(ct);
        return VendorReturnMapper.ToDto(r, (await _store.GetSupplierAsync(r.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class RecordVendorCreditCommandHandler : IRequestHandler<RecordVendorCreditCommand, VendorReturnDto>
{
    private readonly IProcurementStore _store;
    public RecordVendorCreditCommandHandler(IProcurementStore store) => _store = store;

    public async Task<VendorReturnDto> Handle(RecordVendorCreditCommand request, CancellationToken ct)
    {
        var r = await _store.GetReturnAsync(request.ReturnId, ct) ?? throw new KeyNotFoundException($"Vendor return {request.ReturnId} not found");
        Rules.Run(() => r.RecordCredit(request.CreditNoteNumber, request.CreditAmount));
        await _store.SaveChangesAsync(ct);
        return VendorReturnMapper.ToDto(r, (await _store.GetSupplierAsync(r.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class GetVendorReturnQueryHandler : IRequestHandler<GetVendorReturnQuery, VendorReturnDto?>
{
    private readonly IProcurementStore _store;
    public GetVendorReturnQueryHandler(IProcurementStore store) => _store = store;

    public async Task<VendorReturnDto?> Handle(GetVendorReturnQuery request, CancellationToken ct)
    {
        var r = await _store.GetReturnAsync(request.ReturnId, ct);
        return r == null ? null : VendorReturnMapper.ToDto(r, (await _store.GetSupplierAsync(r.SupplierId, ct))?.Name ?? string.Empty);
    }
}

public class ListVendorReturnsQueryHandler : IRequestHandler<ListVendorReturnsQuery, List<VendorReturnSummaryDto>>
{
    private readonly IProcurementStore _store;
    public ListVendorReturnsQueryHandler(IProcurementStore store) => _store = store;

    public async Task<List<VendorReturnSummaryDto>> Handle(ListVendorReturnsQuery request, CancellationToken ct)
    {
        VendorReturnStatus? status = Enum.TryParse<VendorReturnStatus>(request.Status, true, out var s) ? s : null;
        var list = await _store.ListReturnsAsync(request.PurchaseOrderId, status, ct);
        var names = await _store.GetSupplierNamesAsync(list.Select(r => r.SupplierId), ct);
        return list.Select(r => new VendorReturnSummaryDto(r.Id, r.ReturnNumber, r.PONumber, names.GetValueOrDefault(r.SupplierId, string.Empty), r.CurrencyCode, r.Status.ToString(), r.Value, r.CreatedAt)).ToList();
    }
}
