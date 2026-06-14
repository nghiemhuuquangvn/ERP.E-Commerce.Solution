namespace ERP.Identity.Application.DTOs.Common;

public record PagedResponse<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
