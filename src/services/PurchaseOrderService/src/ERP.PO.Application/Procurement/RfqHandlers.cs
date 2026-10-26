using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;
using ERP.Shared.Authorization;
using MediatR;

namespace ERP.PO.Application.Procurement;

// ─── DTOs ─────────────────────────────────────────────────────────────────

public record RfqLineDto(Guid Id, string ProductCode, string ProductName, decimal Quantity, string Unit);
public record RfqInvitationDto(Guid SupplierId, string SupplierName, string State);
public record QuotationLineDto(Guid RfqLineId, decimal UnitPrice);
public record QuotationDto(Guid Id, Guid SupplierId, string SupplierName, string QuoteNumber, DateTime ValidUntil, string CurrencyCode, int LeadTimeDays, string? Notes, bool IsWinner, List<QuotationLineDto> Lines);
public record RfqDto(
    Guid Id, string RfqNumber, string Title, DateTime? NeededBy, string? Notes, string Status, DateTime? SentAt,
    Guid? AwardedSupplierId, Guid? AwardedPurchaseOrderId, DateTime CreatedAt,
    List<RfqLineDto> Lines, List<RfqInvitationDto> Invitations, List<QuotationDto> Quotations);
public record RfqSummaryDto(Guid Id, string RfqNumber, string Title, string Status, int Lines, int Invited, int Quoted, DateTime? NeededBy, DateTime CreatedAt);
public record RfqComparisonDto(
    Guid RfqId, string RfqNumber, string Status,
    List<RfqComparisonLineDto> Lines, List<RfqComparisonSupplierDto> Suppliers);
public record RfqComparisonLineDto(Guid LineId, string ProductCode, string ProductName, decimal Quantity, string Unit, decimal? LowestPrice, List<RfqComparisonPriceDto> Prices);
public record RfqComparisonPriceDto(Guid SupplierId, decimal UnitPrice, bool Expired, bool IsLowest);
public record RfqComparisonSupplierDto(Guid SupplierId, string SupplierName, string QuoteNumber, string CurrencyCode, int LeadTimeDays, DateTime ValidUntil, bool Expired, bool IsWinner, decimal Total);

internal static class RfqMapper
{
    public static async Task<RfqDto> ToDtoAsync(RequestForQuotation r, IProcurementStore store, CancellationToken ct)
    {
        var names = await store.GetSupplierNamesAsync(r.Invitations.Select(i => i.SupplierId).Concat(r.Quotations.Select(q => q.SupplierId)), ct);
        string Name(Guid id) => names.TryGetValue(id, out var n) ? n : id.ToString();
        return new RfqDto(r.Id, r.RfqNumber, r.Title, r.NeededBy, r.Notes, r.Status.ToString(), r.SentAt, r.AwardedSupplierId, r.AwardedPurchaseOrderId, r.CreatedAt,
            r.Lines.Select(l => new RfqLineDto(l.Id, l.ProductCode, l.ProductName, l.Quantity, l.Unit)).ToList(),
            r.Invitations.Select(i => new RfqInvitationDto(i.SupplierId, Name(i.SupplierId), i.State.ToString())).ToList(),
            r.Quotations.Select(q => new QuotationDto(q.Id, q.SupplierId, Name(q.SupplierId), q.QuoteNumber, q.ValidUntil, q.CurrencyCode, q.LeadTimeDays, q.Notes, q.IsWinner,
                q.Lines.Select(l => new QuotationLineDto(l.RfqLineId, l.UnitPrice)).ToList())).ToList());
    }
}

// ─── commands / queries ───────────────────────────────────────────────────

public record RfqLineInput(string ProductCode, string ProductName, decimal Quantity, string Unit);

public record CreateRfqCommand(string Title, DateTime? NeededBy, string? Notes, List<RfqLineInput> Lines, List<Guid> SupplierIds) : IRequest<RfqDto>;
public record SendRfqCommand(Guid RfqId) : IRequest<RfqDto>;
public record CancelRfqCommand(Guid RfqId) : IRequest<RfqDto>;
public record RecordQuotationCommand(
    Guid RfqId, Guid SupplierId, string QuoteNumber, DateTime ValidUntil, string CurrencyCode, int LeadTimeDays, string? Notes,
    Dictionary<Guid, decimal> UnitPrices) : IRequest<RfqDto>;
public record DeclineRfqCommand(Guid RfqId, Guid SupplierId) : IRequest<RfqDto>;
public record AwardRfqCommand(Guid RfqId, Guid SupplierId) : IRequest<RfqDto>;
public record GetRfqQuery(Guid RfqId) : IRequest<RfqDto?>;
public record ListRfqsQuery(string? Status) : IRequest<List<RfqSummaryDto>>;
public record CompareRfqQuery(Guid RfqId) : IRequest<RfqComparisonDto?>;

// ─── handlers ─────────────────────────────────────────────────────────────

public class CreateRfqCommandHandler : IRequestHandler<CreateRfqCommand, RfqDto>
{
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public CreateRfqCommandHandler(IProcurementStore store, IUserContextService user) { _store = store; _user = user; }

    public async Task<RfqDto> Handle(CreateRfqCommand request, CancellationToken ct)
    {
        var tenantId = new Guid(_user.GetCurrentUser().TenantId);
        var number = $"RFQ-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var rfq = Rules.Run(() =>
        {
            var r = RequestForQuotation.Create(number, request.Title, tenantId, request.NeededBy, request.Notes);
            r.CreatedBy = _user.GetCurrentUser().UserId;
            foreach (var l in request.Lines ?? new()) r.AddLine(l.ProductCode, l.ProductName, l.Quantity, l.Unit);
            foreach (var s in request.SupplierIds ?? new()) r.Invite(s);
            return r;
        });

        foreach (var supplierId in rfq.Invitations.Select(i => i.SupplierId))
        {
            var supplier = await _store.GetSupplierAsync(supplierId, ct);
            if (supplier == null || !supplier.IsActive)
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Purchasing", "Every invited supplier must exist and be active.");
        }

        _store.Add(rfq);
        await _store.SaveChangesAsync(ct);
        return await RfqMapper.ToDtoAsync(rfq, _store, ct);
    }
}

public class SendRfqCommandHandler : IRequestHandler<SendRfqCommand, RfqDto>
{
    private readonly IProcurementStore _store;
    public SendRfqCommandHandler(IProcurementStore store) => _store = store;

    public async Task<RfqDto> Handle(SendRfqCommand request, CancellationToken ct)
    {
        var rfq = await _store.GetRfqAsync(request.RfqId, ct) ?? throw new KeyNotFoundException($"RFQ {request.RfqId} not found");
        Rules.Run(rfq.Send);
        await _store.SaveChangesAsync(ct);
        return await RfqMapper.ToDtoAsync(rfq, _store, ct);
    }
}

public class CancelRfqCommandHandler : IRequestHandler<CancelRfqCommand, RfqDto>
{
    private readonly IProcurementStore _store;
    public CancelRfqCommandHandler(IProcurementStore store) => _store = store;

    public async Task<RfqDto> Handle(CancelRfqCommand request, CancellationToken ct)
    {
        var rfq = await _store.GetRfqAsync(request.RfqId, ct) ?? throw new KeyNotFoundException($"RFQ {request.RfqId} not found");
        Rules.Run(rfq.Cancel);
        await _store.SaveChangesAsync(ct);
        return await RfqMapper.ToDtoAsync(rfq, _store, ct);
    }
}

public class RecordQuotationCommandHandler : IRequestHandler<RecordQuotationCommand, RfqDto>
{
    private readonly IProcurementStore _store;
    public RecordQuotationCommandHandler(IProcurementStore store) => _store = store;

    public async Task<RfqDto> Handle(RecordQuotationCommand request, CancellationToken ct)
    {
        var rfq = await _store.GetRfqAsync(request.RfqId, ct) ?? throw new KeyNotFoundException($"RFQ {request.RfqId} not found");
        Rules.Run(() => rfq.RecordQuotation(request.SupplierId, request.QuoteNumber, request.ValidUntil, request.CurrencyCode, request.LeadTimeDays,
            request.Notes, request.UnitPrices ?? new(), DateTime.UtcNow));
        await _store.SaveChangesAsync(ct);
        return await RfqMapper.ToDtoAsync(rfq, _store, ct);
    }
}

public class DeclineRfqCommandHandler : IRequestHandler<DeclineRfqCommand, RfqDto>
{
    private readonly IProcurementStore _store;
    public DeclineRfqCommandHandler(IProcurementStore store) => _store = store;

    public async Task<RfqDto> Handle(DeclineRfqCommand request, CancellationToken ct)
    {
        var rfq = await _store.GetRfqAsync(request.RfqId, ct) ?? throw new KeyNotFoundException($"RFQ {request.RfqId} not found");
        Rules.Run(() => rfq.Decline(request.SupplierId));
        await _store.SaveChangesAsync(ct);
        return await RfqMapper.ToDtoAsync(rfq, _store, ct);
    }
}

/// <summary>Awards the RFQ and creates the Draft purchase order at the quoted prices, in the same save.</summary>
public class AwardRfqCommandHandler : IRequestHandler<AwardRfqCommand, RfqDto>
{
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public AwardRfqCommandHandler(IProcurementStore store, IUserContextService user) { _store = store; _user = user; }

    public async Task<RfqDto> Handle(AwardRfqCommand request, CancellationToken ct)
    {
        var user = _user.GetCurrentUser();
        var tenantId = new Guid(user.TenantId);
        var rfq = await _store.GetRfqAsync(request.RfqId, ct) ?? throw new KeyNotFoundException($"RFQ {request.RfqId} not found");

        var quotation = Rules.Run(() => rfq.Award(request.SupplierId, DateTime.UtcNow));

        var order = Rules.Run(() =>
        {
            var po = PurchaseOrder.Create($"PO-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}", request.SupplierId, tenantId,
                null, $"From {rfq.RfqNumber} (quotation {quotation.QuoteNumber}), lead time {quotation.LeadTimeDays} days");
            po.CreatedBy = user.UserId;
            foreach (var line in rfq.Lines)
                po.AddItem(PurchaseOrderItem.Create(po.Id, line.ProductCode, line.ProductName, line.Quantity, line.Unit,
                    new Money(quotation.PriceFor(line.Id), quotation.CurrencyCode)));
            return po;
        });

        rfq.LinkPurchaseOrder(order.Id);
        _store.Add(order);
        await _store.SaveChangesAsync(ct);
        return await RfqMapper.ToDtoAsync(rfq, _store, ct);
    }
}

public class GetRfqQueryHandler : IRequestHandler<GetRfqQuery, RfqDto?>
{
    private readonly IProcurementStore _store;
    public GetRfqQueryHandler(IProcurementStore store) => _store = store;

    public async Task<RfqDto?> Handle(GetRfqQuery request, CancellationToken ct)
    {
        var rfq = await _store.GetRfqAsync(request.RfqId, ct);
        return rfq == null ? null : await RfqMapper.ToDtoAsync(rfq, _store, ct);
    }
}

public class ListRfqsQueryHandler : IRequestHandler<ListRfqsQuery, List<RfqSummaryDto>>
{
    private readonly IProcurementStore _store;
    public ListRfqsQueryHandler(IProcurementStore store) => _store = store;

    public async Task<List<RfqSummaryDto>> Handle(ListRfqsQuery request, CancellationToken ct)
    {
        RfqStatus? status = Enum.TryParse<RfqStatus>(request.Status, true, out var s) ? s : null;
        var list = await _store.ListRfqsAsync(status, ct);
        return list.Select(r => new RfqSummaryDto(r.Id, r.RfqNumber, r.Title, r.Status.ToString(), r.Lines.Count, r.Invitations.Count,
            r.Invitations.Count(i => i.State == RfqInvitationState.Quoted), r.NeededBy, r.CreatedAt)).ToList();
    }
}

public class CompareRfqQueryHandler : IRequestHandler<CompareRfqQuery, RfqComparisonDto?>
{
    private readonly IProcurementStore _store;
    public CompareRfqQueryHandler(IProcurementStore store) => _store = store;

    public async Task<RfqComparisonDto?> Handle(CompareRfqQuery request, CancellationToken ct)
    {
        var rfq = await _store.GetRfqAsync(request.RfqId, ct);
        if (rfq == null) return null;

        var c = rfq.Compare(DateTime.UtcNow);
        var names = await _store.GetSupplierNamesAsync(c.Suppliers.Select(s => s.SupplierId), ct);
        string Name(Guid id) => names.TryGetValue(id, out var n) ? n : id.ToString();

        return new RfqComparisonDto(c.RfqId, c.RfqNumber, c.Status.ToString(),
            c.Lines.Select(l => new RfqComparisonLineDto(l.LineId, l.ProductCode, l.ProductName, l.Quantity, l.Unit, l.LowestPrice,
                l.Prices.Select(p => new RfqComparisonPriceDto(p.SupplierId, p.UnitPrice, p.Expired, !p.Expired && l.LowestPrice.HasValue && p.UnitPrice == l.LowestPrice)).ToList())).ToList(),
            c.Suppliers.Select(s => new RfqComparisonSupplierDto(s.SupplierId, Name(s.SupplierId), s.QuoteNumber, s.CurrencyCode, s.LeadTimeDays, s.ValidUntil, s.Expired, s.IsWinner, s.Total)).ToList());
    }
}
