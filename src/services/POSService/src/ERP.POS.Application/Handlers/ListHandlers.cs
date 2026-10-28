using ERP.POS.Application.DTOs;
using ERP.POS.Application.Interfaces;
using ERP.POS.Application.Queries;
using ERP.POS.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.POS.Application.Handlers;

/// <summary>Back-office lists (ROADMAP P2-1a). Reads only; the tenant filter of the DbContext scopes them.</summary>
internal static class ListPaging
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize, int Skip) Normalize(int page, int pageSize)
    {
        var p = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        return (p, size, (p - 1) * size);
    }

    /// <summary>The query's last day is inclusive, the repositories take an exclusive end.</summary>
    public static DateTime? EndExclusive(DateTime? to) => to?.Date.AddDays(1);

    public static TEnum? ParseStatus<TEnum>(string? status) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        if (Enum.TryParse<TEnum>(status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new BusinessRuleException("Pos.Status", $"Unknown status '{status}'.");
    }
}

public class ListTransactionsQueryHandler : IRequestHandler<ListTransactionsQuery, PosPagedResult<TransactionDto>>
{
    private readonly ITransactionRepository _transactions;
    public ListTransactionsQueryHandler(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<PosPagedResult<TransactionDto>> Handle(ListTransactionsQuery request, CancellationToken cancellationToken)
    {
        var (page, size, skip) = ListPaging.Normalize(request.Page, request.PageSize);
        var (items, total) = await _transactions.SearchAsync(request.TerminalId, request.CashierId, ListPaging.ParseStatus<TransactionStatus>(request.Status),
            request.From?.Date, ListPaging.EndExclusive(request.To), skip, size, cancellationToken);
        return new PosPagedResult<TransactionDto>
        {
            Items = items.Select(OpenTransactionCommandHandler.MapToDto).ToList(),
            Total = total, Page = page, PageSize = size,
        };
    }
}

public class ListShiftsQueryHandler : IRequestHandler<ListShiftsQuery, PosPagedResult<CashShiftDto>>
{
    private readonly ICashShiftRepository _shifts;
    public ListShiftsQueryHandler(ICashShiftRepository shifts) => _shifts = shifts;

    public async Task<PosPagedResult<CashShiftDto>> Handle(ListShiftsQuery request, CancellationToken cancellationToken)
    {
        var (page, size, skip) = ListPaging.Normalize(request.Page, request.PageSize);
        var (items, total) = await _shifts.SearchAsync(request.TerminalId, ListPaging.ParseStatus<CashShiftStatus>(request.Status),
            request.From?.Date, ListPaging.EndExclusive(request.To), skip, size, cancellationToken);
        return new PosPagedResult<CashShiftDto>
        {
            Items = items.Select(OpenCashShiftCommandHandler.MapShiftToDto).ToList(),
            Total = total, Page = page, PageSize = size,
        };
    }
}
