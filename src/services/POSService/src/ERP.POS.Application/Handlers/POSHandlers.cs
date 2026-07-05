using ERP.POS.Application.Commands;
using ERP.POS.Application.DTOs;
using ERP.POS.Application.Interfaces;
using ERP.POS.Application.Queries;
using ERP.POS.Domain.Entities;
using ERP.POS.Domain.Enums;
using MediatR;

namespace ERP.POS.Application.Handlers;

public class OpenTransactionCommandHandler : IRequestHandler<OpenTransactionCommand, TransactionDto>
{
    private readonly IPosUnitOfWork _uow;
    public OpenTransactionCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<TransactionDto> Handle(OpenTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = Transaction.Open(request.TerminalId, request.CashierId, request.CashShiftId);
        await _uow.Transactions.AddAsync(transaction, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return MapToDto(transaction);
    }

    internal static TransactionDto MapToDto(Transaction t) => new()
    {
        Id = t.Id, TerminalId = t.TerminalId, CashierId = t.CashierId,
        PromotionId = t.PromotionId,
        SubTotal = t.SubTotal, DiscountAmount = t.DiscountAmount,
        TaxAmount = t.TaxAmount, TotalAmount = t.TotalAmount,
        PaymentMethod = t.PaymentMethod, Status = t.Status.ToString(),
        VoidReason = t.VoidReason,
        CompletedAt = t.CompletedAt, VoidedAt = t.VoidedAt,
        ExternalPaymentId = t.ExternalPaymentId, CreatedAt = t.CreatedAt,
        Items = t.Items.Select(i => new TransactionItemDto
        {
            ProductId = i.ProductId, ProductName = i.ProductName,
            Quantity = i.Quantity, UnitPrice = i.UnitPrice,
            TaxRate = i.TaxRate, DiscountAmount = i.DiscountAmount,
            LineTotal = i.LineTotal, TaxAmount = i.TaxAmount
        }).ToList(),
        Tenders = t.Tenders.Select(x => new TenderDto { PaymentMethod = x.PaymentMethod, Amount = x.Amount }).ToList()
    };
}

public class AddTransactionItemCommandHandler : IRequestHandler<AddTransactionItemCommand, TransactionDto>
{
    private readonly IPosUnitOfWork _uow;
    public AddTransactionItemCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<TransactionDto> Handle(AddTransactionItemCommand request, CancellationToken cancellationToken)
    {
        var t = await _uow.Transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException($"Transaction {request.TransactionId} not found.");

        t.AddItem(request.ProductId, request.ProductName, request.Quantity, request.UnitPrice, request.TaxRate);
        await _uow.Transactions.UpdateAsync(t, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class ApplyDiscountCommandHandler : IRequestHandler<ApplyDiscountCommand, TransactionDto>
{
    private readonly IPosUnitOfWork _uow;
    public ApplyDiscountCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<TransactionDto> Handle(ApplyDiscountCommand request, CancellationToken cancellationToken)
    {
        var t = await _uow.Transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaction not found.");

        t.ApplyDiscount(request.DiscountAmount, request.PromotionId);
        await _uow.Transactions.UpdateAsync(t, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class CompleteTransactionCommandHandler : IRequestHandler<CompleteTransactionCommand, TransactionDto>
{
    private readonly IPosUnitOfWork _uow;

    public CompleteTransactionCommandHandler(IPosUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<TransactionDto> Handle(CompleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var t = await _uow.Transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaction not found.");

        if (request.Tenders is { Count: > 0 })
            t.CompleteWithTenders(request.Tenders.Select(x => (x.PaymentMethod, x.Amount)).ToList());
        else
            t.Complete(request.PaymentMethod);

        if (t.CashShiftId.HasValue)
        {
            var shift = await _uow.CashShifts.GetByIdAsync(t.CashShiftId.Value, cancellationToken);
            shift?.RecordTransaction(t.TotalAmount);
            if (shift != null) await _uow.CashShifts.UpdateAsync(shift, cancellationToken);
        }

        await _uow.Transactions.UpdateAsync(t, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class VoidTransactionCommandHandler : IRequestHandler<VoidTransactionCommand, TransactionDto>
{
    private readonly IPosUnitOfWork _uow;
    public VoidTransactionCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<TransactionDto> Handle(VoidTransactionCommand request, CancellationToken cancellationToken)
    {
        var t = await _uow.Transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaction not found.");

        t.Void(request.Reason);
        await _uow.Transactions.UpdateAsync(t, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class OpenCashShiftCommandHandler : IRequestHandler<OpenCashShiftCommand, CashShiftDto>
{
    private readonly IPosUnitOfWork _uow;
    public OpenCashShiftCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<CashShiftDto> Handle(OpenCashShiftCommand request, CancellationToken cancellationToken)
    {
        var existing = await _uow.CashShifts.GetOpenByTerminalAsync(request.TerminalId, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException("Terminal already has an open shift.");

        var openingCounts = request.OpeningCounts?.Select(c => (c.Denomination, c.Count)).ToList();
        var shift = CashShift.Open(request.TerminalId, request.CashierId, request.OpeningBalance, openingCounts);
        await _uow.CashShifts.AddAsync(shift, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return MapShiftToDto(shift);
    }

    internal static CashShiftDto MapShiftToDto(CashShift s) => new()
    {
        Id = s.Id, TerminalId = s.TerminalId, CashierId = s.CashierId,
        OpeningBalance = s.OpeningBalance, ClosingBalance = s.ClosingBalance,
        TotalSales = s.TotalSales, TransactionCount = s.TransactionCount,
        Status = s.Status.ToString(), OpenedAt = s.OpenedAt, ClosedAt = s.ClosedAt,
        OpeningDenominations = s.Denominations.Where(d => d.Stage == Domain.Entities.DenominationStage.Opening)
            .Select(d => new DenominationDto { Denomination = d.Denomination, Count = d.Count, LineTotal = d.LineTotal }).ToList(),
        ClosingDenominations = s.Denominations.Where(d => d.Stage == Domain.Entities.DenominationStage.Closing)
            .Select(d => new DenominationDto { Denomination = d.Denomination, Count = d.Count, LineTotal = d.LineTotal }).ToList()
    };
}

public class CloseCashShiftCommandHandler : IRequestHandler<CloseCashShiftCommand, CashShiftDto>
{
    private readonly IPosUnitOfWork _uow;
    public CloseCashShiftCommandHandler(IPosUnitOfWork uow) => _uow = uow;

    public async Task<CashShiftDto> Handle(CloseCashShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await _uow.CashShifts.GetByIdAsync(request.ShiftId, cancellationToken)
            ?? throw new InvalidOperationException("Shift not found.");

        var closingCounts = request.ClosingCounts?.Select(c => (c.Denomination, c.Count)).ToList();
        shift.Close(request.ClosingBalance, request.Notes, closingCounts);
        await _uow.CashShifts.UpdateAsync(shift, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return OpenCashShiftCommandHandler.MapShiftToDto(shift);
    }
}

public class GetTransactionQueryHandler : IRequestHandler<GetTransactionQuery, TransactionDto?>
{
    private readonly ITransactionRepository _transactions;
    public GetTransactionQueryHandler(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<TransactionDto?> Handle(GetTransactionQuery request, CancellationToken cancellationToken)
    {
        var t = await _transactions.GetByIdAsync(request.TransactionId, cancellationToken);
        return t == null ? null : OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class GetActiveShiftQueryHandler : IRequestHandler<GetActiveShiftQuery, CashShiftDto?>
{
    private readonly ICashShiftRepository _shifts;
    public GetActiveShiftQueryHandler(ICashShiftRepository shifts) => _shifts = shifts;

    public async Task<CashShiftDto?> Handle(GetActiveShiftQuery request, CancellationToken cancellationToken)
    {
        var shift = await _shifts.GetOpenByTerminalAsync(request.TerminalId, cancellationToken);
        return shift == null ? null : OpenCashShiftCommandHandler.MapShiftToDto(shift);
    }
}

public class GetShiftSummaryQueryHandler : IRequestHandler<GetShiftSummaryQuery, ShiftSummaryDto?>
{
    private readonly ICashShiftRepository _shifts;
    private readonly ITransactionRepository _transactions;

    public GetShiftSummaryQueryHandler(ICashShiftRepository shifts, ITransactionRepository transactions)
    {
        _shifts = shifts;
        _transactions = transactions;
    }

    public async Task<ShiftSummaryDto?> Handle(GetShiftSummaryQuery request, CancellationToken cancellationToken)
    {
        var shift = await _shifts.GetByIdAsync(request.ShiftId, cancellationToken);
        if (shift == null)
            return null;

        var transactions = await _transactions.GetByShiftAsync(request.ShiftId, cancellationToken);
        return BuildSummary(shift.Id, transactions);
    }

    internal static ShiftSummaryDto BuildSummary(Guid shiftId, List<Transaction> transactions)
    {
        var completed = transactions.Where(t => t.Status == TransactionStatus.Completed).ToList();

        var salesByMethod = completed
            .SelectMany(t => t.Tenders.Any()
                ? t.Tenders.Select(x => (x.PaymentMethod, x.Amount))
                : new List<(string, decimal)> { (t.PaymentMethod ?? "Unknown", t.TotalAmount) })
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Item2));

        return new ShiftSummaryDto
        {
            ShiftId = shiftId,
            CompletedTransactionCount = completed.Count,
            VoidedTransactionCount = transactions.Count(t => t.Status == TransactionStatus.Voided),
            TotalSales = completed.Sum(t => t.TotalAmount),
            TotalDiscount = completed.Sum(t => t.DiscountAmount),
            TotalTax = completed.Sum(t => t.TaxAmount),
            SalesByPaymentMethod = salesByMethod
        };
    }
}
