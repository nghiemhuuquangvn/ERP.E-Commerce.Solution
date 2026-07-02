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
    private readonly ITransactionRepository _transactions;
    public OpenTransactionCommandHandler(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<TransactionDto> Handle(OpenTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = Transaction.Open(request.TerminalId, request.CashierId, request.CashShiftId);
        await _transactions.AddAsync(transaction, cancellationToken);
        return MapToDto(transaction);
    }

    internal static TransactionDto MapToDto(Transaction t) => new()
    {
        Id = t.Id, TerminalId = t.TerminalId, CashierId = t.CashierId,
        SubTotal = t.SubTotal, DiscountAmount = t.DiscountAmount,
        TaxAmount = t.TaxAmount, TotalAmount = t.TotalAmount,
        PaymentMethod = t.PaymentMethod, Status = t.Status.ToString(),
        CompletedAt = t.CompletedAt, CreatedAt = t.CreatedAt,
        Items = t.Items.Select(i => new TransactionItemDto
        {
            ProductId = i.ProductId, ProductName = i.ProductName,
            Quantity = i.Quantity, UnitPrice = i.UnitPrice,
            TaxRate = i.TaxRate, LineTotal = i.LineTotal
        }).ToList()
    };
}

public class AddTransactionItemCommandHandler : IRequestHandler<AddTransactionItemCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    public AddTransactionItemCommandHandler(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<TransactionDto> Handle(AddTransactionItemCommand request, CancellationToken cancellationToken)
    {
        var t = await _transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException($"Transaction {request.TransactionId} not found.");

        t.AddItem(request.ProductId, request.ProductName, request.Quantity, request.UnitPrice, request.TaxRate);
        await _transactions.UpdateAsync(t, cancellationToken);
        return OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class ApplyDiscountCommandHandler : IRequestHandler<ApplyDiscountCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    public ApplyDiscountCommandHandler(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<TransactionDto> Handle(ApplyDiscountCommand request, CancellationToken cancellationToken)
    {
        var t = await _transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaction not found.");

        t.ApplyDiscount(request.DiscountAmount);
        await _transactions.UpdateAsync(t, cancellationToken);
        return OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class CompleteTransactionCommandHandler : IRequestHandler<CompleteTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    private readonly ICashShiftRepository _shifts;

    public CompleteTransactionCommandHandler(ITransactionRepository transactions, ICashShiftRepository shifts)
    {
        _transactions = transactions;
        _shifts = shifts;
    }

    public async Task<TransactionDto> Handle(CompleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var t = await _transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaction not found.");

        t.Complete(request.PaymentMethod);

        if (t.CashShiftId.HasValue)
        {
            var shift = await _shifts.GetByIdAsync(t.CashShiftId.Value, cancellationToken);
            shift?.RecordTransaction(t.TotalAmount);
            if (shift != null) await _shifts.UpdateAsync(shift, cancellationToken);
        }

        await _transactions.UpdateAsync(t, cancellationToken);
        return OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class VoidTransactionCommandHandler : IRequestHandler<VoidTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactions;
    public VoidTransactionCommandHandler(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<TransactionDto> Handle(VoidTransactionCommand request, CancellationToken cancellationToken)
    {
        var t = await _transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaction not found.");

        t.Void(request.Reason);
        await _transactions.UpdateAsync(t, cancellationToken);
        return OpenTransactionCommandHandler.MapToDto(t);
    }
}

public class OpenCashShiftCommandHandler : IRequestHandler<OpenCashShiftCommand, CashShiftDto>
{
    private readonly ICashShiftRepository _shifts;
    public OpenCashShiftCommandHandler(ICashShiftRepository shifts) => _shifts = shifts;

    public async Task<CashShiftDto> Handle(OpenCashShiftCommand request, CancellationToken cancellationToken)
    {
        var existing = await _shifts.GetOpenByTerminalAsync(request.TerminalId, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException("Terminal already has an open shift.");

        var shift = CashShift.Open(request.TerminalId, request.CashierId, request.OpeningBalance);
        await _shifts.AddAsync(shift, cancellationToken);
        return MapShiftToDto(shift);
    }

    internal static CashShiftDto MapShiftToDto(CashShift s) => new()
    {
        Id = s.Id, TerminalId = s.TerminalId, CashierId = s.CashierId,
        OpeningBalance = s.OpeningBalance, ClosingBalance = s.ClosingBalance,
        TotalSales = s.TotalSales, TransactionCount = s.TransactionCount,
        Status = s.Status.ToString(), OpenedAt = s.OpenedAt, ClosedAt = s.ClosedAt
    };
}

public class CloseCashShiftCommandHandler : IRequestHandler<CloseCashShiftCommand, CashShiftDto>
{
    private readonly ICashShiftRepository _shifts;
    public CloseCashShiftCommandHandler(ICashShiftRepository shifts) => _shifts = shifts;

    public async Task<CashShiftDto> Handle(CloseCashShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await _shifts.GetByIdAsync(request.ShiftId, cancellationToken)
            ?? throw new InvalidOperationException("Shift not found.");

        shift.Close(request.ClosingBalance, request.Notes);
        await _shifts.UpdateAsync(shift, cancellationToken);
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
        var completed = transactions.Where(t => t.Status == TransactionStatus.Completed).ToList();

        return new ShiftSummaryDto
        {
            ShiftId = shift.Id,
            CompletedTransactionCount = completed.Count,
            VoidedTransactionCount = transactions.Count(t => t.Status == TransactionStatus.Voided),
            TotalSales = completed.Sum(t => t.TotalAmount),
            TotalDiscount = completed.Sum(t => t.DiscountAmount),
            TotalTax = completed.Sum(t => t.TaxAmount)
        };
    }
}
