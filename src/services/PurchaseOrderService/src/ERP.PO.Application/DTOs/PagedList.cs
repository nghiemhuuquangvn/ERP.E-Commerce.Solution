namespace ERP.PO.Application.DTOs;

/// <summary>One page of a list and how many rows match in all (the shape every paged list of the platform answers: items, totalCount, pageNumber, pageSize).</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>The page number and size as the server will use them (at least 1; the size at most 100).</summary>
    public static (int PageNumber, int PageSize) Clamp(int pageNumber, int pageSize) =>
        (Math.Max(1, pageNumber), Math.Min(MaxPageSize, Math.Max(1, pageSize)));

    public PagedList<TOut> Map<TOut>(Func<T, TOut> map) => new(Items.Select(map).ToList(), TotalCount, PageNumber, PageSize);
}
